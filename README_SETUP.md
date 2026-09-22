# Racing Simulation Engine - Setup & Usage Guide

## 📋 Prerequisites

- **Unity Version**: 2021.3 LTS atau lebih baru
- **Render Pipeline**: Universal Render Pipeline (URP)
- **API Compatibility**: .NET Standard 2.1
- **Burst Compiler**: Package Manager → Install "Burst" package
- **Collections**: Package Manager → Install "Collections" package
- **Mathematics**: Package Manager → Install "Mathematics" package

---

## 🚀 Quick Start

### Step 1: Import Scripts ke Unity

Copy folder berikut ke dalam project Unity Anda:
```
/workspace/Scripts/          → Assets/Scripts/
/workspace/ScriptableObjects/ → Assets/ScriptableObjects/
/workspace/Tests/            → Assets/Tests/
```

### Step 2: Create VehicleConfig Asset

1. Di Unity Project window, klik kanan di folder `Assets/ScriptableObjects/`
2. Pilih **Create → Racing Sim → Vehicle Config**
3. Beri nama "F1Car" atau "GT3Car"
4. Klik asset tersebut untuk membuka Inspector

### Step 3: Configure Vehicle Parameters

Di Inspector, atur parameter kendaraan:

#### General Parameters (Default F1 Car)
- **Total Mass**: 750 kg
- **Wheelbase**: 2.65 m
- **CoG Height**: 0.28 m
- **Front Track Width**: 1.45 m
- **Rear Track Width**: 1.40 m

#### Engine
- **Idle RPM**: 900
- **Redline RPM**: 13500
- **Max Power**: 550 HP
- **Max Torque**: 380 Nm
- **Engine Torque Curve**: Edit AnimationCurve dengan dyno data

#### Drivetrain
- **Drivetrain Type**: RWD
- **Gear Ratios**: [−3.5, 3.8, 2.9, 2.3, 1.9, 1.6, 1.3, 1.1]
- **Final Drive Ratio**: 4.2
- **Differential Type**: LimitedSlip
- **Torsen TBR**: 3.0

#### Suspension
- **Spring Rate Front/Rear**: 180000 / 200000 N/m
- **Damper Compression**: 12000 N·s/m
- **Damper Rebound**: 16000 N·s/m
- **Anti-roll Bar Front/Rear**: 40000 / 45000 N·m/rad

#### Brakes
- **Max Pressure**: 120 bar
- **Caliper Area Front**: 0.005 m²
- **Caliper Area Rear**: 0.004 m²
- **Disc Radius Front**: 0.165 m
- **Disc Radius Rear**: 0.160 m
- **Pad Friction**: 0.45

#### Aerodynamics
- **Drag Coefficient (Cd)**: 0.9
- **Lift Coefficient (Cl)**: -3.5
- **Aero Balance Front**: 0.45
- **Frontal Area**: 1.8 m²

#### Electronics
- **TC Enabled**: true
- **TC Level**: 0.5 (0-1)
- **ABS Enabled**: true
- **ABS Level**: 0.7

#### Tires (Pacejka Coefficients)
Gunakan Default values yang sudah disediakan, atau customize:
- **pCy1**: 1.3 (shape factor lateral)
- **pDy1**: 1.9 (peak lateral)
- **pKy1**: 15.0 (stiffness lateral)

---

## 🎮 Create Test Scene

### Step 1: Create Ground Plane

