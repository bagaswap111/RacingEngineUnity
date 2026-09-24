using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Roll dynamics and load transfer calculation.
    /// Handles body roll, geometric vs elastic load transfer,
    /// and roll center migration.
    /// </summary>

    public static class RollDynamics
    {
        private const float GRAVITY = 9.81f;

        public static float CalculateRollAngle(
            float lateralAcceleration,
            float vehicleMass,
            float cgHeight,
            float totalRollStiffness)
        {
            if (totalRollStiffness < 1f) return 0f;
            return (vehicleMass * lateralAcceleration * cgHeight) / totalRollStiffness;
        }

        public static float CalculateDynamicRoll(
            float currentRollAngle,
            float currentRollVelocity,
            float lateralAcceleration,
            float vehicleMass,
            float cgHeight,
            float rollStiffness,
            float rollDamping,
            float rollInertia,
            float dt)
        {
            float rollMoment = vehicleMass * lateralAcceleration * cgHeight;
            float restoringMoment = rollStiffness * currentRollAngle;
            float dampingMoment = rollDamping * currentRollVelocity;

            float angularAccel = rollInertia > 0.0001f ? (rollMoment - restoringMoment - dampingMoment) / rollInertia : 0f;
            float newVelocity = currentRollVelocity + angularAccel * dt;
            float newAngle = currentRollAngle + newVelocity * dt;

            return newAngle;
        }

        public static float CalculateRollStiffnessFront(
            float springRateFL,
            float springRateFR,
            float trackFront,
            float arbRateFront,
            float arbRatio)
        {
            float kSuspension = (springRateFL + springRateFR) * (trackFront * 0.5f) * (trackFront * 0.5f);
            float kArb = arbRateFront * arbRatio * (trackFront * 0.5f) * (trackFront * 0.5f);
            return kSuspension + kArb;
        }

        public static float CalculateRollStiffnessRear(
            float springRateRL,
            float springRateRR,
            float trackRear,
            float arbRateRear,
            float arbRatio)
        {
            float kSuspension = (springRateRL + springRateRR) * (trackRear * 0.5f) * (trackRear * 0.5f);
            float kArb = arbRateRear * arbRatio * (trackRear * 0.5f) * (trackRear * 0.5f);
            return kSuspension + kArb;
        }

        public static LoadTransferResult CalculateLoadTransfer(
            float vehicleMass,
            float longitudinalAccel,
            float lateralAccel,
            float cgHeight,
            float wheelbase,
            float distToFrontAxle,
            float trackFront,
            float trackRear,
            float rollCenterHeightFront,
            float rollCenterHeightRear)
        {
            LoadTransferResult result = new LoadTransferResult();

            float distToRearAxle = wheelbase - distToFrontAxle;

            result.LongitudinalFront = (vehicleMass * longitudinalAccel * cgHeight) / wheelbase;
            result.LongitudinalRear = -(vehicleMass * longitudinalAccel * cgHeight) / wheelbase;

            float totalTrack = (trackFront + trackRear) * 0.5f;
            if (totalTrack < 0.001f) return result;

            result.LateralFront = (vehicleMass * lateralAccel * cgHeight * distToRearAxle) /
                                  (wheelbase * totalTrack);
            result.LateralRear = (vehicleMass * lateralAccel * cgHeight * distToFrontAxle) /
                                 (wheelbase * totalTrack);

            if (trackFront > 0.001f)
            {
                float heightAboveRC_front = cgHeight - rollCenterHeightFront;
                result.GeometricFront = (vehicleMass * lateralAccel * rollCenterHeightFront) / trackFront;
                result.ElasticFront = (vehicleMass * lateralAccel * heightAboveRC_front) / trackFront;
            }
            if (trackRear > 0.001f)
            {
                float heightAboveRC_rear = cgHeight - rollCenterHeightRear;
                result.GeometricRear = (vehicleMass * lateralAccel * rollCenterHeightRear) / trackRear;
                result.ElasticRear = (vehicleMass * lateralAccel * heightAboveRC_rear) / trackRear;
            }

            return result;
        }

        public static float CalculateRollDistribution(
            float rollStiffnessFront,
            float rollStiffnessRear)
        {
            float total = rollStiffnessFront + rollStiffnessRear;
            if (total < 1f) return 0.5f;
            return rollStiffnessFront / total;
        }

        public static float GetLoadPerWheel(
            float staticWeight,
            float longTransfer,
            float latTransfer,
            bool isFront,
            bool isLeft)
        {
            float load = staticWeight;

            if (isFront)
                load += longTransfer;
            else
                load -= longTransfer;

            if (isLeft)
                load -= latTransfer;
            else
                load += latTransfer;

            return math.max(load, 0f);
        }
    }
}
