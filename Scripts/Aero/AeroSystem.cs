using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using RacingSim.Vehicle;

namespace RacingSim.Aero
{
    /// <summary>
    /// [OBSOLETE] Use AdaptiveAeroSystem.cs instead.
    /// This file is retained for reference only. It uses a different type
    /// system (AeroMapPoint, AeroCoefficients) that is incompatible with
    /// the current AeroDataStructures.cs.
    /// 
    /// Replacement: AdaptiveAeroSystem.cs with 5-tier GPU-adaptive aero.
    /// </summary>
    [System.Obsolete("Use AdaptiveAeroSystem instead")]
    public static class AeroSystem_OLD
    {
        // Intentionally empty - legacy code preserved in git history
    }

    /// <summary>
    /// Aerodynamic coefficients lookup from aero map
    /// Stores Cd, Cl_front, Cl_rear, Cs for interpolation
    /// </summary>
    [System.Serializable]
    public struct AeroMapPoint
    {
        public float yawAngle;      // degrees (-180 to 180)
        public float rideHeightF;   // mm (front)
        public float rideHeightR;   // mm (rear)
        public float Cd;            // Drag coefficient
        public float ClFront;       // Lift coefficient front (negative = downforce)
        public float ClRear;        // Lift coefficient rear (negative = downforce)
        public float Cs;            // Side force coefficient
    }

    /// <summary>
    /// Aerodynamics simulation with drag, downforce, and side forces
    /// Implements: F = 0.5 × ρ × V² × C × A
    /// </summary>
    [BurstCompile]
    public static class AeroSim
    {
        /// <summary>
        /// Air density at sea level (kg/m³)
        /// </summary>
        public const float AIR_DENSITY_SEA_LEVEL = 1.225f;

        /// <summary>
        /// Calculate aerodynamic forces on vehicle
        /// </summary>
        [BurstCompile]
        public static (float3 force, float3 torque) CalculateAeroForces(
            float3 velocity,        // World space velocity
            float3 windVelocity,    // World space wind
            quaternion rotation,    // Vehicle rotation
            float rideHeightFront,  // meters
            float rideHeightRear,   // meters
            bool drsActive,
            float wingFrontAngle,
            float wingRearAngle,
            in VehicleConfig config)
        {
            // 1. Calculate relative velocity (vehicle - wind)
            float3 relVelocity = velocity - windVelocity;
            
            // 2. Transform to vehicle local space
            float3 localVel = math.mul(math.inverse(rotation), relVelocity);
            
            // 3. Calculate speed magnitude and yaw angle
            float speedMag = math.length(relVelocity);
            float yawAngle = math.degrees(math.atan2(localVel.z, localVel.x));
            
            // Clamp yaw angle to -180..180
            if (yawAngle > 180f) yawAngle -= 360f;
            if (yawAngle < -180f) yawAngle += 360f;
            
            // 4. Lookup aero coefficients from map
            AeroCoefficients coeffs = LookupAeroCoefficients(
                yawAngle, 
                rideHeightFront * 1000f, // Convert to mm
                rideHeightRear * 1000f,
                config);
            
            // 5. Apply wing angle adjustments
            coeffs = ApplyWingAdjustments(coeffs, wingFrontAngle, wingRearAngle, config);
            
            // 6. Apply DRS if active
            if (drsActive)
            {
                coeffs.Cd *= config.drsCdMultiplier;
                coeffs.ClRear *= config.drsClMultiplier;
            }
            
            // 7. Calculate dynamic pressure: q = 0.5 × ρ × V²
            float airDensity = GetAirDensity(config.altitude);
            float dynamicPressure = 0.5f * airDensity * speedMag * speedMag;
            
            // 8. Calculate forces in local space
            // Drag force (opposite to velocity direction)
            float dragForce = dynamicPressure * coeffs.Cd * config.frontalArea;
            
            // Downforce (negative Y in local space)
            float downforceFront = dynamicPressure * coeffs.ClFront * config.planformAreaFront;
            float downforceRear = dynamicPressure * coeffs.ClRear * config.planformAreaRear;
            
            // Side force (lateral)
            float sideForce = dynamicPressure * coeffs.Cs * config.sideArea;
            
            // 9. Create force vector in local space
            // Drag acts opposite to X velocity
            float3 localForce = new float3(
                -math.sign(localVel.x) * dragForce,  // Drag
                downforceFront + downforceRear,       // Downforce (positive Y = down in our convention)
                -math.sign(localVel.z) * sideForce    // Side force
            );
            
            // 10. Transform force back to world space
            float3 worldForce = math.mul(rotation, localForce);
            
            // 11. Calculate aerodynamic torque (from drag/downforce acting at CoP)
            float3 copOffset = new float3(
                config.aeroCenterOfPressureX,  // Distance from CoG
                config.aeroCenterOfPressureY,
                config.aeroCenterOfPressureZ
            );
            
            // Torque = r × F
            float3 torque = math.cross(copOffset, localForce);
            float3 worldTorque = math.mul(rotation, torque);
            
            return (worldForce, worldTorque);
        }

