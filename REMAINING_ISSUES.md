# Remaining Issues — Tracked for Future Resolution

Last updated: 2026-09-23

---

## BLOCKED: Legacy Files (won't compile, need architectural decisions)

### 1. DrivetrainSystem.cs
**Status:** Won't compile — 15+ wrong VehicleConfig field names
**Root Cause:** Written against old VehicleConfig definition
**Required Fix:** Separate DamageConfig ScriptableObject or extend VehicleConfig

| Wrong Field | Actual Field |
|---|---|
| `engineMomentOfInertia` | `engineInertia` |
| `engineFrictionConstant` | `engineFrictionBase` |
| `clutchFrictionCoeff` | `clutchFrictionCoefficient` |
| `clutchNormalForce` | `clutchMaxForce` |
| `clutchNumFrictionSurfaces` | *(missing)* |
| `lsdPreloadTorque` | `diffPreloadTorque` |
| `lsdRampFactor` | *(missing)* |
| `lsdRampAngle` | `diffRampAngle` |
| `viscousDiffCoefficient` | `diffViscousCoefficient` |
| `torsenTBR` | `diffTorqueBiasRatio` |
| `engineBrakingBase` | *(missing)* |
| `optimalOilTempMin` | *(missing)* |
| `engineTempOptimalMax` | *(missing)* |
| `engineTempCritical` | *(missing)* |
| `optimalShiftRPM` | *(missing)* |
| `shiftDuration` | *(missing)* |
| `transmissionType` | *(missing)* |
| `awdFrontBias` | *(missing)* |
| `TransmissionType.Automatic` | *(enum missing)* |
| `DifferentialType.LimitedSlip` | `DifferentialType.LSD_Clutch` |

---

### 2. DamageSystem.cs
**Status:** Won't compile — 20+ wrong VehicleConfig fields
**Root Cause:** Written against old VehicleConfig definition
**Required Fix:** Separate DamageConfig ScriptableObject

| Wrong Field | Purpose |
|---|---|
| `engineDamageOverrevRate` | Engine over-rev damage rate |
| `engineTempOptimalMax` | Max optimal engine temp |
| `engineDamageOverheatRate` | Overheat damage rate |
| `optimalOilTempMin` | Min optimal oil temp |
| `engineDamageColdRate` | Cold engine damage rate |
| `suspensionMaxLoad` | Max suspension load |
| `suspensionDamageOverloadRate` | Overload damage rate |
| `suspensionDamageBottomingRate` | Bottoming damage rate |
| `suspensionDamageOvertravelRate` | Overtravel damage rate |
| `suspensionTravelMax` | Max suspension travel |
| `tireWearBaseRate` | Base tire wear rate |
| `tireWearSlipRate` | Slip-based tire wear |
| `tireOverheatThreshold` | Tire overheat temp |
| `tireWearFlatSpotRate` | Flat spot wear rate |
| `tireMaxLoad` | Max tire load |
| `tireBurstTemperature` | Tire burst temp |
| `aeroDamageThreshold` | Aero damage threshold |
| `aeroDamageCoefficient` | Aero damage multiplier |

**Additional Issues:**
- Missing `using Unity.Collections;` for NativeArray
- `[BurstCompile]` on static class (invalid)
- `string` parameter in Burst-compiled method
- Wrong normal calculation formula (line 93)

---

### 3. AeroSystem.cs
**Status:** Dead code — superseded by AdaptiveAeroSystem
**Root Cause:** Written against different type system (AeroMapPoint, AeroCoefficients)
**Required Fix:** Delete file or mark `[Obsolete]`

**Issues:**
- Missing `using RacingSim.Vehicle;`
- 20+ wrong VehicleConfig field names
- Uses `AeroMapPoint[]` and `AeroCoefficients` (incompatible with AeroDataStructures)
- Return type mismatch (float3 vs float for torque)

---

### 4. SimulationLoop.cs
**Status:** Won't compile — multiple issues
**Root Cause:** Written against old type definitions
**Required Fix:** Rewrite to use new types (ChassisRigidBody, IVehicleInput, etc.)

**Issues:**
- Missing `using RacingSim.Vehicle;`
- `DifferentialType` in wrong namespace
- `DrivetrainType` in wrong namespace
- `WheelState` field name mismatches (PascalCase vs camelCase)
- Wrong return type for Aero torque
- Wrong argument count for `CalculateKinematics`
- `DrivetrainState.OutputTorque` doesn't exist
- GC allocations in hot path (new float[4], new PIDController[4])
- Inverted angular acceleration guard
- Double-counting brake forces

