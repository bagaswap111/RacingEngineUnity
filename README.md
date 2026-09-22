# Racing Sim - Custom Physics Engine for Unity

Arsitektur engine simulasi balap custom di Unity dengan fisika kendaraan tingkat tinggi (DX11 / GTX 7 Series compatible).

## Struktur Project

```
/workspace
├── Scripts/
│   ├── Physics/          # Core physics systems
│   │   ├── PacejkaTireModel.cs    # Ban model dengan Pacejka Magic Formula
│   │   └── SuspensionSystem.cs    # Suspensi kinematika & spring-damper
│   ├── Vehicle/          # Vehicle dynamics & drivetrain
│   ├── Aero/             # Aerodynamics simulation
│   ├── Electronics/      # ECU, TC, ABS
│   ├── Damage/           # Visual & mechanical damage
│   ├── Telemetry/        # Data logging & lap timing
│   └── Utils/            # Helper utilities
├── ScriptableObjects/    # Config assets
│   └── VehicleConfig.cs  # Parameter kendaraan lengkap
└── Tests/                # Unit tests
    └── PacejkaTireModelTests.cs
```

## Modul yang Sudah Diimplementasikan

### ✅ 1. VehicleConfig (ScriptableObject)
- Parameter lengkap untuk vehicle dynamics
- Engine, drivetrain, brake, suspension, tire, aero, electronics
- Damage thresholds dan konfigurasi DRS

### ✅ 2. Pacejka Tire Model
- Pure lateral force calculation
- Pure longitudinal force calculation  
- Combined slip dengan weighting functions
- Aligning torque (Mz)
- Temperature multiplier (grip vs suhu)
- Wear multiplier (grip vs keausan)
- Load-dependent coefficients
- Optimized dengan Unity.Burst

### ✅ 3. Suspension System
- Spring-damper model dengan bump stop
- Kinematics (camber, toe, caster)
- Hub position calculation
- Ride height detection
- WheelState struct untuk data roda

### 📋 Berikutnya (To Implement)
4. Drivetrain & Engine Simulation
5. Aerodynamics System
6. Electronics (TC/ABS)
7. Thermal Models (Tire & Brake)
8. Damage System
9. Telemetry & Lap Timing
10. Main Simulation Loop Integration

## Cara Menggunakan dengan Qwen Coder

Gunakan strategi modular untuk generate modul berikutnya:

```
PROMPT 4 - Drivetrain:
"Buat sistem drivetrain lengkap di Unity C#: engine torque map,
clutch model, gearbox dengan shift time, differential 
(open + LSD). Class DrivetrainSim."

PROMPT 5 - Aerodynamics:
"Buat sistem aerodinamika dengan aero map (2D lookup table)
berdasarkan yaw angle dan ride height. Hitung drag, downforce
depan/belakang, dan side force. Class AeroSim."

...dan seterusnya sesuai checklist.
```

## Requirements Unity

- Unity 2021.3 LTS atau lebih baru
- Unity.Mathematics package
- Unity.Burst Compiler package
- Unity.Collections package
- Unity.Jobs package (untuk parallel processing)

## Target Performa

- Physics tick: 240Hz (FixedUpdate dengan timestep 1/240)
- Per roda per frame: < 0.1ms
- Total vehicle update: < 1ms per frame
- Compatible dengan GTX 7 Series (DX11)

## Referensi Fisika

Lihat dokumentasi lengkap dalam prompt awal untuk:
- Rumus Pacejka Magic Formula
- Load transfer equations
- Aerodynamics formulas
- Thermal models
- Damage algorithms

## Testing

Jalankan unit tests di folder `Tests/` menggunakan Unity Test Runner.
Tests mencakup validasi fisika dasar untuk tire model.