        /// <summary>
        /// Lookup aero coefficients using bilinear interpolation
        /// </summary>
        [BurstCompile]
        private static AeroCoefficients LookupAeroCoefficients(
            float yawAngle,
            float rideHeightF,
            float rideHeightR,
            in VehicleConfig config)
        {
            // Simplified: use base values if no aero map
            if (config.aeroMap == null || config.aeroMap.Length == 0)
            {
                return new AeroCoefficients
                {
                    Cd = config.cdBase,
                    ClFront = config.clFrontBase,
                    ClRear = config.clRearBase,
                    Cs = config.csBase
                };
            }
            
            // Find surrounding points for trilinear interpolation
            // This is simplified - full implementation would search the 3D grid
            
            // For now, use yaw-based interpolation only
            AeroCoefficients result = InterpolateYawOnly(yawAngle, config);
            
            // Apply ride height sensitivity
            float rhSensitivityFront = config.rideHeightSensitivityFront;
            float rhSensitivityRear = config.rideHeightSensitivityRear;
            
            float nominalRH = 50f; // 50mm nominal ride height
            float deltaRF = (nominalRH - rideHeightF) / 1000f; // Convert to meters
            float deltaRR = (nominalRH - rideHeightR) / 1000f;
            
            result.ClFront += deltaRF * rhSensitivityFront;
            result.ClRear += deltaRR * rhSensitivityRear;
            
            // Ground effect on drag
            result.Cd += (deltaRF + deltaRR) * 0.5f * config.rideHeightDragSensitivity;
            
            return result;
        }

        /// <summary>
        /// Interpolate coefficients based on yaw angle only
        /// </summary>
        [BurstCompile]
        private static AeroCoefficients InterpolateYawOnly(float yawAngle, in VehicleConfig config)
        {
            if (config.aeroMap == null || config.aeroMap.Length == 0)
            {
                return new AeroCoefficients
                {
                    Cd = config.cdBase,
                    ClFront = config.clFrontBase,
                    ClRear = config.clRearBase,
                    Cs = config.csBase
                };
            }
            
            // Normalize yaw to 0-180 (symmetry assumption)
            float absYaw = math.abs(yawAngle);
            
            // Find two surrounding points
            AeroMapPoint lower = config.aeroMap[0];
            AeroMapPoint upper = config.aeroMap[config.aeroMap.Length - 1];
            
            for (int i = 0; i < config.aeroMap.Length - 1; i++)
            {
                if (absYaw >= config.aeroMap[i].yawAngle && 
                    absYaw <= config.aeroMap[i + 1].yawAngle)
                {
                    lower = config.aeroMap[i];
                    upper = config.aeroMap[i + 1];
                    break;
                }
            }
            
            // Linear interpolation
            float t = 0f;
            if (upper.yawAngle != lower.yawAngle)
            {
                t = (absYaw - lower.yawAngle) / (upper.yawAngle - lower.yawAngle);
            }
            
            return new AeroCoefficients
            {
                Cd = math.lerp(lower.Cd, upper.Cd, t),
                ClFront = math.lerp(lower.ClFront, upper.ClFront, t),
                ClRear = math.lerp(lower.ClRear, upper.ClRear, t),
                Cs = math.lerp(lower.Cs, upper.Cs, t) * math.sign(yawAngle)
            };
        }

