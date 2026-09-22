using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Coordinate system conventions for the racing simulation.
    /// All modules MUST use these conventions to ensure consistent sign behavior.
    /// 
    /// Unity World Space:
    ///   X = right, Y = up, Z = forward
    /// 
    /// Vehicle Local Space (SAE J670 adapted):
    ///   X = forward (longitudinal)
    ///   Y = left (lateral, driver's left)
    ///   Z = up (vertical)
    ///   
    ///   Note: This is DIFFERENT from Unity's world space.
    ///   Transform between them via quaternion rotation.
    /// 
    /// Tire Coordinate System (SAE J670):
    ///   X_tire = forward (rolling direction)
    ///   Y_tire = left (driver's left)
    ///   Z_tire = up (from road surface)
    ///   
    ///   Fx = longitudinal force (positive = forward/driving)
    ///   Fy = lateral force (positive = left)
    ///   Fz = vertical force (positive = up/compression)
    ///   Mz = aligning torque (positive = CCW around Z)
    ///   
    /// Sign Conventions:
    ///   Slip angle: positive = tire pointing left of velocity vector
    ///   Slip ratio: positive = braking (locked), negative = spinning
    ///   Camber: positive = top of tire tilted inward (negative camber)
    ///   Toe: positive = toe-in (front of tire pointing inward)
    ///   Steering: positive = left turn
    ///   Yaw rate: positive = CCW (looking from above)
    ///   Roll angle: positive = body rolling left (right side up)
    ///   Pitch: positive = nose up (squat)
    /// </summary>
    public static class CoordinateSystem
    {
        // Unity world space directions
        public static readonly float3 WorldForward = new float3(0, 0, 1);
        public static readonly float3 WorldRight = new float3(1, 0, 0);
        public static readonly float3 WorldUp = new float3(0, 1, 0);

        // Vehicle local space directions (SAE)
        public static readonly float3 VehicleForward = new float3(1, 0, 0);
        public static readonly float3 VehicleLeft = new float3(0, 1, 0);
        public static readonly float3 VehicleUp = new float3(0, 0, 1);

        // Conversion: Vehicle local → Unity local
        // Vehicle X (forward) → Unity Z (forward)
        // Vehicle Y (left) → Unity X (right, negated)
        // Vehicle Z (up) → Unity Y (up)
        public static float3 VehicleToUnityLocal(float3 vehicleVec)
        {
            return new float3(-vehicleVec.y, vehicleVec.z, vehicleVec.x);
        }

        // Conversion: Unity local → Vehicle local
        public static float3 UnityToVehicleLocal(float3 unityVec)
        {
            return new float3(unityVec.z, -unityVec.x, unityVec.y);
        }

        // Conversion: Vehicle local → World space given vehicle rotation
        public static float3 VehicleToWorld(float3 vehicleVec, quaternion vehicleRotation)
        {
            return math.mul(vehicleRotation, VehicleToUnityLocal(vehicleVec));
        }

        // Conversion: World space → Vehicle local given vehicle rotation
        public static float3 WorldToVehicle(float3 worldVec, quaternion vehicleRotation)
        {
            return UnityToVehicleLocal(math.mul(math.inverse(vehicleRotation), worldVec));
        }

        /// <summary>
        /// Calculate slip angle using SAE convention.
        /// Positive = tire pointing left of velocity vector.
        /// </summary>
        public static float CalculateSlipAngle(
            float3 tireForward,
            float3 tireVelocity,
            float steerAngle)
        {
            float velX = math.dot(tireVelocity, tireForward);
            float velY = math.dot(tireVelocity, math.cross(math.up(), tireForward));

            if (math.abs(velX) < 0.1f)
                return 0f;

            float slipAngle = math.atan2(velY, math.abs(velX));
            return slipAngle + math.radians(steerAngle);
        }

        /// <summary>
        /// Calculate slip ratio using SAE convention.
        /// Positive = braking (wheel locked), negative = spinning (wheel faster than road).
        /// </summary>
        public static float CalculateSlipRatio(
            float wheelAngularVelocity,
            float wheelRadius,
            float forwardVelocity)
        {
            float wheelSpeed = wheelAngularVelocity * wheelRadius;
            float refSpeed = math.max(math.abs(forwardVelocity), 1.0f);

            return (wheelSpeed - forwardVelocity) / refSpeed;
        }
    }
}
