using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Vehicle;
using System.Runtime.InteropServices;

namespace RacingSim.Core
{
    /// <summary>
    /// [OBSOLETE] Needs rewrite to use new types:
    /// - ChassisRigidBody instead of manual integration
    /// - IVehicleInput instead of Input.GetAxis
    /// - DamageConfig instead of VehicleConfig for damage
    /// - AdvancedEngine/AdvancedTransmission instead of DrivetrainSystem
    /// 
    /// This file references many non-existent fields and will not compile.
    /// Retained for integration reference only.
    /// </summary>
    [System.Obsolete("Needs rewrite to use ChassisRigidBody, IVehicleInput, DamageConfig")]
    public class SimulationLoop_OLD : MonoBehaviour
    {
        // Intentionally empty - legacy code preserved in git history
    }

    /// <summary>
    /// Main simulation loop that integrates all physics systems
    /// Runs at fixed timestep (240Hz or higher) independent of render framerate
    /// 
    /// SIMULATION ORDER:
    /// 1. Read Input
    /// 2. Electronics (TC/ABS intervention calculation)
    /// 3. Drivetrain (engine, clutch, gearbox, differential)
    /// 4. Suspension kinematics
    /// 5. Tire forces (Pacejka)
    /// 6. Aerodynamics
    /// 7. Load transfer & weight distribution
    /// 8. Rigid body integration
    /// 9. Thermal updates
    /// 10. Damage checks
    /// 11. Telemetry logging
    /// </summary>
    [BurstCompile]
    public static class SimulationLoop
    {
        /// <summary>
        /// Fixed timestep for physics simulation
        /// Recommended: 1/240 = 0.00416667s for high accuracy
        /// Can go up to 1/360 or 1/480 for professional-grade sim
        /// </summary>
        public const float FIXED_TIMESTEP = 1f / 240f;

        /// <summary>
        /// Complete vehicle state for one simulation tick
        /// </summary>
        public struct SimulationState
        {
            // Rigid Body State
            public float3 Position;
            public quaternion Rotation;
            public float3 Velocity;
            public float3 AngularVelocity;
            public float3 Acceleration;
            
            // Input (from player or AI)
            public float ThrottleInput;
            public float BrakeInput;
            public float SteeringInput;
            public float ClutchInput;
            public int GearRequest;
            public bool DRSRequested;
            
            // Wheel states (4 wheels)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public Physics.WheelState[] Wheels;
            
            // Drivetrain state
            public Drivetrain.DrivetrainState Drivetrain;
            
            // Electronics state
            public Electronics.ElectronicsState Electronics;
            
            // Thermal state
            public Thermal.ThermalState Thermal;
            
            // Damage state
            public Damage.DamageStateData Damage;
            
            // Aero state
            public Aero.AeroState Aero;
            
            // Timing
            public double Timestamp;
            public int FrameIndex;
        }

