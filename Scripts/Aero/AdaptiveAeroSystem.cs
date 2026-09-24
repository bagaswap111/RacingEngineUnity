using Unity.Mathematics;
using UnityEngine;

namespace RacingSim.Aero
{
    /// <summary>
    /// Main adaptive aerodynamics system.
    /// Integrates tier-based aero models, slipstream, ground effect,
    /// crosswind, and damage coupling.
    /// </summary>
    public class AdaptiveAeroSystem : MonoBehaviour
    {
        [Header("Configuration")]
        public AeroConfig config;

        [Header("Runtime State")]
        public AeroTier currentTier;
        public AeroForces currentForces;

        [Header("External References")]
        public Rigidbody vehicleRigidbody;
        public Transform frontAxleTransform;
        public Transform rearAxleTransform;

        [Header("Vehicle State (set by simulation)")]
        public float3 vehicleVelocity;
        public float rideHeightFront;
        public float rideHeightRear;
        public float pitchAngle;
        public bool drsRequested;
        public float frontWingAngle;
        public float rearWingAngle;
        public float windSpeed;
        public float3 windDirection;
        public float brakeDuctOpening;

        [Header("Damage (set by damage system)")]
        public float frontDamage;
        public float rearDamage;
        public float sideDamage;
        public float underbodyDamage;
        public float frontWingDamage;
        public float rearWingDamage;

        private float drsProgress;
        private bool drsActive;
        private float time;
        private AeroTier detectedMaxTier;
        private bool isInitialized;

        public bool DRSActive => drsActive;
        public AeroTier CurrentTier => currentTier;

        private void Awake()
        {
            if (vehicleRigidbody == null)
                vehicleRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            GPUDetect.GPUDetectResult gpu = GPUDetect.DetectAndClassify();
            currentTier = (AeroTier)gpu.Tier;
            detectedMaxTier = currentTier;

            if (config.AirDensitySeaLevel <= 0f)
                config = AeroConfig.Default();

            isInitialized = true;

            Debug.Log($"[AeroSystem] GPU: {gpu.Name} | VRAM: {gpu.VRAM_MB}MB | " +
                      $"Tier: {currentTier} (Score: {gpu.Score})");
        }

        private void FixedUpdate()
        {
            if (!isInitialized) return;

            float dt = Time.fixedDeltaTime;
            time += dt;

            UpdateDRS(dt);
            CalculateAero(dt);
            ApplyForcesToRigidbody();
        }