---

## DESIGN: Architectural Decisions Needed

### A. DamageConfig ScriptableObject
**Priority:** High
**Effort:** 1 day
**Impact:** Unblocks DamageSystem.cs and DrivetrainSystem.cs

```csharp
[CreateAssetMenu(fileName = "DamageConfig", menuName = "RacingSim/Damage Config")]
public class DamageConfig : ScriptableObject
{
    [Header("Engine")]
    public float EngineDamageOverrevRate;
    public float EngineDamageOverheatRate;
    public float EngineDamageColdRate;
    public float EngineTempOptimalMax;
    public float OptimalOilTempMin;

    [Header("Suspension")]
    public float SuspensionMaxLoad;
    public float SuspensionDamageOverloadRate;
    public float SuspensionDamageBottomingRate;
    public float SuspensionDamageOvertravelRate;

    [Header("Tires")]
    public float TireWearBaseRate;
    public float TireWearSlipRate;
    public float TireOverheatThreshold;
    public float TireWearFlatSpotRate;
    public float TireMaxLoad;
    public float TireBurstTemperature;

    [Header("Aero")]
    public float AeroDamageThreshold;
    public float AeroDamageCoefficient;
}
```

---

### B. Shared VehicleState.cs
**Priority:** High
**Effort:** 0.5 day
**Impact:** Unblocks SimulationLoop.cs

```csharp
// Scripts/Core/VehicleState.cs
public struct VehicleState
{
    // Chassis
    public float3 Position;
    public quaternion Rotation;
    public float3 Velocity;
    public float3 AngularVelocity;

    // Per-wheel (4 elements)
    public float[] Travel;
    public float[] VerticalLoad;
    public float[] SlipRatio;
    public float[] SlipAngle;
    public bool[] IsGrounded;

    // Drivetrain
    public float EngineRPM;
    public int CurrentGear;
    public float ClutchPosition;

    // Input
    public float Steering;
    public float Throttle;
    public float Brake;
}
```

---

### C. IVehicleInput Integration
**Priority:** Medium
**Effort:** 0.5 day
**Impact:** Removes legacy Input.GetAxis usage

Replace in:
- `AdvancedSuspensionSystem.cs:254,259,264` — `Input.GetAxis` calls
- `SimulationLoop.cs` — if rewritten

---

### D. WheelConfig Struct
**Priority:** Low
**Effort:** 0.5 day
**Impact:** Removes hardcoded 0.33f in 3 files

```csharp
[System.Serializable]
public struct WheelConfig
{
    public float Radius;      // 0.33f default
    public float Width;       // 0.25f
    public float TireStiffness;
    public float TireDamping;
}
```

Files with hardcoded 0.33f:
- `SuspensionKinematics.cs:246`
- `AdvancedSuspensionSystem.cs:103`
- `GroundDetection.cs:190`

---

### E. WheelState Relocation
**Priority:** Low
**Effort:** 0.25 day
**Impact:** Clean up obsolete type

Move `WheelState` from `SuspensionSystem.cs` to `Core/VehicleState.cs` for tire model integration.

---

## MINOR: Non-blocking Issues

| File | Issue | Severity |
|---|---|---|
| `EnvironmentalPhysics.cs` | Hardcoded gravity 9.80665 vs VehicleConstants 9.81 | Low |
| `VehicleConstants.cs` | DEG_TO_RAD precision (0.0174533) | Low |
| `GroundDetection.cs` | String allocation in tag comparison | Low |
| `GroundDetection.cs` | Array allocation in hot path | Low |
| `SuspensionDataStructures.cs` | [Header] on non-serialized struct | Low |
| `AdvancedSuspensionSystem.cs` | Legacy Input system | Low |
| `SuspensionKinematics.cs` | Wheelbase approximated from hardpoints | Low |

---

## RESOLUTION ORDER

1. **Create DamageConfig.cs** → Unblocks DamageSystem + DrivetrainSystem
2. **Create VehicleState.cs** → Unblocks SimulationLoop
3. **Wire IVehicleInput** → Removes legacy Input calls
4. **Create WheelConfig** → Removes hardcoded 0.33f
5. **Delete/archive AeroSystem.cs** → Cleanup dead code
6. **Rewrite SimulationLoop.cs** → Uses new types