        /// <summary>
        /// Main simulation update function
        /// Call this every FixedUpdate from MonoBehaviour
        /// </summary>
        [BurstCompile]
        public static void Update(
            ref SimulationState state,
            float dt,
            in VehicleConfig config,
            in DamageConfig damageConfig,
            NativeArray<float3> trackNormals,
            float3 windVelocity,
            float ambientTemp,
            float trackTemp)
        {
            // Validate timestep
            if (dt <= 0f || dt > 0.1f)
            {
                Debug.LogError($"Invalid timestep: {dt}");
                return;
            }

            // ============================================================
            // STEP 1: Electronics (TC/ABS intervention calculation)
            // Must happen first as it modifies throttle/brake inputs
            // ============================================================
            
            float correctedThrottle = state.ThrottleInput;
            float[] brakePressures = new float[4];
            
            // Traction Control
            if (config.tcEnabled && config.tcLevel > 0f)
            {
                float[] slipRatios = new float[4];
                for (int i = 0; i < 4; i++)
                {
                    slipRatios[i] = state.Wheels[i].SlipRatio;
                }
                
                int[] drivenWheels = GetDrivenWheels(config.drivetrainType);
                var tcPID = new Electronics.PIDController(config.tcKp, config.tcKi, config.tcKd);
                
                var tcResult = Electronics.TractionControl.Calculate(
                    slipRatios, drivenWheels, correctedThrottle, 
                    config.tcLevel, config, ref tcPID);
                
                correctedThrottle = tcResult.correctedThrottle;
                state.Electronics.TCActive = tcResult.tcActive;
                state.Electronics.TCReduction = tcResult.tcReduction;
            }
            
            // ABS
            if (config.absEnabled && config.absLevel > 0f && state.BrakeInput > 0.01f)
            {
                float[] slipRatios = new float[4];
                for (int i = 0; i < 4; i++)
                {
                    slipRatios[i] = state.Wheels[i].SlipRatio;
                }
                
                var absPIDs = new Electronics.PIDController[4];
                for (int i = 0; i < 4; i++)
                {
                    absPIDs[i] = new Electronics.PIDController(config.absKp, config.absKi, config.absKd);
                }
                
                var absResult = Electronics.ABSSystem.Calculate(
                    slipRatios, state.BrakeInput, config.brakeMaxPressure,
                    config.absLevel, config, ref absPIDs);
                
                brakePressures = absResult.pressures;
                state.Electronics.ABSActive = absResult.absActive;
                state.Electronics.ABSReduction = absResult.absReduction;
            }
            
            // ============================================================
            // STEP 2: Drivetrain Update
            // Calculate engine torque, gear shifts, drive torque to wheels
            // ============================================================
            
            Drivetrain.DrivetrainSystem.Update(
                ref state.Drivetrain,
                correctedThrottle,
                state.ClutchInput,
                state.BrakeInput,
                dt,
                config,
                state.Damage.EngineHealth,
                state.Thermal.EngineTemp,
                state.Thermal.OilTemp);
            
            // Apply drivetrain wheel speeds
            state.Wheels[0].AngularVelocity = state.Drivetrain.WheelOmegaFL;
            state.Wheels[1].AngularVelocity = state.Drivetrain.WheelOmegaFR;
            state.Wheels[2].AngularVelocity = state.Drivetrain.WheelOmegaRL;
            state.Wheels[3].AngularVelocity = state.Drivetrain.WheelOmegaRR;
            
            // ============================================================
            // STEP 3: Suspension Kinematics
            // Calculate camber, toe, ride height for each wheel
            // ============================================================
            
            for (int i = 0; i < 4; i++)
            {
                // Get suspension travel from wheel position relative to body
                float suspensionTravel = CalculateSuspensionTravel(state, i, config);
                
                // Calculate kinematics
                float maxSteerAngle = 0.5f; // ~28.6 degrees default max steer
                var kinematics = Physics.SuspensionSystem.CalculateKinematics(
                    suspensionTravel,
                    state.SteeringInput,
                    maxSteerAngle,
                    config.camberGain,
                    config.toeChangePerTravel,
                    config.bumpSteerGain,
                    0f,
                    0f);

                state.Wheels[i].CamberAngle = kinematics.camber;
                state.Wheels[i].ToeAngle = kinematics.toe;
                state.Wheels[i].RideHeight = config.suspensionMaxTravel - suspensionTravel;
            }
            
            // ============================================================
            // STEP 4: Tire Forces (Pacejka Magic Formula)
            // Calculate Fx, Fy, Fz, Mz for each wheel
            // ============================================================
            
            for (int i = 0; i < 4; i++)
            {
                // Get grip multiplier from thermal model
                float gripMultiplier = GetTireGripMultiplier(state, i, config);
                
                // Calculate tire forces
                var forces = Physics.PacejkaTireModel.CalculateCombinedForces(
                    state.Wheels[i].SlipAngle,
                    state.Wheels[i].SlipRatio,
                    state.Wheels[i].VerticalLoad,
                    state.Wheels[i].CamberAngle,
                    gripMultiplier,
                    config.tireCoefficients);
                
                state.Wheels[i].LongitudinalForce = forces.Fx;
                state.Wheels[i].LateralForce = forces.Fy;
                state.Wheels[i].AligningTorque = forces.Mz;
                
                // Apply brake force if braking
                if (state.BrakeInput > 0.01f && !state.Electronics.ABSActive)
                {
                    float brakeForce = CalculateBrakeForce(
                        state.BrakeInput, i, config, state.Thermal);
                    state.Wheels[i].LongitudinalForce -= brakeForce;
                }
                else if (state.Electronics.ABSActive)
                {
                    float brakeForce = CalculateBrakeForce(
                        brakePressures[i] / config.brakeMaxPressure, 
                        i, config, state.Thermal);
                    state.Wheels[i].LongitudinalForce -= brakeForce;
                }
                
                // Apply drive torque
                float driveTorque = GetDriveTorqueForWheel(state.Drivetrain, i, config);
                float driveForce = driveTorque / config.wheelRadius;
                state.Wheels[i].LongitudinalForce += driveForce;
            }
            
            // ============================================================
            // STEP 5: Aerodynamics
            // Calculate drag, downforce, side force
            // ============================================================
            
            float rideHeightFront = AverageRideHeight(state, true);
            float rideHeightRear = AverageRideHeight(state, false);
            
            var aeroResult = Aero.AeroSim.CalculateAeroForces(
                state.Velocity,
                windVelocity,
                state.Rotation,
                rideHeightFront,
                rideHeightRear,
                state.DRSRequested,
                0f, // wingFrontAngle (would come from setup)
                0f, // wingRearAngle
                config);
            
            state.Aero.ForceWorld = aeroResult.force;
            state.Aero.TorqueWorld = aeroResult.torque;
            
            // ============================================================
            // STEP 6: Sum Forces and Torques
            // Combine all forces acting on the vehicle body
            // ============================================================
            
            float3 totalForce = new float3(0f, -config.totalMass * 9.81f, 0f); // Gravity
            float3 totalTorque = new float3(0f);
            
            // Add wheel forces (transformed to world space)
            for (int i = 0; i < 4; i++)
            {
                float3 wheelForceLocal = new float3(
                    state.Wheels[i].LongitudinalForce,
                    0f,
                    state.Wheels[i].LateralForce);
                
                float3 wheelForceWorld = math.mul(state.Rotation, wheelForceLocal);
                totalForce += wheelForceWorld;
                
                // Torque from wheel forces
                float3 wheelPosLocal = GetWheelPositionLocal(i, config);
                float3 torque = math.cross(wheelPosLocal, wheelForceLocal);
                totalTorque += math.mul(state.Rotation, torque);
            }
            
            // Add aerodynamic forces
            totalForce += state.Aero.ForceWorld;
            totalTorque += state.Aero.TorqueWorld;
            
            // ============================================================
            // STEP 7: Rigid Body Integration
            // Integrate acceleration → velocity → position
            // Using semi-implicit Euler integration
            // ============================================================
            
            // Linear acceleration: a = F / m
            state.Acceleration = totalForce / config.totalMass;
            
            // Update velocity
            state.Velocity += state.Acceleration * dt;
            
            // Apply damping (rolling resistance, drivetrain losses)
            state.Velocity *= (1f - config.rollingResistanceCoefficient * dt);
            
            // Update position
            state.Position += state.Velocity * dt;
            
            // Angular acceleration: α = τ / I
            float3 momentOfInertia = new float3(config.momentOfInertiaX, config.momentOfInertiaY, config.momentOfInertiaZ);
            float3 angularAccel = new float3(
                totalTorque.x / momentOfInertia.x,
                totalTorque.y / momentOfInertia.y,
                totalTorque.z / momentOfInertia.z);
            
            // Update angular velocity
            state.AngularVelocity += angularAccel * dt;
            
            // Apply angular damping
            state.AngularVelocity *= (1f - config.angularDampingCoefficient * dt);
            
            // Update rotation using proper quaternion integration
            // dq/dt = 0.5 * q * ω (as quaternion multiplication)
            float mag = math.length(state.AngularVelocity);
            if (mag > 1e-6f)
            {
                float halfAngle = mag * dt * 0.5f;
                float sinHalfAngle = math.sin(halfAngle);
                quaternion deltaRot = new quaternion(
                    math.cos(halfAngle),
                    state.AngularVelocity.x * sinHalfAngle / mag,
                    state.AngularVelocity.y * sinHalfAngle / mag,
                    state.AngularVelocity.z * sinHalfAngle / mag
                );
                state.Rotation = math.normalize(math.mul(state.Rotation, deltaRot));
            }
            
            // ============================================================
            // STEP 8: Thermal Updates
            // Update tire, brake, and engine temperatures
            // ============================================================
            
            for (int i = 0; i < 4; i++)
            {
                bool isContacting = state.Wheels[i].VerticalLoad > 100f;
                
                var tireTemps = Thermal.TireThermalModel.UpdateTemperatures(
                    GetTireTempInner(state, i),
                    GetTireTempMiddle(state, i),
                    GetTireTempOuter(state, i),
                    ambientTemp,
                    trackTemp,
                    state.Wheels[i].VerticalLoad,
                    state.Wheels[i].SlipRatio,
                    state.Wheels[i].SlipAngle,
                    state.Wheels[i].AngularVelocity,
                    math.length(state.Velocity),
                    state.Wheels[i].Pressure,
                    GetTireWear(state, i),
                    isContacting,
                    dt,
                    config);
                
                SetTireTemps(ref state, i, tireTemps.tempInner, tireTemps.tempMiddle, tireTemps.tempOuter);
                
                // Brake temperature
                float brakeTorque = math.abs(state.Wheels[i].LongitudinalForce) * config.wheelRadius;
                float newBrakeTemp = Thermal.BrakeThermalModel.UpdateTemperature(
                    GetBrakeTemp(state, i),
                    ambientTemp,
                    brakeTorque,
                    state.Wheels[i].AngularVelocity,
                    math.length(state.Velocity),
                    GetBrakeWear(state, i),
                    state.BrakeInput > 0.01f,
                    dt,
                    config);
                
                SetBrakeTemp(ref state, i, newBrakeTemp);
            }
            
            // Engine thermal
            var engineTemps = Thermal.EngineThermalModel.UpdateTemperatures(
                state.Thermal.EngineTemp,
                state.Thermal.OilTemp,
                ambientTemp,
                state.Thermal.CoolantTemp,
                state.Drivetrain.EngineRPM,
                correctedThrottle,
                math.length(state.Velocity),
                1f, // radiatorFanSpeed
                dt,
                config);
            
            state.Thermal.EngineTemp = engineTemps.engineTemp;
            state.Thermal.OilTemp = engineTemps.oilTemp;
            
            // ============================================================
            // STEP 9: Damage Updates
            // Check and update component damage
            // ============================================================
            
            // Engine damage
            state.Damage.EngineHealth = Damage.MechanicalDamageSystem.UpdateEngineDamage(
                state.Damage.EngineHealth,
                state.Drivetrain.EngineRPM,
                state.Thermal.EngineTemp,
                state.Thermal.OilTemp,
                correctedThrottle,
                dt,
                config,
                damageConfig,
                out state.Damage.EngineMisfiring);
            
            // Tire wear
            for (int i = 0; i < 4; i++)
            {
                float distanceTraveled = math.length(state.Velocity) * dt;
                bool isLocked = math.abs(state.Wheels[i].SlipRatio) > 0.9f;
                
                var tireResult = Damage.MechanicalDamageSystem.UpdateTireDamage(
                    GetTireWear(state, i),
                    GetTireTempMiddle(state, i),
                    state.Wheels[i].VerticalLoad,
                    state.Wheels[i].SlipRatio,
                    state.Wheels[i].SlipAngle,
                    distanceTraveled,
                    isLocked,
                    damageConfig);
                
                SetTireWear(ref state, i, tireResult.wear);
                SetTirePunctured(ref state, i, tireResult.punctured);
            }
            
            // ============================================================
            // STEP 10: Update Timestamp
            // ============================================================
            
            state.Timestamp += dt;
            state.FrameIndex++;
        }