        public void CalculateAero(float dt)
        {
            float3 relVelocity = vehicleVelocity - windDirection * windSpeed;
            float speed = math.length(relVelocity);

            if (speed < 0.5f)
            {
                currentForces = new AeroForces();
                return;
            }

            quaternion vehicleRot = transform.rotation;
            float3 localVel = math.mul(math.inverse(vehicleRot), relVelocity);
            float yawAngle = math.atan2(localVel.z, localVel.x);

            float q = 0.5f * config.AirDensitySeaLevel * speed * speed;

            float Cd = config.CdBase;
            float ClFront = config.ClFrontBase;
            float ClRear = config.ClRearBase;
            float Cs = config.CsBase;
            float Cyaw = config.CyawBase;

            if (currentTier >= AeroTier.Low)
            {
                ApplyWingEffects(ref Cd, ref ClFront, ref ClRear);
                ApplyDRSEffects(ref Cd, ref ClFront, ref ClRear);
            }

            SlipstreamResult slipResult = new SlipstreamResult();
            if (currentTier >= AeroTier.Medium)
            {
                slipResult = CalculateSlipstream();
                Cd *= slipResult.DragMultiplier;
                ClFront *= slipResult.DownforceMultiplier;
                ClRear *= slipResult.DownforceMultiplier;
            }

            GroundEffectResult geResult = new GroundEffectResult();
            if (currentTier >= AeroTier.Medium)
            {
                geResult = GroundEffectModel.Calculate(
                    rideHeightFront, rideHeightRear,
                    pitchAngle, yawAngle,
                    brakeDuctOpening, config);

                ClFront += AeroDamageCoupling.ApplyGroundEffectDamage(
                    geResult.FrontContribution, CalculateDamageResult());
                ClRear += AeroDamageCoupling.ApplyGroundEffectDamage(
                    geResult.RearContribution, CalculateDamageResult());
            }

            CrosswindResult cwResult = new CrosswindResult();
            if (currentTier >= AeroTier.Low)
            {
                cwResult = CrosswindSystem.Calculate(
                    transform.position, vehicleVelocity,
                    transform.forward, windSpeed, windDirection,
                    time, config);
            }

            AeroDamageResult damageResult = CalculateDamageResult();
            AeroDamageCoupling.ApplyDamage(ref Cd, ref ClFront, ref ClRear, ref Cs, damageResult);

            float F_drag = q * Cd * config.FrontalArea;
            float F_down_f = q * ClFront * config.PlanformAreaFront;
            float F_down_r = q * ClRear * config.PlanformAreaRear;
            float F_side = q * Cs * config.SideArea;
            float M_yaw = q * Cyaw * config.SideArea * config.Wheelbase;

            F_side += cwResult.SideForce;
            M_yaw += cwResult.YawMoment;

            if (currentTier >= AeroTier.High && slipResult.TurbulenceIntensity > 0.01f)
            {
                float3 turbForce = SlipstreamSystem.CalculateTurbulenceForce(
                    slipResult.TurbulenceIntensity, q, config.FrontalArea, time);
                vehicleRigidbody.AddForce(turbForce, ForceMode.Force);
            }

            currentForces = new AeroForces
            {
                Drag = F_drag,
                DownforceFront = F_down_f,
                DownforceRear = F_down_r,
                SideForce = F_side,
                YawMoment = M_yaw,
                CdTotal = Cd,
                ClFrontTotal = ClFront,
                ClRearTotal = ClRear,
                CsTotal = Cs,
                DynamicPressure = q,
                AirDensity = config.AirDensitySeaLevel,
                Speed = speed,
                YawAngle = math.degrees(yawAngle),
                SlipstreamActive = slipResult.SlipstreamActive,
                DRSActive = drsActive,
                DiffuserStalled = geResult.DiffuserStalled
            };
        }

        private void ApplyWingEffects(ref float Cd, ref float ClFront, ref float ClRear)
        {
            float wingFrontCl = frontWingAngle * config.FrontWingClGain;
            float wingFrontCd = frontWingAngle * config.FrontWingCdGain;
            float wingRearCl = rearWingAngle * config.RearWingClGain;
            float wingRearCd = rearWingAngle * config.RearWingCdGain;

            ClFront += wingFrontCl;
            ClRear += wingRearCl;
            Cd += wingFrontCd + wingRearCd;
        }

        private void ApplyDRSEffects(ref float Cd, ref float ClFront, ref float ClRear)
        {
            if (!drsActive) return;

            ClRear *= (1f - config.DRS_ClReduction * drsProgress);
            Cd *= (1f - config.DRS_CdReduction * drsProgress);
        }