1. **GameObject → 3D Object → Plane**
2. Scale: (100, 1, 100)
3. Create material "Asphalt" dengan:
   - Albedo: Dark gray (#333333)
   - Roughness: 0.8
4. Assign material ke plane

### Step 2: Create Vehicle GameObject

1. **GameObject → Create Empty** → Rename jadi "PlayerCar"
2. Position: (0, 0.5, 0)
3. Add component **SimulationController** (script ada di `Scripts/Core/SimulationLoop.cs`)

### Step 3: Setup SimulationController

Di Inspector untuk SimulationController:

```csharp
// Drag & drop VehicleConfig asset yang sudah dibuat
Vehicle Config: [F1Car.asset]

// Input mapping (default keyboard)
Throttle Axis: Keyboard W / Gamepad Right Trigger
Brake Axis: Keyboard S / Gamepad Left Trigger
Steering Axis: Keyboard A,D / Gamepad Left Stick X
Clutch Axis: Keyboard C / Gamepad Button
Gear Up: Keyboard Up Arrow / Gamepad DPAD Up
Gear Down: Keyboard Down Arrow / Gamepad DPAD Down
DRS Toggle: Keyboard K / Gamepad Button X

// Visual wheel transforms (optional, untuk animasi roda)
Wheel FL Transform: [drag wheel model front-left]
Wheel FR Transform: [drag wheel model front-right]
Wheel RL Transform: [drag wheel model rear-left]
Wheel RR Transform: [drag wheel model rear-right]

// Camera settings
Chase Camera Offset: (0, 2.5, -5)
Chase Camera Damping: 5.0

// Telemetry
Enable Telemetry: true
Telemetry Port: 20777
Log To File: false
```

### Step 4: Add Visual Wheel Models

Untuk setiap roda:
1. Buat cylinder sebagai placeholder: **GameObject → 3D Object → Cylinder**
2. Rotate 90° pada X axis (supaya menghadap forward)
3. Scale: (0.33, 0.2, 0.33) sesuai wheel radius
4. Posisikan di keempat sudut mobil:
   - FL: (+1.15, 0, −0.725)
   - FR: (+1.15, 0, +0.725)
   - RL: (−1.50, 0, −0.70)
   - RR: (−1.50, 0, +0.70)
5. Jadikan child dari "PlayerCar"
6. Drag masing-masing ke slot di SimulationController

### Step 5: Add Main Camera

1. **GameObject → Camera**
2. Position: (0, 3, −6)
3. Rotation: (10, 0, 0)
4. Field of View: 60°
5. Near Clip: 0.1
6. Far Clip: 1000

### Step 6: Add Lighting

1. **GameObject → Light → Directional Light**
2. Intensity: 1.0
3. Color: Warm white (#FFF4E6)
4. Rotation: (50, −30, 0)

---

## ⌨️ Input Configuration

### Default Keyboard Controls

| Action | Key |
|--------|-----|
| Throttle | W |
| Brake | S |
| Steer Left | A |
| Steer Right | D |
| Clutch | C |
| Shift Up | ↑ Arrow |
| Shift Down | ↓ Arrow |
| DRS | K |
| Handbrake | Space |
| Reset Car | R |
| Toggle Camera | V |

### Gamepad Controls (XInput)

| Action | Button/Axis |
|--------|-------------|
| Throttle | RT (Right Trigger) |
| Brake | LT (Left Trigger) |
| Steer | Left Stick X-axis |
| Clutch | Left Bumper |
| Shift Up | DPAD Up |
| Shift Down | DPAD Down |
| DRS | X Button |
| Handbrake | A Button |
| Reset Car | Back Button |

---

## 🔧 Tuning & Calibration

### Tire Coefficients Tuning

Jika mobil terasa:
- **Understeer berlebihan**: Tingkatkan `pDy1` (lateral grip depan) atau turunkan `pKy1`
- **Oversteer berlebihan**: Turunkan `pDy1` belakang atau naikkan `pKy1` depan
- **Grip terlalu tinggi**: Turunkan semua `pD*` coefficients 10-20%
- **Grip terlalu rendah**: Naikkan `pD*` coefficients

### Suspension Tuning

- **Terlalu bouncy**: Naikkan damper compression (`damperCompression`)
- **Terlalu kaku**: Turunkan spring rate (`springRateFront/Rear`)
- **Body roll berlebihan**: Naikkan anti-roll bar (`antiRollBarFront/Rear`)
- **Bottoming out**: Naikkan bump stop stiffness (`bumpStopStiffness`)

### Brake Bias Adjustment

Untuk braking stability:
- **Understeer saat braking**: Pindahkan bias ke belakang (turunkan `brakeBias` dari 0.6 ke 0.55)
- **Oversteer saat braking**: Pindahkan bias ke depan (naikkan `brakeBias` ke 0.65)

### TC/ABS Tuning

- **TC terlalu agresif**: Turunkan `tcLevel` atau `tcKp`
- **TC tidak cukup介入**: Naikkan `tcLevel` atau `tcKp`
- **ABS menyebabkan getaran**: Turunkan `absKp` dan naikkan `absKd`

---

## 📊 Telemetry & Debugging

### In-Game HUD

SimulationController menyediakan debug overlay (tekan **F1**):
- Speed (km/h)
- RPM & Gear
- Throttle/Brake %
- Steering angle
- G-force (longitudinal/lateral)
- Tire temps (FL, FR, RL, RR)
- Brake temps
- Lap time & delta

### UDP Telemetry Output

Aktifkan di SimulationController:
```csharp
Enable UDP Telemetry: true
UDP Port: 20777
Target IP: 127.0.0.1 (localhost) atau broadcast IP
```

Format data: Binary struct `TelemetryFrame` (lihat `Scripts/Telemetry/TelemetrySystem.cs`)

Tools kompatibel:
- **SimHub** (simhub.com)
- **RaceAnalyzer**
- Custom dashboard dengan Python/C++

### Log Files

Aktifkan logging:
```csharp
Log To File: true
Log Directory: /TelemetryLogs/
```

File format: Binary `.bin` atau CSV (configurable)

---

## 🐛 Troubleshooting

### Mobil Tidak Bergerak

**Penyebab**: Stub methods belum diimplementasi
**Solusi**: Pastikan semua helper methods di `SimulationLoop.cs` sudah filled (sudah done di versi ini)

### FPS Drop Parah

**Penyebab**: Burst compiler tidak aktif
**Solusi**: 
1. Package Manager → Burst → Enable
2. Edit → Project Settings → Burst → Enable Compilation
3. Check "Synchronous Compilation" untuk testing

### Roda Bergetar Hebat

**Penyebab**: Timestep terlalu besar atau spring rate terlalu tinggi
**Solusi**: 
- Turunkan `FIXED_TIMESTEP` dari 1/240 ke 1/360
- Turunkan spring rate 20-30%

### Ban Tembus Tanah

**Penyebab**: Ride height negatif atau ground detection salah
**Solusi**: 
- Pastikan `cogHeight` > 0.25m
- Check suspension travel limits

### Compile Error: "Namespace not found"

**Solusi**: Pastikan using statements lengkap di setiap file:
```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
```

---

## 📈 Performance Optimization

### Target Performance

- **Physics Tick**: 240 Hz minimum (4.17ms per frame)
- **Render Frame**: 60+ FPS (GTX 7 Series compatible)
- **Memory**: < 50 MB allocation per session

### Optimization Tips

1. **Enable Burst Compilation**
   - Edit → Project Settings → Burst → Enable
   - Set "Optimization Level" ke High

2. **Use Job System**
   - Semua sistem fisika sudah menggunakan `[BurstCompile]`
   - Hindari GC allocation di hot path

3. **Reduce Draw Calls**
   - Bake lighting (Window → Rendering → Lighting)
   - Use GPU instancing untuk track objects

4. **LOD for Track**
   - Implement LOD groups untuk track geometry
   - Occlusion culling untuk complex circuits

---

## 🏁 Next Steps

Setelah basic setup bekerja:

1. **Import Track Model**
   - Download free track dari Asset Store atau RaceDepartment
   - Setup colliders dan surface types

2. **Add AI Opponents**
   - Clone PlayerCar prefab
   - Replace input dengan AI controller
   - Setup racing line waypoints

3. **Implement Race Systems**
   - Pit stop logic
   - Fuel consumption
   - Tire wear strategies

4. **Multiplayer (Advanced)**
   - Client-server architecture
   - State interpolation/extrapolation
   - Lag compensation

---

## 📚 References

### Physics Papers
- **Pacejka Magic Formula**: "Tire and Vehicle Dynamics" by Hans Pacejka (3rd Ed.)
- **Vehicle Dynamics**: "Fundamentals of Vehicle Dynamics" by Thomas D. Gillespie
- **Racing Car Design**: "Competition Car Engineering" by Simon McBeath

### Open Source Projects
- **Project Cars** (partial source available)
- **rFactor SDK**
- **Live for Speed** physics documentation

### Tools
- **MoTeC i2** untuk telemetry analysis
- **ATLAS** untuk data comparison
- **Blender** untuk 3D modeling

---

## 📞 Support

Jika mengalami masalah:
1. Check console untuk error messages
2. Verify semua dependencies terinstall
3. Test dengan default config terlebih dahulu
4. Compare dengan reference implementation

**Happy Racing! 🏎️💨**