        // Helper methods
        
        [BurstCompile]
        private static int[] GetDrivenWheels(DrivetrainType type)
        {
            switch (type)
            {
                case DrivetrainType.FWD: return new[] { 0, 1 };
                case DrivetrainType.RWD: return new[] { 2, 3 };
                case DrivetrainType.AWD: return new[] { 0, 1, 2, 3 };
                default: return new[] { 2, 3 };
            }
        }
        
        /// <summary>
        /// Calculate suspension travel based on wheel position relative to body
        /// Uses raycast-like approach from wheel hub to ground plane
        /// </summary>
        [BurstCompile]
        private static float CalculateSuspensionTravel(SimulationState state, int wheelIndex, in VehicleConfig config)
        {
            // Get wheel position in local space
            float3 wheelPosLocal = GetWheelPositionLocal(wheelIndex, config);
            
            // Transform to world space
            float3 wheelPosWorld = math.mul(state.Rotation, wheelPosLocal) + state.Position;
            
            // Simple ground plane assumption (y = 0)
            // In real implementation, would use track height map
            float groundHeight = 0f;
            
            // Calculate distance from wheel hub to ground
            float rideHeight = wheelPosWorld.y - groundHeight;
            
            // Suspension travel = nominal ride height - current ride height
            float nominalRideHeight = 0.08f; // 80mm static ride height
            float travel = nominalRideHeight - rideHeight;
            
            // Clamp to physical limits (±80mm travel)
            return math.clamp(travel, -0.08f, 0.08f);
        }
        