        private SlipstreamResult CalculateSlipstream()
        {
            SlipstreamResult result = new SlipstreamResult
            {
                DragMultiplier = 1f,
                DownforceMultiplier = 1f,
                TurbulenceIntensity = 0f,
                TotalVelocityDeficit = 0f,
                SlipstreamActive = false
            };

            if (config.SlipstreamDetectionRadius <= 0f) return result;

            Collider[] nearby = UnityEngine.Physics.OverlapSphere(
                transform.position, config.SlipstreamDetectionRadius,
                LayerMask.GetMask("Vehicle"));

            float totalDeficit = 0f;
            float maxTurb = 0f;

            for (int i = 0; i < nearby.Length && i < 8; i++)
            {
                if (nearby[i].transform == transform) continue;

                float3 otherPos = (float3)nearby[i].transform.position;
                float3 toOther = otherPos - (float3)transform.position;
                float dist = math.length(toOther);

                if (dist < 1f || dist > config.SlipstreamDetectionRadius) continue;

                float3 otherForward = (float3)nearby[i].transform.forward;
                float alignment = math.dot(math.normalizesafe(toOther), otherForward);

                if (alignment > 0.3f)
                {
                    float deficit = (1f - dist / config.SlipstreamDetectionRadius) * alignment;
                    totalDeficit += deficit * 0.5f;
                    maxTurb = math.max(maxTurb, deficit * 0.1f);
                }
            }

            totalDeficit = math.clamp(totalDeficit, 0f, 0.6f);

            result.TotalVelocityDeficit = totalDeficit;
            result.DragMultiplier = 1f - totalDeficit * config.DragReductionFactor;
            result.DownforceMultiplier = 1f - totalDeficit * config.DownforceReductionFactor;
            result.TurbulenceIntensity = maxTurb;
            result.SlipstreamActive = totalDeficit > 0.01f;

            return result;
        }

        private void UpdateDRS(float dt)
        {
            float targetProgress = drsRequested ? 1f : 0f;
            float speed = math.length(vehicleVelocity);
            bool canActivate = speed > config.DRS_MinSpeed;

            if (drsRequested && !canActivate)
                targetProgress = 0f;

            float transitionSpeed = 1f / math.max(config.DRS_TransitionTime, 0.01f);
            drsProgress = math.lerp(drsProgress, targetProgress, transitionSpeed * dt);

            drsProgress = math.clamp(drsProgress, 0f, 1f);
            drsActive = drsProgress > 0.5f;
        }

        private AeroDamageResult CalculateDamageResult()
        {
            return AeroDamageCoupling.Calculate(
                frontDamage, rearDamage, sideDamage,
                underbodyDamage, frontWingDamage, rearWingDamage);
        }

        private void ApplyForcesToRigidbody()
        {
            if (vehicleRigidbody == null) return;

            float3 down = new float3(0, -1, 0);
            float3 right = new float3(1, 0, 0);

            float3 dragForce = -math.normalizesafe(vehicleVelocity) * currentForces.Drag;
            vehicleRigidbody.AddForce(dragForce, ForceMode.Force);

            if (frontAxleTransform != null)
            {
                vehicleRigidbody.AddForceAtPosition(
                    down * math.abs(currentForces.DownforceFront),
                    frontAxleTransform.position,
                    ForceMode.Force);
            }

            if (rearAxleTransform != null)
            {
                vehicleRigidbody.AddForceAtPosition(
                    down * math.abs(currentForces.DownforceRear),
                    rearAxleTransform.position,
                    ForceMode.Force);
            }

            vehicleRigidbody.AddForce(right * currentForces.SideForce, ForceMode.Force);
            vehicleRigidbody.AddTorque(new float3(0, currentForces.YawMoment, 0), ForceMode.Force);
        }

        public void SetDRS(bool requested)
        {
            drsRequested = requested;
        }

        public void SetWind(float speed, float headingDeg)
        {
            windSpeed = speed;
            windDirection = CrosswindSystem.GetWindDirection(headingDeg);
        }

        public void SetDamage(float front, float rear, float side, float underbody,
            float frontWing, float rearWing)
        {
            frontDamage = front;
            rearDamage = rear;
            sideDamage = side;
            underbodyDamage = underbody;
            frontWingDamage = frontWing;
            rearWingDamage = rearWing;
        }

        public void AdjustTier(float currentFPS)
        {
            if (currentFPS < 30f && currentTier > AeroTier.Minimum)
            {
                currentTier--;
                Debug.LogWarning($"[AeroSystem] FPS drop! Tier reduced to {currentTier}");
            }
            else if (currentFPS > 55f && currentTier < detectedMaxTier)
            {
                currentTier++;
            }
        }

        public AeroForces GetForces()
        {
            return currentForces;
        }
    }
}
