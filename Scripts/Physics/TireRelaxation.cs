using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Tire transient response model using relaxation length.
    /// 
    /// Real tires don't respond instantly to slip changes.
    /// The tire force builds up over a characteristic time constant:
    /// 
    ///   τ = relaxation_length / |Vx|
    ///   
    ///   relaxation_length ≈ 0.2 - 0.4 m for racing tires
    ///   
    ///   At 100 km/h: τ ≈ 0.3/27.8 ≈ 11 ms
    ///   At 300 km/h: τ ≈ 0.3/83.3 ≈ 4 ms
    /// 
    /// Implementation: First-order exponential smoothing
    ///   F_actual = lerp(F_actual, F_steady, α)
    ///   α = dt / (τ + dt)
    /// 
    /// This prevents "twitchy" tire behavior and creates
    /// the smooth grip build-up felt in real racing.
    /// </summary>
    [BurstCompile]
    public struct TireRelaxation
    {
        public float RelaxationLength;
        public float3 ForceSmoothed;
        public float TorqueSmoothed;

        public static TireRelaxation Create(float relaxationLength = 0.3f)
        {
            return new TireRelaxation
            {
                RelaxationLength = relaxationLength,
                ForceSmoothed = float3.zero,
                TorqueSmoothed = 0f
            };
        }

        [BurstCompile]
        public void Update(
            float3 steadyStateForce,
            float steadyStateTorque,
            float forwardSpeed,
            float dt)
        {
            float absSpeed = math.abs(forwardSpeed);
            float tau = RelaxationLength / math.max(absSpeed, 0.1f);
            float alpha = dt / (tau + dt);

            alpha = math.clamp(alpha, 0f, 1f);

            ForceSmoothed = math.lerp(ForceSmoothed, steadyStateForce, alpha);
            TorqueSmoothed = math.lerp(TorqueSmoothed, steadyStateTorque, alpha);
        }

        [BurstCompile]
        public void Reset()
        {
            ForceSmoothed = float3.zero;
            TorqueSmoothed = 0f;
        }
    }

    /// <summary>
    /// Per-wheel tire relaxation state.
    /// Stores smoothed Fx, Fy, Fz, Mz separately for independent relaxation.
    /// </summary>
    [BurstCompile]
    public struct WheelTireRelaxation
    {
        public TireRelaxation Lateral;
        public TireRelaxation Longitudinal;
        public TireRelaxation Aligning;

        public static WheelTireRelaxation Create(float relaxationLength = 0.3f)
        {
            return new WheelTireRelaxation
            {
                Lateral = TireRelaxation.Create(relaxationLength),
                Longitudinal = TireRelaxation.Create(relaxationLength),
                Aligning = TireRelaxation.Create(relaxationLength)
            };
        }

        [BurstCompile]
        public void Update(
            float3 steadyStateForce,
            float steadyStateTorque,
            float forwardSpeed,
            float dt)
        {
            Lateral.Update(
                new float3(0, steadyStateForce.y, 0),
                0f, forwardSpeed, dt);

            Longitudinal.Update(
                new float3(steadyStateForce.x, 0, 0),
                0f, forwardSpeed, dt);

            Aligning.Update(
                float3.zero,
                steadyStateTorque, forwardSpeed, dt);
        }

        [BurstCompile]
        public float3 GetTotalForce()
        {
            return new float3(
                Longitudinal.ForceSmoothed.x,
                Lateral.ForceSmoothed.y,
                0f);
        }

        [BurstCompile]
        public float GetAligningTorque()
        {
            return Aligning.TorqueSmoothed;
        }

        [BurstCompile]
        public void Reset()
        {
            Lateral.Reset();
            Longitudinal.Reset();
            Aligning.Reset();
        }
    }
}