        /// <summary>
        /// Calculate tire grip multiplier based on temperature and wear
        /// Optimal temperature range: 90-110°C for slick tires
        /// </summary>
        [BurstCompile]
        private static float GetTireGripMultiplier(SimulationState state, int index, in VehicleConfig config)
        {
            // Get average tire temperature for this wheel
            float tempInner, tempMiddle, tempOuter;
            GetTireTemps(state, index, out tempInner, out tempMiddle, out tempOuter);
            
            float avgTemp = (tempInner + tempMiddle + tempOuter) / 3f;
            
            // Temperature grip curve (optimal at 100°C)
            float optimalTemp = 100f;
            float tempRange = 40f;
            float tempMultiplier = math.exp(-math.pow((avgTemp - optimalTemp) / tempRange, 2));
            
            // Wear multiplier (linear degradation)
            float wear = GetTireWear(state, index);
            float wearMultiplier = 1f - (wear * 0.3f); // Max 30% loss at full wear
            
            // Pressure multiplier (optimal pressure ~200 kPa)
            float pressure = GetTirePressure(state, index);
            float optimalPressure = 200f;
            float pressureMultiplier = 1f - math.abs(pressure - optimalPressure) / 100f;
            pressureMultiplier = math.max(pressureMultiplier, 0.7f);
            
            return tempMultiplier * wearMultiplier * pressureMultiplier;
        }
        
