using Unity.Burst;
using Unity.Mathematics;

namespace RacingSim.Physics
{
    /// <summary>
    /// Roll dynamics and load transfer calculation.
    /// Handles body roll, geometric vs elastic load transfer,
    /// and roll center migration.
    /// </summary>
    [BurstCompile]
    public static class RollDynamics
    {
        private const float GRAVITY = 9.81f;

        [BurstCompile]
        public static float CalculateRollAngle(
            float lateralAcceleration,
            float vehicleMass,
            float cgHeight,
            float totalRollStiffness)
        {
            if (totalRollStiffness < 1f) return 0f;
            return (vehicleMass * lateralAcceleration * cgHeight) / totalRollStiffness;
        }

        [BurstCompile]
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

            float angularAccel = (rollMoment - restoringMoment - dampingMoment) / rollInertia;
            float newVelocity = currentRollVelocity + angularAccel * dt;
            float newAngle = currentRollAngle + newVelocity * dt;

            return newAngle;
        }

        [BurstCompile]
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

        [BurstCompile]
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

        [BurstCompile]
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

            result.GeometricFront = (vehicleMass * lateralAccel * rollCenterHeightFront) / trackFront;
            result.GeometricRear = (vehicleMass * lateralAccel * rollCenterHeightRear) / trackRear;

            float heightAboveRC_front = cgHeight - rollCenterHeightFront / 1000f;
            float heightAboveRC_rear = cgHeight - rollCenterHeightRear / 1000f;
            result.ElasticFront = (vehicleMass * lateralAccel * heightAboveRC_front) / trackFront;
            result.ElasticRear = (vehicleMass * lateralAccel * heightAboveRC_rear) / trackRear;

            return result;
        }

        [BurstCompile]
        public static float CalculateRollDistribution(
            float rollStiffnessFront,
            float rollStiffnessRear)
        {
            float total = rollStiffnessFront + rollStiffnessRear;
            if (total < 1f) return 0.5f;
            return rollStiffnessFront / total;
        }

        [BurstCompile]
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