        /// <summary>
        /// Apply wing angle adjustments to aero coefficients
        /// </summary>
        [BurstCompile]
        private static AeroCoefficients ApplyWingAdjustments(
            AeroCoefficients coeffs,
            float wingFrontAngle,
            float wingRearAngle,
            in VehicleConfig config)
        {
            // Front wing adjustment
            coeffs.ClFront *= (1f + wingFrontAngle * config.wingFrontLiftGain);
            coeffs.Cd += wingFrontAngle * config.wingFrontDragGain;
            
            // Rear wing adjustment
            coeffs.ClRear *= (1f + wingRearAngle * config.wingRearLiftGain);
            coeffs.Cd += wingRearAngle * config.wingRearDragGain;
            
            return coeffs;
        }

        /// <summary>
        /// Get air density based on altitude
        /// ρ = ρ₀ × (1 - L×h/T₀)^(g×M/(R×L))
        /// Simplified: linear approximation
        /// </summary>
        [BurstCompile]
        private static float GetAirDensity(float altitude)
        {
            // Simplified barometric formula
            // Density decreases ~1% per 100m altitude
            float densityRatio = 1f - (altitude / 10000f); // ~10% per km
            return AIR_DENSITY_SEA_LEVEL * math.max(0.5f, densityRatio);
        }

        /// <summary>
        /// Calculate individual wheel aero effects (for open-wheel cars)
        /// </summary>
        [BurstCompile]
        public static float3 CalculateWheelDrag(
            float3 wheelPosition,
            float3 velocity,
            float wheelAngularVelocity,
            in VehicleConfig config)
        {
            // Wheel drag coefficient (higher than body due to rotation)
            float wheelCd = config.wheelDragCoefficient;
            
            // Rotational drag increase
            float rotationalFactor = 1f + 0.1f * math.abs(wheelAngularVelocity) / 100f;
            
            // Effective area of wheel
            float wheelArea = math.PI * config.wheelRadius * config.wheelRadius * 0.75f;
            
            // Local velocity at wheel position
            float speedSq = math.lengthsq(velocity);
            
            // Drag force
            float dragForce = 0.5f * AIR_DENSITY_SEA_LEVEL * speedSq * wheelCd * wheelArea * rotationalFactor;
            
            // Direction opposite to velocity
            float3 dragDir = -math.normalize(velocity);
            
            return dragDir * dragForce;
        }
    }

    /// <summary>
    /// Aerodynamic coefficients container
    /// </summary>
    public struct AeroCoefficients
    {
        public float Cd;        // Drag coefficient
        public float ClFront;   // Lift coefficient front
        public float ClRear;    // Lift coefficient rear
        public float Cs;        // Side force coefficient
    }

    /// <summary>
    /// Complete aerodynamics state
    /// </summary>
    public struct AeroState
    {
        public float DragForce;
        public float DownforceFront;
        public float DownforceRear;
        public float SideForce;
        public float YawAngle;
        public float SlipAngle; // Vehicle sideslip angle (β)
        
        public float3 ForceLocal;
        public float3 ForceWorld;
        public float3 TorqueLocal;
        public float3 TorqueWorld;
        
        public float DynamicPressure;
        public float AirDensity;
    }
}