        [BurstCompile]
        private static float GetTireTempInner(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Thermal.TireTempFL_Inner;
                case 1: return state.Thermal.TireTempFR_Inner;
                case 2: return state.Thermal.TireTempRL_Inner;
                case 3: return state.Thermal.TireTempRR_Inner;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static float GetTireTempMiddle(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Thermal.TireTempFL_Middle;
                case 1: return state.Thermal.TireTempFR_Middle;
                case 2: return state.Thermal.TireTempRL_Middle;
                case 3: return state.Thermal.TireTempRR_Middle;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static float GetTireTempOuter(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Thermal.TireTempFL_Outer;
                case 1: return state.Thermal.TireTempFR_Outer;
                case 2: return state.Thermal.TireTempRL_Outer;
                case 3: return state.Thermal.TireTempRR_Outer;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static void GetTireTemps(SimulationState state, int index, out float inner, out float middle, out float outer)
        {
            inner = GetTireTempInner(state, index);
            middle = GetTireTempMiddle(state, index);
            outer = GetTireTempOuter(state, index);
        }
        
        [BurstCompile]
        private static void SetTireTemps(ref SimulationState state, int index, float inner, float middle, float outer)
        {
            switch (index)
            {
                case 0:
                    state.Thermal.TireTempFL_Inner = inner;
                    state.Thermal.TireTempFL_Middle = middle;
                    state.Thermal.TireTempFL_Outer = outer;
                    break;
                case 1:
                    state.Thermal.TireTempFR_Inner = inner;
                    state.Thermal.TireTempFR_Middle = middle;
                    state.Thermal.TireTempFR_Outer = outer;
                    break;
                case 2:
                    state.Thermal.TireTempRL_Inner = inner;
                    state.Thermal.TireTempRL_Middle = middle;
                    state.Thermal.TireTempRL_Outer = outer;
                    break;
                case 3:
                    state.Thermal.TireTempRR_Inner = inner;
                    state.Thermal.TireTempRR_Middle = middle;
                    state.Thermal.TireTempRR_Outer = outer;
                    break;
            }
        }
        
        [BurstCompile]
        private static float GetBrakeTemp(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Thermal.BrakeTempFL;
                case 1: return state.Thermal.BrakeTempFR;
                case 2: return state.Thermal.BrakeTempRL;
                case 3: return state.Thermal.BrakeTempRR;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static void SetBrakeTemp(ref SimulationState state, int index, float temp)
        {
            switch (index)
            {
                case 0: state.Thermal.BrakeTempFL = temp; break;
                case 1: state.Thermal.BrakeTempFR = temp; break;
                case 2: state.Thermal.BrakeTempRL = temp; break;
                case 3: state.Thermal.BrakeTempRR = temp; break;
            }
        }
        
        [BurstCompile]
        private static float GetTireWear(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Damage.TireWearFL;
                case 1: return state.Damage.TireWearFR;
                case 2: return state.Damage.TireWearRL;
                case 3: return state.Damage.TireWearRR;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static float GetBrakeWear(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Damage.BrakeWearFL;
                case 1: return state.Damage.BrakeWearFR;
                case 2: return state.Damage.BrakeWearRL;
                case 3: return state.Damage.BrakeWearRR;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static float GetTirePressure(SimulationState state, int index)
        {
            switch (index)
            {
                case 0: return state.Wheels[0].Pressure;
                case 1: return state.Wheels[1].Pressure;
                case 2: return state.Wheels[2].Pressure;
                case 3: return state.Wheels[3].Pressure;
                default: return 0f;
            }
        }
        
        [BurstCompile]
        private static void SetTireWear(ref SimulationState state, int index, float wear)
        {
            switch (index)
            {
                case 0: state.Damage.TireWearFL = wear; break;
                case 1: state.Damage.TireWearFR = wear; break;
                case 2: state.Damage.TireWearRL = wear; break;
                case 3: state.Damage.TireWearRR = wear; break;
            }
        }
        
        [BurstCompile]
        private static void SetTirePunctured(ref SimulationState state, int index, bool punctured)
        {
            switch (index)
            {
                case 0: state.Damage.TirePuncturedFL = punctured; break;
                case 1: state.Damage.TirePuncturedFR = punctured; break;
                case 2: state.Damage.TirePuncturedRL = punctured; break;
                case 3: state.Damage.TirePuncturedRR = punctured; break;
            }
        }
        
        /// <summary>
        /// Calculate average ride height for front or rear axle
        /// Used for aerodynamic calculations
        /// </summary>
        [BurstCompile]
        private static float AverageRideHeight(SimulationState state, bool front)
        {
            if (front)
            {
                return (state.Wheels[0].RideHeight + state.Wheels[1].RideHeight) * 0.5f;
            }
            else
            {
                return (state.Wheels[2].RideHeight + state.Wheels[3].RideHeight) * 0.5f;
            }
        }
        
        /// <summary>
        /// Get wheel position in local vehicle space
        /// Coordinate system: X=forward, Y=up, Z=right
        /// </summary>
        [BurstCompile]
        private static float3 GetWheelPositionLocal(int index, in VehicleConfig config)
        {
            float halfFrontTrack = config.frontTrackWidth * 0.5f;
            float halfRearTrack = config.rearTrackWidth * 0.5f;
            
            switch (index)
            {
                case 0: // Front Left
                    return new float3(config.cogToFrontAxle, -config.cogHeight, -halfFrontTrack);
                case 1: // Front Right
                    return new float3(config.cogToFrontAxle, -config.cogHeight, halfFrontTrack);
                case 2: // Rear Left
                    return new float3(-config.cogToRearAxle, -config.cogHeight, -halfRearTrack);
                case 3: // Rear Right
                    return new float3(-config.cogToRearAxle, -config.cogHeight, halfRearTrack);
                default:
                    return new float3(0f);
            }
        }
        
        /// <summary>
        /// Calculate brake force with thermal fade consideration
        /// Brake fade reduces effectiveness as temperature increases
        /// </summary>
        [BurstCompile]
        private static float CalculateBrakeForce(float brakeInput, int wheelIndex, in VehicleConfig config, in Thermal.ThermalState thermal)
        {
            float brakeTemp = GetBrakeTemp(new SimulationState { Thermal = thermal }, wheelIndex);
            
            // Base brake torque from hydraulic pressure
            float maxPressure = config.brakeMaxPressure;
            float pressure = brakeInput * maxPressure;
            
            // Brake torque = pressure * caliper area * friction * radius
            float caliperArea = wheelIndex < 2 ? config.brakeCaliperAreaFront : config.brakeCaliperAreaRear;
            float discRadius = wheelIndex < 2 ? config.brakeDiscRadiusFront : config.brakeDiscRadiusRear;
            
            // Friction coefficient decreases with temperature (brake fade)
            float muBase = wheelIndex < 2 ? config.brakePadFrictionFront : config.brakePadFrictionRear;
            float fadeTemp = 500f; // Temperature where fade starts
            float muActual = muBase * math.exp(-math.max(0f, brakeTemp - fadeTemp) / 200f);
            
            float brakeTorque = pressure * caliperArea * muActual * discRadius;
            
            // Convert torque to force at contact patch
            float brakeForce = brakeTorque / config.wheelRadius;
            
            return brakeForce;
        }
        
        /// <summary>
        /// Get drive torque distribution to specific wheel based on differential type
        /// </summary>
        [BurstCompile]
        private static float GetDriveTorqueForWheel(Drivetrain.DrivetrainState dt, int wheelIndex, in VehicleConfig config)
        {
            // Get total torque from drivetrain
            float totalTorque = dt.OutputTorque;
            
            // Distribute based on differential type
            switch (config.differentialType)
            {
                case Vehicle.DifferentialType.Open:
                    // Equal torque split
                    if (config.drivetrainType == Vehicle.DrivetrainType.FWD && wheelIndex < 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.RWD && wheelIndex >= 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.AWD)
                        return totalTorque * 0.25f;
                    break;
                    
                case Vehicle.DifferentialType.LimitedSlip:
                    // LSD biases torque to wheel with more grip
                    // Simplified: assume equal distribution for now
                    if (config.drivetrainType == Vehicle.DrivetrainType.FWD && wheelIndex < 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.RWD && wheelIndex >= 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.AWD)
                        return totalTorque * 0.25f;
                    break;
                    
                case Vehicle.DifferentialType.Torsen:
                    // Torsen can bias up to TBR (Torque Bias Ratio)
                    float tbr = config.torsenTBR;
                    // Simplified distribution
                    if (config.drivetrainType == Vehicle.DrivetrainType.FWD && wheelIndex < 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.RWD && wheelIndex >= 2)
                        return totalTorque * 0.5f;
                    else if (config.drivetrainType == Vehicle.DrivetrainType.AWD)
                        return totalTorque * 0.25f;
                    break;
            }
            
            return 0f;
        }
    }

    /// <summary>
    /// MonoBehaviour wrapper to integrate simulation with Unity
    /// Attach this to your vehicle GameObject
    /// </summary>
    public class SimulationController : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [Tooltip("Fixed timestep for physics (1/240 = 0.00416667)")]
        public float fixedTimestep = 1f / 240f;
        
        [Header("References")]
        public VehicleConfig vehicleConfig;
        public DamageConfig damageConfig;
        public Transform carTransform;
        
        // Simulation state
        private SimulationLoop.SimulationState simState;
        private double simulationTime;
        private int frameIndex;
        
        // Environment
        private float3 windVelocity = new float3(0f);
        private float ambientTemp = 25f;
        private float trackTemp = 35f;
        
        void Start()
        {
            // Initialize simulation state
            simState.Position = carTransform.position;
            simState.Rotation = carTransform.rotation;
            simState.Timestamp = 0;
            simState.FrameIndex = 0;
            
            // Set Unity fixed timestep
            Time.fixedDeltaTime = fixedTimestep;
        }
        
        void FixedUpdate()
        {
            // Read input
            simState.ThrottleInput = Input.GetAxis("Vertical") > 0 ? Input.GetAxis("Vertical") : 0f;
            simState.BrakeInput = Input.GetAxis("Vertical") < 0 ? -Input.GetAxis("Vertical") : 0f;
            simState.SteeringInput = Input.GetAxis("Horizontal");
            simState.ClutchInput = Input.GetKey(KeyCode.LeftShift) ? 0f : 1f;
            
            // Run simulation
            SimulationLoop.Update(
                ref simState,
                Time.fixedDeltaTime,
                vehicleConfig,
                damageConfig,
                default, // track normals
                windVelocity,
                ambientTemp,
                trackTemp);
            
            // Update transform from simulation
            carTransform.position = simState.Position;
            carTransform.rotation = simState.Rotation;
        }
        
        void OnRenderObject()
        {
            // Could render debug info here
        }
    }
}