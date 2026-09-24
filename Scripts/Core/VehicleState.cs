using Unity.Mathematics;

namespace RacingSim.Core
{
    /// <summary>
    /// Shared vehicle state for cross-system communication.
    /// Used by SimulationLoop, ChassisRigidBody, and all subsystems.
    /// </summary>
    public struct VehicleState
    {
        // Chassis
        public float3 Position;
        public quaternion Rotation;
        public float3 Velocity;
        public float3 AngularVelocity;

        // Per-wheel (4 elements: FL=0, FR=1, RL=2, RR=3)
        public float TravelFL;
        public float TravelFR;
        public float TravelRL;
        public float TravelRR;

        public float VerticalLoadFL;
        public float VerticalLoadFR;
        public float VerticalLoadRL;
        public float VerticalLoadRR;

        public float SlipRatioFL;
        public float SlipRatioFR;
        public float SlipRatioRL;
        public float SlipRatioRR;

        public float SlipAngleFL;
        public float SlipAngleFR;
        public float SlipAngleRL;
        public float SlipAngleRR;

        public bool IsGroundedFL;
        public bool IsGroundedFR;
        public bool IsGroundedRL;
        public bool IsGroundedRR;

        // Drivetrain
        public float EngineRPM;
        public int CurrentGear;
        public float ClutchPosition;

        // Input
        public float Steering;
        public float Throttle;
        public float Brake;
        public float Clutch;

        // Aero
        public float Downforce;
        public float Drag;

        // Helpers
        public float GetTravel(int wheel) => wheel switch
        {
            0 => TravelFL,
            1 => TravelFR,
            2 => TravelRL,
            3 => TravelRR,
            _ => 0f
        };

        public float GetVerticalLoad(int wheel) => wheel switch
        {
            0 => VerticalLoadFL,
            1 => VerticalLoadFR,
            2 => VerticalLoadRL,
            3 => VerticalLoadRR,
            _ => 0f
        };

        public bool GetIsGrounded(int wheel) => wheel switch
        {
            0 => IsGroundedFL,
            1 => IsGroundedFR,
            2 => IsGroundedRL,
            3 => IsGroundedRR,
            _ => false
        };
    }
}
