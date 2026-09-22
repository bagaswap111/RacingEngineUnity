# Sistem Simulasi Aerodinamika Adaptif
## Dari Model Sederhana (GTX 7) hingga Simulasi Kompleks (RTX 40)

---

## 1. LEVEL KOMPLEKSITAS AERODINAMIKA

```
Spektrum Simulasi Aerodinamika untuk Game Balap:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Level 0: ARCADE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: F_drag = konstanta × V²
         F_down = konstanta × V²
  Input: Kecepatan saja
  Tidak ada: yaw, ride height, wing, slipstream
  CPU: ~0.001 ms
  Contoh: Mario Kart, NFS arcade

Level 1: SIMCADE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Cd & Cl tetap, tapi ada front/rear split
  Input: Kecepatan, wing angle (1 parameter)
  Ada: slipstream sederhana, DRS
  Tidak ada: yaw dependency, ride height, ground effect
  CPU: ~0.005 ms
  Contoh: Forza Motorsport, GRID

Level 2: SIMULATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Aero map multi-dimensi (yaw, ride height, wing)
  Input: Kecepatan, yaw, ride height, wing angles, steer
  Ada: slipstream, dirty air, crosswind, ground effect,
       aero damage, brake cooling trade-off
  CPU: ~0.02 ms
  Contoh: Assetto Corsa Competizione, rFactor 2, iRacing

Level 3: SEMI-CFD (Panel Method / Vortex Lattice)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Simplified CFD dengan panel method
  Input: Full 3D geometry approximation
  Ada: wake interaction, vortex shedding (simplified),
       tire wake, diffuser flow separation
  CPU: ~0.5-2.0 ms (dengan GPU compute)
  Contoh: Belum ada di game komersial (riset)

Level 4: FULL CFD (Computational Fluid Dynamics)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Navier-Stokes solver (RANS/LES)
  TIDAK FEASIBLE untuk real-time game
  CPU: ~menit-jam per frame
  Contoh: Simulasi engineering F1, wind tunnel virtual
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Untuk game Anda, kita akan merancang **sistem adaptif** yang memilih level berdasarkan GPU:

```
Adaptive Aero Quality:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
GPU Tier 0 (GTX 7 / Integrated)  → Level 1 (Simcade)
GPU Tier 1 (GTX 10 / RX 500)     → Level 2 (Simulation)
GPU Tier 2 (RTX 20 / RX 5000)    → Level 2+ (Enhanced Simulation)
GPU Tier 3 (RTX 30 / RX 6000)    → Level 2++ (Full Simulation)
GPU Tier 4 (RTX 40 / RX 7000+)   → Level 3 (Semi-CFD + Visual FX)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 2. MODEL AERODINAMIKA DASAR (Semua Tier)

### 2.1 Rumus Fundamental

```
Aerodynamic Forces - Fundamental:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Dynamic Pressure:
  q = 0.5 × ρ × V²
  
  ρ = densitas udara (kg/m³)
    = 1.225 di sea level, 15°C
    = 1.112 di ketinggian 1000m
    = 1.007 di ketinggian 2000m
  V = kecepatan relatif kendaraan terhadap udara (m/s)

Drag Force (tahanan udara):
  F_drag = q × Cd × A_frontal
  
  Cd = drag coefficient (dimensionless)
  A_frontal = luas frontal kendaraan (m²)
  
  Contoh:
    GT3 car: Cd ≈ 0.45-0.55, A ≈ 1.8-2.2 m²
    F1 car:  Cd ≈ 0.70-0.90, A ≈ 1.5-1.8 m²
    Road car: Cd ≈ 0.25-0.35, A ≈ 2.0-2.5 m²

Downforce (gaya tekan ke bawah):
  F_down = q × Cl × A_planform
  
  Cl = lift coefficient (negatif = downforce)
  A_planform = luas referensi (m²)
  
  Contoh:
    GT3 di 250 km/h: F_down ≈ 15.000-20.000 N
    F1 di 250 km/h:  F_down ≈ 25.000-35.000 N
    
  Downforce menambah grip:
    Fz_total = Fz_static + F_down
    → Grip ban meningkat tanpa menambah massa

Side Force (gaya samping):
  F_side = q × Cs × A_side
  
  Cs = side force coefficient
  A_side = luas samping kendaraan (m²)
  → Penting saat crosswind

Yaw Moment (torsi yaw dari aero):
  M_yaw = q × Cyaw × A_side × L_wheelbase
  
  → Mempengaruhi stabilitas saat crosswind
  → Mempengaruhi balance saat slipstream
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 2.2 Aero Balance

```
Aero Balance (Distribusi Downforce Depan/Belakang):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Downforce tidak merata. Distribusi depan/belakang
sangat mempengaruhi handling:

  F_down_front = F_down_total × balance_front
  F_down_rear = F_down_total × (1 - balance_front)
  
  balance_front biasanya: 0.40 - 0.50
  
  balance_front > 0.50 → lebih stabil (understeer tendency)
  balance_front < 0.40 → lebih rotasi (oversteer tendency)

Aero balance BERUBAH dengan:
  - Yaw angle (saat cornering)
  - Ride height (pitch & roll)
  - Wing angle settings
  - DRS active/inactive
  - Damage (body deformasi)
  - Slipstream (mengubah distribusi tekanan)

Center of Pressure (CoP):
  x_CoP = (F_down_front × x_front + F_down_rear × x_rear) / F_down_total
  
  CoP di depan CoG → stabil
  CoP di belakang CoG → unstable (aero-induced oversteer)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 2.3 Algoritma Aero Dasar (Tier 0)

```
ALGORITMA: AeroModel_Basic.Calculate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIER: 0 (GTX 7 / Integrated)
CPU COST: ~0.002 ms

INPUT:  vehicleSpeed, vehicleForwardDir, windVector
OUTPUT: F_drag, F_down_front, F_down_rear

1. Hitung kecepatan relatif terhadap udara:
   V_rel = vehicleVelocity - windVector
   V_mag = |V_rel|
   
   IF V_mag < 0.5:
       RETURN (0, 0, 0)  // Terlalu pelan, abaikan

2. Hitung dynamic pressure:
   q = 0.5 × airDensity × V_mag²

3. Hitung drag:
   F_drag_mag = q × Cd_fixed × A_frontal
   F_drag = -normalize(V_rel) × F_drag_mag
   
   // Cd_fixed = konstanta dari config (tidak berubah)

4. Hitung downforce:
   F_down_total = q × Cl_fixed × A_planform
   F_down_front = F_down_total × balance_front
   F_down_rear = F_down_total × (1 - balance_front)
   
   // balance_front = konstanta dari config

5. Apply ke vehicle:
   vehicle.AddForce(F_drag)                    // Drag
   vehicle.AddForceAtPosition(
       float3(0, -F_down_front, 0),            // Downforce depan
       frontAxlePosition
   )
   vehicle.AddForceAtPosition(
       float3(0, -F_down_rear, 0),             // Downforce belakang
       rearAxlePosition
   )

6. RETURN F_drag, F_down_front, F_down_rear
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 3. AERO MAP SYSTEM (Tier 1+)

### 3.1 Multi-Dimensional Aero Map

```
Aero Map Concept:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Cd dan Cl BUKAN konstanta. Mereka adalah fungsi dari:

  Cd(β, RH_f, RH_r, δ_wing_f, δ_wing_r, DRS)
  Cl(β, RH_f, RH_r, δ_wing_f, δ_wing_r, DRS)
  
  β = yaw angle (sudut antara arah mobil & arah kecepatan)
  RH_f = ride height depan (mm)
  RH_r = ride height belakang (mm)
  δ_wing_f = sudut sayap depan (derajat)
  δ_wing_r = sudut sayap belakang (derajat)
  DRS = Drag Reduction System aktif? (bool)

Implementasi: 2D/3D Lookup Table dengan interpolasi

Untuk performa, kita pecah menjadi beberapa map kecil:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.2 Yaw Angle Dependency

```
Yaw Angle Effect:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Saat cornering, mobil tidak bergerak lurus.
Ada sudut yaw (β) antara arah mobil dan arah kecepatan.

  β = arctan(Vy / |Vx|)
  
  Vy = kecepatan lateral (m/s)
  Vx = kecepatan longitudinal (m/s)

Efek yaw:
  - Cd MENINGKAT (more frontal area exposed)
  - Cl MENURUN (flow separation di satu sisi)
  - Side force MUNCUL (F_side)
  - Yaw moment MUNCUL (stability effect)

Lookup Table (1D):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
β (deg)  │ Cd_mult │ Cl_mult │ Cs    │ Cyaw
─────────┼─────────┼─────────┼───────┼──────
  0      │ 1.000   │ 1.000   │ 0.000 │ 0.000
  5      │ 1.020   │ 0.980   │ 0.150 │ 0.020
  10     │ 1.060   │ 0.940   │ 0.350 │ 0.050
  15     │ 1.120   │ 0.880   │ 0.550 │ 0.080
  20     │ 1.200   │ 0.800   │ 0.700 │ 0.100
  30     │ 1.400   │ 0.650   │ 0.900 │ 0.120
  45     │ 1.700   │ 0.450   │ 1.100 │ 0.100
  60     │ 2.000   │ 0.300   │ 1.200 │ 0.050
  90     │ 2.500   │ 0.150   │ 1.300 │ 0.000

Interpolasi: linear antar titik
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.3 Ride Height Dependency

```
Ride Height Effect:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ride height (jarak mobil ke tanah) sangat mempengaruhi
ground effect dan overall aero.

  RH_f = ride height depan (mm), biasanya 30-100 mm
  RH_r = ride height belakang (mm), biasanya 50-120 mm

Efek ride height:
  - RH rendah → downforce NAIK (ground effect lebih kuat)
  - RH terlalu rendah → STALL (flow separation, downforce DROP)
  - RH tinggi → downforce TURUN, drag TURUN sedikit
  
Ride Height Sensitivity (Pitch):
  pitch = (RH_r - RH_f) / wheelbase × 1000  // mm per meter
  
  pitch positif (rake) → rear downforce naik, front turun
  pitch negatif → sebaliknya

Lookup Table (2D: RH_f × RH_r):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
         │ RH_r = 60  │ RH_r = 80  │ RH_r = 100 │ RH_r = 120
─────────┼────────────┼────────────┼────────────┼───────────
RH_f=30  │ Cl=3.2     │ Cl=3.0     │ Cl=2.7     │ Cl=2.3
         │ Cd=0.52    │ Cd=0.50    │ Cd=0.48    │ Cd=0.46
─────────┼────────────┼────────────┼────────────┼───────────
RH_f=50  │ Cl=2.9     │ Cl=2.7     │ Cl=2.5     │ Cl=2.2
         │ Cd=0.48    │ Cd=0.47    │ Cd=0.45    │ Cd=0.44
─────────┼────────────┼────────────┼────────────┼───────────
RH_f=70  │ Cl=2.5     │ Cl=2.4     │ Cl=2.2     │ Cl=2.0
         │ Cd=0.45    │ Cd=0.44    │ Cd=0.43    │ Cd=0.42
─────────┼────────────┼────────────┼────────────┼───────────
RH_f=90  │ Cl=2.1     │ Cl=2.0     │ Cl=1.9     │ Cl=1.8
         │ Cd=0.43    │ Cd=0.42    │ Cd=0.41    │ Cd=0.40

Interpolasi: bilinear (4 titik terdekat)

STALL REGION:
  IF RH_f < 20 OR RH_r < 30:
      // Ground effect stall
      Cl *= 0.3  // Downforce drop drastis
      Cd *= 1.3  // Drag naik (flow separation)
      StallWarning = true
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.4 Wing Angle Dependency

```
Wing Angle Effect:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Sayap depan dan belakang adjustable:

Front Wing:
  δ_f = sudut sayap depan (0° - 30°)
  
  Cl_front += δ_f × Cl_gain_per_deg_front
  Cd += δ_f × Cd_gain_per_deg_front
  
  Cl_gain_per_deg_front ≈ 0.08 - 0.15 per derajat
  Cd_gain_per_deg_front ≈ 0.005 - 0.010 per derajat

Rear Wing:
  δ_r = sudut sayap belakang (0° - 40°)
  
  Cl_rear += δ_r × Cl_gain_per_deg_rear
  Cd += δ_r × Cd_gain_per_deg_rear
  
  Cl_gain_per_deg_rear ≈ 0.10 - 0.20 per derajat
  Cd_gain_per_deg_rear ≈ 0.008 - 0.015 per derajat

Contoh Setup:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Track Type    │ δ_front │ δ_rear  │ Result
──────────────┼─────────┼─────────┼──────────────────
Monaco        │ 25°     │ 35°     │ Max downforce, slow
Monza         │ 5°      │ 10°     │ Min drag, fast straight
Spa           │ 15°     │ 20°     │ Medium
Oval          │ 10°     │ 25°     │ Asymmetric (rear bias)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.5 Algoritma Aero Map (Tier 1)

```
ALGORITMA: AeroModel_MapBased.Calculate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIER: 1 (GTX 10 / RX 500)
CPU COST: ~0.008 ms

INPUT:  vehicleState, windVector, setup
OUTPUT: F_drag, F_down_front, F_down_rear, F_side, M_yaw

1. Hitung kecepatan relatif:
   V_rel = vehicleVelocity - windVector
   V_mag = |V_rel|
   IF V_mag < 0.5: RETURN zeros

2. Hitung yaw angle:
   V_local = TransformToLocal(V_rel, vehicleRotation)
   β = arctan2(V_local.y, |V_local.x|)  // radians
   β_deg = β × RAD2DEG

3. Lookup yaw effects:
   yawData = YawMap.Sample(β_deg)
   // yawData: { Cd_mult, Cl_mult, Cs, Cyaw }

4. Lookup ride height effects:
   RH_f = vehicle.Suspension.FL.RideHeight
   RH_r = vehicle.Suspension.RR.RideHeight
   
   rhData = RideHeightMap.Sample(RH_f, RH_r)
   // rhData: { Cl_base, Cd_base }
   
   // Stall check
   IF RH_f < stallThreshold_f OR RH_r < stallThreshold_r:
       rhData.Cl *= stallClMultiplier
       rhData.Cd *= stallCdMultiplier

5. Calculate wing contributions:
   Cl_front_wing = setup.FrontWingAngle × Cl_gain_front
   Cl_rear_wing = setup.RearWingAngle × Cl_gain_rear
   Cd_wing = setup.FrontWingAngle × Cd_gain_front
           + setup.RearWingAngle × Cd_gain_rear

6. Apply DRS:
   IF drs_active:
       Cl_rear_wing *= drs_Cl_reduction     // ~0.40
       Cd_wing *= drs_Cd_reduction          // ~0.75

7. Calculate total coefficients:
   Cd_total = (rhData.Cd + Cd_wing) × yawData.Cd_mult
   Cl_front = (rhData.Cl × balance_front + Cl_front_wing) 
              × yawData.Cl_mult
   Cl_rear = (rhData.Cl × (1 - balance_front) + Cl_rear_wing)
             × yawData.Cl_mult
   Cs = yawData.Cs
   Cyaw = yawData.Cyaw

8. Calculate forces:
   q = 0.5 × airDensity × V_mag²
   
   F_drag = q × Cd_total × A_frontal
   F_down_front = q × Cl_front × A_ref
   F_down_rear = q × Cl_rear × A_ref
   F_side = q × Cs × A_side × sign(V_local.y)
   M_yaw = q × Cyaw × A_side × L_wheelbase × sign(β)

9. Apply aero damage:
   F_down_front *= (1.0 - aeroDamage_front × 0.35)
   F_down_rear *= (1.0 - aeroDamage_rear × 0.35)
   F_drag *= (1.0 + totalAeroDamage × 0.20)

10. Apply ke vehicle:
    vehicle.AddForce(-normalize(V_rel) × F_drag)
    vehicle.AddForceAtPosition(down × F_down_front, frontAxle)
    vehicle.AddForceAtPosition(down × F_down_rear, rearAxle)
    vehicle.AddForce(vehicleRight × F_side)
    vehicle.AddTorque(up × M_yaw)

RETURN all forces
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 4. SLIPSTREAM & WAKE TURBULENCE (Tier 1+)

### 4.1 Slipstream (Drafting)

```
Slipstream Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Saat mobil mengikuti mobil lain di belakang,
mobil belakang mendapat "slipstream" → drag berkurang.

Konsep:
  Mobil depan membuat "wake" di belakangnya.
  Di dalam wake, kecepatan udara lebih rendah
  dan turbulensi lebih tinggi.

Efek slipstream:
  - Drag mobil belakang BERKURANG (bagus untuk straight)
  - Downforce mobil belakang BERKURANG (buruk untuk cornering)
  - Turbulensi meningkat (mobil lebih sulit dikendalikan)

Model Wake (Simplified Cone):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Wake berbentuk kerucut di belakang mobil:

  Mobil depan
     ┌───┐
     │   │  →  Wake zone (expanding cone)
     └───┘
        ╲              ╱
         ╲            ╱    ← Wake boundary
          ╲          ╱
           ╲        ╱
            ╲      ╱
             ╲    ╱
              ╲  ╱
               ╲╱
               
  Wake length ≈ 5-10 × car length
  Wake width ≈ 1.5-2.5 × car width di ujung
  
  Di dalam wake:
    velocity_deficit = f(distance, lateral_offset)
    turbulence_intensity = f(distance)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.2 Algoritma Slipstream

```
ALGORITMA: AeroModel_Slipstream.Calculate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIER: 1+ (GTX 10+)
CPU COST: ~0.005 ms per pasangan mobil

INPUT:  egoVehicle, nearbyVehicles[]
OUTPUT: slipstreamMultiplier, turbulenceIntensity

1. UNTUK SETIAP mobil lain dalam jarak 50m:

   1a. Hitung jarak dan arah:
       toOther = otherVehicle.Position - egoVehicle.Position
       distance = |toOther|
       
       IF distance > wakeLength: CONTINUE  // Terlalu jauh
       
       // Hanya jika mobil lain di DEPAN
       forwardDot = dot(normalize(toOther), egoVehicle.Forward)
       IF forwardDot < 0.3: CONTINUE  // Bukan di depan

   1b. Hitung lateral offset:
       lateralOffset = |cross(normalize(toOther), egoVehicle.Forward)|
       lateralOffset_m = lateralOffset × distance
       
       // Wake width bertambah dengan jarak
       wakeWidth = carWidth × (1.0 + distance × wakeExpansionRate)
       
       IF lateralOffset_m > wakeWidth: CONTINUE  // Di luar wake

   1c. Hitung velocity deficit:
       // Semakin dekat, semakin besar efek
       distanceFactor = 1.0 - (distance / wakeLength)
       distanceFactor = pow(distanceFactor, 1.5)  // Non-linear
       
       // Semakin di tengah wake, semakin besar efek
       lateralFactor = 1.0 - (lateralOffset_m / wakeWidth)
       lateralFactor = pow(lateralFactor, 2.0)
       
       // Combined
       velocityDeficit = maxDeficit × distanceFactor × lateralFactor
       
       // maxDeficit ≈ 0.20-0.40 (20-40% kecepatan berkurang)

   1d. Hitung turbulence:
       turbulence = baseTurbulence × distanceFactor
       // turbulence ≈ 0.1-0.5 (10-50% additional turbulence)

   1e. Accumulate (jika multiple mobil di depan):
       totalVelocityDeficit += velocityDeficit
       totalTurbulence = max(totalTurbulence, turbulence)

2. Apply slipstream:
   // Drag berkurang
   slipstreamDragMult = 1.0 - totalVelocityDeficit × dragReductionFactor
   // dragReductionFactor ≈ 0.6-0.8
   
   // Downforce berkurang (dirty air)
   slipstreamDownMult = 1.0 - totalVelocityDeficit × downforceReductionFactor
   // downforceReductionFactor ≈ 0.4-0.6
   
   F_drag *= slipstreamDragMult
   F_down_front *= slipstreamDownMult
   F_down_rear *= slipstreamDownMult

3. Apply turbulence (random perturbation):
   turbForce = turbulence × q × A_frontal
   F_turb_x = perlinNoise(time × freq1) × turbForce × 0.3
   F_turb_y = perlinNoise(time × freq2 + 100) × turbForce × 0.5
   F_turb_z = perlinNoise(time × freq3 + 200) × turbForce × 0.2
   
   vehicle.AddForce(F_turb_x, F_turb_y, F_turb_z)

4. RETURN slipstreamDragMult, slipstreamDownMult, totalTurbulence
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.3 Wake Visualization Data (untuk Visual FX di Tier Tinggi)

```
Wake Data untuk Visual Effects:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Setiap kendaraan menyimpan wake data:

struct WakeData {
    float3 Position;          // Posisi mobil
    float3 Direction;         // Arah wake (ke belakang)
    float  Width;             // Lebar wake saat ini
    float  Length;            // Panjang wake
    float  VelocityDeficit;   // Kekurangan kecepatan di wake
    float  Turbulence;        // Intensitas turbulensi
    float  Age;               // Umur wake (untuk fade)
}

Wake trail di-update setiap frame:
  - Spawn wake segment di belakang mobil
  - Wake segment meluas seiring waktu
  - Wake segment fade setelah 2-3 detik
  - Digunakan untuk:
    → Slipstream calculation (gameplay)
    → Particle effects (visual, Tier 3+)
    → Rain spray direction (visual, Tier 2+)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 5. GROUND EFFECT (Tier 2+)

### 5.1 Ground Effect Model

```
Ground Effect Aerodynamics:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ground effect = fenomena aerodinamika di mana
dekatnya mobil ke tanah meningkatkan downforce.

Mekanisme:
  1. Venturi Effect di bawah mobil:
     Udara dipercepat di bawah mobil (area sempit)
     → Tekanan turun → Suction ke bawah
     
  2. Diffuser di belakang:
     Udara diperlambat & diekspansi
     → Pressure recovery → Additional suction
     
  3. Side skirts / floor edge:
     Mencegah udara masuk dari samping
     → Menjaga low pressure di bawah

Model Ground Effect:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
F_ground_effect = q × Cl_ge × A_floor × f(RH, pitch, yaw)

f(RH, pitch, yaw) = f_height(RH) × f_pitch(pitch) × f_yaw(yaw)

f_height(RH):
  // Normalisasi ride height
  RH_norm = RH / RH_reference
  
  // Ground effect meningkat saat RH turun
  IF RH_norm > 1.5:
      f_height = 0.3  // Terlalu tinggi, ground effect minimal
  ELSE IF RH_norm > 0.5:
      f_height = 1.0 + (1.5 - RH_norm) × 1.5  // Linear increase
  ELSE IF RH_norm > 0.2:
      f_height = 2.5  // Peak ground effect
  ELSE:
      f_height = 2.5 × (RH_norm / 0.2)  // Stall approaching
      // Terlalu rendah → flow separation → stall

f_pitch(pitch):
  // Pitch = perbedaan ride height depan/belakang
  pitch = (RH_rear - RH_front) / wheelbase
  
  // Positive pitch (rake) → rear ground effect naik
  f_pitch_front = 1.0 - pitch × pitchSensitivityFront
  f_pitch_rear = 1.0 + pitch × pitchSensitivityRear
  
  // Pitch sensitivity ≈ 2.0-5.0 per radian

f_yaw(yaw):
  // Saat yaw, ground effect berkurang
  // (udara masuk dari samping, merusak seal)
  f_yaw = 1.0 - |yaw| × yawGroundEffectLoss
  // yawGroundEffectLoss ≈ 0.3-0.8 per radian

Diffuser Stall:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  IF pitch > pitch_stall_threshold:
      // Diffuser stall: flow separation di diffuser
      F_ground_effect *= 0.4  // Downforce drop drastis
      Cd *= 1.2  // Drag naik
      diffuserStalled = true
      
  // Pitch stall threshold ≈ 0.03-0.05 radian
  // (sekitar 2-3 derajat)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.2 Algoritma Ground Effect

```
ALGORITMA: AeroModel_GroundEffect.Calculate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
TIER: 2+ (RTX 20+)
CPU COST: ~0.005 ms

INPUT:  vehicleState, setup
OUTPUT: F_ground_front, F_ground_rear, diffuserStalled

1. Hitung ride heights:
   RH_f = (Susp_FL.RideHeight + Susp_FR.RideHeight) / 2
   RH_r = (Susp_RL.RideHeight + Susp_RR.RideHeight) / 2

2. Hitung pitch:
   pitch = (RH_r - RH_f) / wheelbase  // radian

3. Hitung yaw:
   β = vehicleState.YawAngle  // radian

4. Hitung height factor:
   RH_norm_f = RH_f / RH_reference
   RH_norm_r = RH_r / RH_reference
   
   f_height_f = CalculateHeightFactor(RH_norm_f)
   f_height_r = CalculateHeightFactor(RH_norm_r)

5. Hitung pitch factor:
   f_pitch_f = 1.0 - pitch × pitchSensFront
   f_pitch_r = 1.0 + pitch × pitchSensRear
   
   f_pitch_f = clamp(f_pitch_f, 0.0, 2.0)
   f_pitch_r = clamp(f_pitch_r, 0.0, 2.5)

6. Hitung yaw factor:
   f_yaw = 1.0 - |β| × yawGELoss
   f_yaw = clamp(f_yaw, 0.2, 1.0)

7. Hitung ground effect force:
   q = 0.5 × airDensity × V_mag²
   
   F_ge_front = q × Cl_ge_front × A_floor_front 
                × f_height_f × f_pitch_f × f_yaw
   
   F_ge_rear = q × Cl_ge_rear × A_floor_rear
               × f_height_r × f_pitch_r × f_yaw

8. Diffuser stall check:
   IF pitch > diffuserStallPitch:
       F_ge_rear *= diffuserStallMultiplier  // 0.3-0.5
       diffuserStalled = true
   ELSE:
       diffuserStalled = false

9. Porosity effect (brake ducts, radiator):
   // Opening brake ducts mengurangi ground effect
   F_ge_front *= (1.0 - brakeDuctOpening × 0.10)
   F_ge_rear *= (1.0 - brakeDuctOpening × 0.05)

10. RETURN F_ge_front, F_ge_rear, diffuserStalled
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 6. CROSSWIND MODEL (Tier 1+)

### 6.1 Crosswind Simulation

```
Crosswind Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Angin samping mempengaruhi:
  1. Side force (mendorong mobil ke samping)
  2. Yaw moment (memutar mobil)
  3. Perubahan Cd dan Cl (flow angle berubah)
  4. Turbulensi (gusts)

Wind Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
V_wind = V_mean + V_gust + V_turbulence

V_mean = windSpeed × windDirection
  // Konstanta dari weather system
  
V_gust = gustAmplitude × sin(time × gustFrequency + phase)
  // Periodic gusts
  
V_turbulence = perlinNoise3D(position × scale, time × freq) 
               × turbulenceIntensity
  // Spatial & temporal turbulence

Effective wind angle:
  β_wind = arctan2(V_wind.y, V_vehicle.x)
  
  // Sudut efektif antara angin dan arah mobil
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Side Force dari Crosswind:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
F_side = 0.5 × ρ × V_rel² × Cs(β_wind) × A_side

di mana:
  V_rel = |V_vehicle - V_wind|  // Kecepatan relatif
  Cs(β_wind) = side force coefficient dari lookup table
  A_side = luas samping mobil (~4-6 m²)

Yaw Moment:
  M_yaw = 0.5 × ρ × V_rel² × Cyaw(β_wind) × A_side × L_wheelbase
  
  // Center of pressure biasanya di depan CoG
  // → Crosswind cenderung mendorong hidung mobil
  
  x_CoP_offset = (x_CoP - x_CoG)  // meter
  M_yaw = F_side × x_CoP_offset

Gust Effect:
  Saat gust datang tiba-tiba:
  → Side force spike
  → Driver harus koreksi steering
  → Bisa menyebabkan spin jika terlalu kuat
  
  Gust magnitude:
    Light: 2-5 m/s
    Moderate: 5-10 m/s
    Strong: 10-20 m/s
    Storm: 20+ m/s
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 7. DRS & ADJUSTABLE AERO (Tier 1+)

### 7.1 DRS (Drag Reduction System)

```
DRS Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

DRS = mekanisme yang mengurangi drag di straight
dengan mengubah sudut sayap belakang.

DRS Activation Conditions:
  - Hanya di zona DRS yang ditentukan
  - Hanya jika gap < 1 detik dari mobil depan (racing rule)
  - Tidak saat braking atau cornering
  - Kecepatan minimum (biasanya > 100 km/h)

DRS Effect:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
IF drs_active:
    // Rear wing flap opens (angle berkurang)
    δ_rear_effective = δ_rear_base - drs_angle_reduction
    
    // drs_angle_reduction ≈ 10-20°
    
    // Downforce rear berkurang drastis
    Cl_rear *= (1.0 - drs_downforce_reduction)
    // drs_downforce_reduction ≈ 0.40-0.60
    
    // Drag berkurang
    Cd *= (1.0 - drs_drag_reduction)
    // drs_drag_reduction ≈ 0.20-0.30
    
    // Balance shift ke depan
    // → Mobil lebih "pointy" tapi kurang stabil di rear
    
    // Transition time
    drs_transition_time = 0.3  // detik
    // DRS tidak instan, ada delay buka/tutup

DRS Transition:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
IF drs_requested AND NOT drs_active:
    drs_timer += dt
    drs_progress = clamp(drs_timer / drs_transition_time, 0, 1)
    
    // Smooth transition (ease-in-out)
    drs_progress_smooth = smoothstep(0, 1, drs_progress)
    
    // Interpolate coefficients
    Cl_rear_actual = lerp(Cl_rear_closed, Cl_rear_open, drs_progress_smooth)
    Cd_actual = lerp(Cd_closed, Cd_open, drs_progress_smooth)

IF drs_deactivated AND drs_active:
    // Reverse transition
    drs_timer -= dt
    // Same interpolation in reverse
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 8. AERO DAMAGE INTEGRATION (dari Deformation System)

### 8.1 Damage → Aero Coupling

```
Aero Damage Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Kerusakan body (dari hybrid deformation engine)
mempengaruhi aerodinamika:

1. FRONT DAMAGE:
   frontDamage = GetZoneDamage("FrontBumper", "Hood", "Fender_FL", "Fender_FR")
   
   // Front downforce berkurang
   Cl_front *= (1.0 - frontDamage × 0.35)
   
   // Drag bertambah (body tidak smooth lagi)
   Cd *= (1.0 + frontDamage × 0.12)
   
   // Balance shift ke belakang
   balance_front -= frontDamage × 0.05

2. REAR DAMAGE:
   rearDamage = GetZoneDamage("RearBumper", "Trunk", "Spoiler")
   
   // Rear downforce berkurang
   Cl_rear *= (1.0 - rearDamage × 0.40)
   
   // Drag bertambah
   Cd *= (1.0 + rearDamage × 0.10)
   
   // Balance shift ke depan (oversteer tendency)
   balance_front += rearDamage × 0.05

3. WING DAMAGE:
   IF frontWingDamaged:
       // Wing endplate rusak → vortex hilang
       Cl_front_wing *= (1.0 - wingDamage × 0.5)
       
       // Drag berubah (wing angle tidak optimal)
       Cd_wing *= (1.0 + wingDamage × 0.15)
   
   IF rearWingDamaged:
       Cl_rear_wing *= (1.0 - wingDamage × 0.5)
       Cd_wing *= (1.0 + wingDamage × 0.15)
       
       // DRS mungkin tidak bisa aktif
       IF wingDamage > 0.5:
           drs_available = false

4. UNDERBODY DAMAGE:
   underbodyDamage = GetZoneDamage("Floor", "Diffuser")
   
   // Ground effect berkurang
   Cl_ge *= (1.0 - underbodyDamage × 0.40)
   
   // Diffuser efficiency turun
   diffuserEfficiency *= (1.0 - underbodyDamage × 0.30)

5. SIDE DAMAGE:
   sideDamage = GetZoneDamage("Door_L", "Door_R", "SidePod")
   
   // Side sensitivity meningkat
   Cs *= (1.0 + sideDamage × 0.30)
   
   // Crosswind lebih berpengaruh
   crosswindSensitivity *= (1.0 + sideDamage × 0.25)

6. MISSING PARTS:
   FOR each missingPart:
       // Part yang lepas mengubah aero
       Cd += missingPart.DragContribution
       Cl_front += missingPart.ClFrontContribution
       Cl_rear += missingPart.ClRearContribution
       
       // Contoh: bumper lepas → Cd +0.05, Cl_front -0.1
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 9. ADAPTIVE QUALITY SYSTEM (GPU Detection)

### 9.1 GPU Detection Algorithm

```
ALGORITMA: GPUDetect.DetectAndClassify
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Dipanggil saat game startup (sebelum masuk gameplay)

1. Ambil informasi GPU:
   gpuName = SystemInfo.graphicsDeviceName
   gpuVendor = SystemInfo.graphicsDeviceVendor
   vram = SystemInfo.graphicsMemorySize  // MB
   
   // Unity API:
   // SystemInfo.graphicsDeviceName → "NVIDIA GeForce GTX 750 Ti"
   // SystemInfo.graphicsMemorySize → 2048 (MB)
   // SystemInfo.supportedRenderTargetCount
   // SystemInfo.supportsComputeShaders
   // SystemInfo.maxTextureSize

2. Parse GPU name untuk identifikasi:
   gpuFamily = ParseGPUFamily(gpuName)
   
   // NVIDIA:
   //   "GeForce GTX 7" → Family: Kepler/Maxwell, Gen: 7
   //   "GeForce GTX 10" → Family: Pascal, Gen: 10
   //   "GeForce RTX 20" → Family: Turing, Gen: 20
   //   "GeForce RTX 30" → Family: Ampere, Gen: 30
   //   "GeForce RTX 40" → Family: Ada Lovelace, Gen: 40
   
   // AMD:
   //   "Radeon RX 500" → Family: Polaris, Gen: 5
   //   "Radeon RX 5000" → Family: RDNA, Gen: 5000
   //   "Radeon RX 6000" → Family: RDNA2, Gen: 6000
   //   "Radeon RX 7000" → Family: RDNA3, Gen: 7000
   
   // Intel:
   //   "Intel UHD" → Family: UHD, Gen: integrated
   //   "Intel Arc" → Family: Arc, Gen: discrete

3. Score GPU berdasarkan multiple factors:
   score = 0
   
   // VRAM
   IF vram >= 12288: score += 40      // 12GB+
   ELSE IF vram >= 8192: score += 30   // 8GB+
   ELSE IF vram >= 4096: score += 20   // 4GB+
   ELSE IF vram >= 2048: score += 10   // 2GB+
   ELSE: score += 5                     // <2GB
   
   // GPU generation
   score += gpuGenerationScore
   
   // Feature support
   IF SystemInfo.supportsComputeShaders: score += 10
   IF SystemInfo.supportedRenderTargetCount >= 8: score += 5
   IF SystemInfo.maxTextureSize >= 16384: score += 5
   
   // Benchmark (optional, first-run)
   IF hasBenchmarkData:
       score += benchmarkScore

4. Classify tier:
   IF score >= 90: tier = 4   // Ultra (RTX 40, RX 7000+)
   ELSE IF score >= 70: tier = 3  // High (RTX 30, RX 6000)
   ELSE IF score >= 50: tier = 2  // Medium (RTX 20, RX 5000)
   ELSE IF score >= 30: tier = 1  // Low (GTX 10, RX 500)
   ELSE: tier = 0              // Minimum (GTX 7, Integrated)

5. RETURN tier, gpuName, vram, score
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 9.2 Feature Matrix per Tier

```
Adaptive Aero Feature Matrix:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Feature                    │ T0  │ T1  │ T2  │ T3  │ T4
                           │GTX7 │GTX10│RTX20│RTX30│RTX40
───────────────────────────┼─────┼─────┼─────┼─────┼─────
Basic Drag/Downforce       │ ✅  │ ✅  │ ✅  │ ✅  │ ✅
Fixed Cd/Cl               │ ✅  │ ─   │ ─   │ ─   │ ─
Yaw Angle Dependency       │ ─   │ ✅  │ ✅  │ ✅  │ ✅
Ride Height Dependency     │ ─   │ ✅  │ ✅  │ ✅  │ ✅
Wing Angle Adjustment      │ ─   │ ✅  │ ✅  │ ✅  │ ✅
DRS Simulation             │ ─   │ ✅  │ ✅  │ ✅  │ ✅
Slipstream (Simple)        │ ─   │ ✅  │ ✅  │ ✅  │ ✅
Slipstream (Wake Model)    │ ─   │ ─   │ ✅  │ ✅  │ ✅
Crosswind (Constant)       │ ─   │ ✅  │ ✅  │ ✅  │ ✅
Crosswind (Gusts)          │ ─   │ ─   │ ✅  │ ✅  │ ✅
Crosswind (Turbulence)     │ ─   │ ─   │ ─   │ ✅  │ ✅
Ground Effect              │ ─   │ ─   │ ✅  │ ✅  │ ✅
Ground Effect (Pitch Sens) │ ─   │ ─   │ ─   │ ✅  │ ✅
Diffuser Stall             │ ─   │ ─   │ ─   │ ✅  │ ✅
Aero Damage Coupling       │ ─   │ ─   │ ✅  │ ✅  │ ✅
Brake Duct Trade-off       │ ─   │ ─   │ ✅  │ ✅  │ ✅
Tire Wake Interaction      │ ─   │ ─   │ ─   │ ✅  │ ✅
Wake Visualization (VFX)   │ ─   │ ─   │ ─   │ ─   │ ✅
Air Flow Particles (VFX)   │ ─   │ ─   │ ─   │ ─   │ ✅
Semi-CFD (Panel Method)    │ ─   │ ─   │ ─   │ ─   │ ✅
Rain Spray Direction       │ ─   │ ─   │ ─   │ ✅  │ ✅
Debris Wind Interaction    │ ─   │ ─   │ ─   │ ✅  │ ✅
Heat Shimmer VFX           │ ─   │ ─   │ ─   │ ─   │ ✅
───────────────────────────┼─────┼─────┼─────┼─────┼─────
CPU Budget (aero)          │0.002│0.008│0.020│0.035│0.060
ms per frame               │     │     │     │     │
───────────────────────────┼─────┼─────┼─────┼─────┼─────
Aero Map Resolution        │ ─   │ 8×8 │16×16│32×32│64×64
Wake Segments              │ ─   │ 10  │ 20  │ 40  │ 80
Crosswind Samples          │ ─   │ 1   │ 4   │ 8   │ 16
───────────────────────────┼─────┼─────┼─────┼─────┼─────
Visual FX Budget           │0 ms │0 ms │2 ms │5 ms │10 ms
(particles, trails)        │     │     │     │     │
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 9.3 Aero Quality Manager

```csharp
public class AeroQualityManager
{
    public enum AeroTier { Minimum = 0, Low = 1, Medium = 2, High = 3, Ultra = 4 }
    
    private AeroTier currentTier;
    private IAeroModel activeModel;
    
    public void Initialize()
    {
        // 1. Detect GPU
        GPUDetectResult gpu = GPUDetect.DetectAndClassify();
        
        // 2. Set tier
        currentTier = (AeroTier)gpu.Tier;
        
        // 3. Create appropriate aero model
        switch (currentTier)
        {
            case AeroTier.Minimum:
                activeModel = new AeroModel_Basic();
                break;
            case AeroTier.Low:
                activeModel = new AeroModel_MapBased();
                break;
            case AeroTier.Medium:
                activeModel = new AeroModel_Enhanced();
                // + Ground effect
                // + Wake model
                // + Gust crosswind
                break;
            case AeroTier.High:
                activeModel = new AeroModel_FullSimulation();
                // + Diffuser stall
                // + Tire wake
                // + Full turbulence
                break;
            case AeroTier.Ultra:
                activeModel = new AeroModel_SemiCFD();
                // + Panel method
                // + Full VFX integration
                break;
        }
        
        // 4. Initialize visual effects berdasarkan tier
        AeroVisualFX.Initialize(currentTier);
        
        // 5. Log
        Debug.Log($"GPU: {gpu.Name} | VRAM: {gpu.VRAM}MB | " +
                  $"Aero Tier: {currentTier} | Model: {activeModel.GetType().Name}");
    }
    
    public void CalculateAero(VehicleState vehicle, float dt)
    {
        activeModel.Calculate(vehicle, dt);
    }
    
    // Runtime quality adjustment (jika FPS drop)
    public void AdjustForPerformance(float currentFPS)
    {
        if (currentFPS < 30 && currentTier > AeroTier.Minimum)
        {
            currentTier--;
            RebuildModel();
            Debug.LogWarning($"FPS drop! Aero tier reduced to {currentTier}");
        }
        else if (currentFPS > 55 && currentTier < detectedMaxTier)
        {
            currentTier++;
            RebuildModel();
        }
    }
}

// Interface untuk semua aero models
public interface IAeroModel
{
    void Calculate(VehicleState vehicle, float dt);
    AeroForces GetForces();
    void Initialize(AeroConfig config);
}
```

### 9.4 GPU Detection Code (Unity)

```csharp
public static class GPUDetect
{
    public struct GPUDetectResult
    {
        public string Name;
        public string Vendor;
        public int VRAM_MB;
        public int Tier;
        public int Score;
        public bool SupportsCompute;
        public int MaxTextureSize;
    }
    
    public static GPUDetectResult DetectAndClassify()
    {
        GPUDetectResult result = new GPUDetectResult();
        
        result.Name = SystemInfo.graphicsDeviceName;
        result.Vendor = SystemInfo.graphicsDeviceVendor;
        result.VRAM_MB = SystemInfo.graphicsMemorySize;
        result.SupportsCompute = SystemInfo.supportsComputeShaders;
        result.MaxTextureSize = SystemInfo.maxTextureSize;
        
        int score = 0;
        
        // VRAM scoring
        if (result.VRAM_MB >= 16384) score += 50;
        else if (result.VRAM_MB >= 12288) score += 40;
        else if (result.VRAM_MB >= 8192) score += 30;
        else if (result.VRAM_MB >= 4096) score += 20;
        else if (result.VRAM_MB >= 2048) score += 10;
        else score += 5;
        
        // GPU family scoring
        string nameLower = result.Name.ToLower();
        
        if (nameLower.Contains("rtx 40") || nameLower.Contains("rx 7900") 
            || nameLower.Contains("rx 7800") || nameLower.Contains("rx 7600"))
        {
            score += 50;  // Latest gen
        }
        else if (nameLower.Contains("rtx 30") || nameLower.Contains("rx 6900") 
                 || nameLower.Contains("rx 6800") || nameLower.Contains("rx 6700"))
        {
            score += 40;
        }
        else if (nameLower.Contains("rtx 20") || nameLower.Contains("rx 5700") 
                 || nameLower.Contains("rx 5600"))
        {
            score += 30;
        }
        else if (nameLower.Contains("gtx 16") || nameLower.Contains("gtx 10") 
                 || nameLower.Contains("rx 590") || nameLower.Contains("rx 580"))
        {
            score += 20;
        }
        else if (nameLower.Contains("gtx 9") || nameLower.Contains("gtx 7") 
                 || nameLower.Contains("rx 480") || nameLower.Contains("rx 470"))
        {
            score += 10;
        }
        else
        {
            score += 5;  // Unknown / integrated
        }
        
        // Feature scoring
        if (result.SupportsCompute) score += 10;
        if (SystemInfo.supportedRenderTargetCount >= 8) score += 5;
        if (result.MaxTextureSize >= 16384) score += 5;
        
        // Classify
        if (score >= 100) result.Tier = 4;
        else if (score >= 80) result.Tier = 3;
        else if (score >= 60) result.Tier = 2;
        else if (score >= 35) result.Tier = 1;
        else result.Tier = 0;
        
        result.Score = score;
        return result;
    }
}
```

---

## 10. VISUAL EFFECTS AERODINAMIKA (Tier-Based)

### 10.1 Air Flow Visualization

```
Air Flow VFX per Tier:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

TIER 0-1: Tidak ada visual aero
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Tidak ada particle effects untuk aero.
  Hanya force calculation (invisible).

TIER 2: Basic Air Flow
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - Speed lines saat V > 200 km/h (subtle)
  - Dust/debu saat dekat tanah
  - Rain spray sederhana di belakang roda
  
  Particle budget: 200 particles
  Update: 30 Hz

TIER 3: Enhanced Air Flow
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - Air flow streamlines di sekitar mobil
  - Wake turbulence visualization (di belakang mobil)
  - Tire spray (air dari ban saat hujan)
  - Brake dust particles
  - Heat shimmer dari exhaust (subtle)
  - Aero flow-vis paint (untuk testing mode)
  
  Particle budget: 2.000 particles
  Update: 60 Hz

TIER 4: Full Aero Visualization
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - Full streamline visualization (CFD-style)
  - Vortex visualization dari wing endplates
  - Ground effect flow visualization
  - Diffuser flow visualization
  - Wake turbulence (full 3D)
  - Tire wake interaction visible
  - Heat shimmer (exhaust, brakes)
  - Rain spray (full simulation, direction dari aero)
  - Aero flow-vis paint (real-time)
  - Wind tunnel mode (untuk testing)
  
  Particle budget: 10.000+ particles
  Update: 60-120 Hz
  GPU Compute: Ya (particle simulation di GPU)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 10.2 Algoritma Aero Visual FX

```
ALGORITMA: AeroVisualFX.Update
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  vehicleState, aeroForces, tier, dt
OUTPUT: updated particle systems

IF tier >= 3:  // HIGH

   1. Streamline Particles:
      // Spawn streamlines di depan mobil
      spawnRate = vehicleSpeed × streamlineSpawnFactor
      
      FOR each new particle:
          position = vehicleFront + randomOffset
          velocity = -vehicleVelocity × 0.8  // Relative wind
          lifetime = 1.0-2.0 seconds
          
      // Update: particles mengikuti flow field
      FOR each particle:
          // Defleksi berdasarkan aero surface terdekat
          nearestSurface = FindNearestAeroSurface(particle.pos)
          IF nearestSurface.distance < influenceRadius:
              // Deflect particle along surface
              particle.vel += surfaceNormal × deflectionForce
              particle.vel = projectOnSurface(particle.vel)
          
          // Wake influence
          FOR each wake in activeWakes:
              IF particle.InWake(wake):
                  particle.vel *= (1.0 - wake.VelocityDeficit)
                  particle.vel += turbulenceNoise × wake.Turbulence

   2. Vortex Visualization:
      // Spawn vortex particles dari wing endplates
      IF frontWingAngle > 5:
          SpawnVortexParticles(
              position: frontWingEndplate_L,
              strength: Cl_front × vortexStrengthFactor,
              direction: vehicleForward + vehicleRight × 0.3
          )
          SpawnVortexParticles(
              position: frontWingEndplate_R,
              strength: Cl_front × vortexStrengthFactor,
              direction: vehicleForward - vehicleRight × 0.3
          )

   3. Tire Spray (Rain):
      IF isRaining:
          FOR each wheel:
              sprayDirection = -wheelVelocity × 0.5 
                               + wheelNormal × sprayLiftFactor
              // Spray direction dipengaruhi aero
              sprayDirection += aeroDownforceDirection × 0.2
              
              SpawnSprayParticles(
                  position: wheel.ContactPatch,
                  direction: sprayDirection,
                  count: vehicleSpeed × sprayRate
              )

IF tier >= 4:  // ULTRA

   4. Ground Effect Flow:
      // Particles di bawah mobil, menunjukkan suction
      SpawnUndertrayParticles(
          position: vehicleFloor - Vector3.up × 0.05,
          velocity: vehicleForward × vehicleSpeed × 1.2,
          // Faster than vehicle speed (venturi acceleration)
          color: based on pressure (blue = low pressure)
      )

   5. Diffuser Flow:
      // Particles keluar dari diffuser
      SpawnDiffuserParticles(
          position: vehicleRear - Vector3.up × 0.1,
          direction: vehicleForward × 0.7 + Vector3.up × 0.3,
          // Upwash dari diffuser
          spread: diffuserAngle
      )

   6. Wind Tunnel Mode (Testing):
      IF windTunnelMode:
          // Full CFD-style visualization
          // Grid of streamlines
          // Color-coded by velocity/pressure
          // Interactive: user bisa rotate camera
          UpdateWindTunnelVisualization(vehicleState)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 11. ALGORITMA UTAMA: Full Aero Pipeline

```
ALGORITMA: AeroSystem.MainUpdate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Dipanggil setiap FixedUpdate

1. Get current tier:
   tier = AeroQualityManager.CurrentTier

2. Calculate relative wind:
   V_rel = vehicleVelocity - windVector
   V_mag = |V_rel|
   
   IF V_mag < 0.5:
       RETURN  // No significant aero forces

3. Calculate dynamic pressure:
   q = 0.5 × airDensity × V_mag²

4. TIER 0 (Basic):
   IF tier == 0:
       F_drag = q × Cd_fixed × A_frontal × normalize(V_rel)
       F_down = q × Cl_fixed × A_planform × balance
       GOTO step 9

5. TIER 1+ (Map-Based):
   β = CalculateYawAngle(V_rel, vehicleRotation)
   
   yawData = YawMap.Sample(β)
   rhData = RideHeightMap.Sample(RH_front, RH_rear)
   wingData = CalculateWingEffects(wingAngles)
   drsData = CalculateDRS(drsActive, drsProgress)
   
   Cd = (rhData.Cd + wingData.Cd) × yawData.Cd_mult × drsData.Cd_mult
   Cl_f = (rhData.Cl × balance + wingData.Cl_f) × yawData.Cl_mult
   Cl_r = (rhData.Cl × (1-balance) + wingData.Cl_r) × yawData.Cl_mult
   Cs = yawData.Cs

6. TIER 2+ (Enhanced):
   // Slipstream
   slipData = CalculateSlipstream(vehicle, nearbyVehicles)
   Cd *= slipData.DragMultiplier
   Cl_f *= slipData.DownforceMultiplier
   Cl_r *= slipData.DownforceMultiplier
   
   // Ground effect
   geData = CalculateGroundEffect(RH_f, RH_r, pitch, β)
   Cl_f += geData.FrontContribution
   Cl_r += geData.RearContribution
   
   // Crosswind gusts
   windGust = CalculateWindGusts(time, position)
   V_rel += windGust

7. TIER 3+ (Full Simulation):
   // Tire wake interaction
   tireWakeEffect = CalculateTireWake(vehicle)
   Cl_r *= tireWakeEffect.RearWingEfficiency
   
   // Diffuser stall
   IF geData.DiffuserStalled:
       Cl_r *= 0.5
       Cd *= 1.2
   
   // Brake duct trade-off
   brakeDuctEffect = CalculateBrakeDucts(ductOpening)
   Cl_f *= brakeDuctEffect.AeroTradeoff
   Cd *= brakeDuctEffect.DragPenalty

8. Apply aero damage:
   damageData = DamagePerformanceLink.GetAeroDamage()
   Cd *= (1.0 + damageData.DragIncrease)
   Cl_f *= (1.0 - damageData.FrontDownLoss)
   Cl_r *= (1.0 - damageData.RearDownLoss)
   Cs *= (1.0 + damageData.SideSensitivity)

9. Calculate final forces:
   F_drag = q × Cd × A_frontal
   F_down_f = q × Cl_f × A_ref
   F_down_r = q × Cl_r × A_ref
   F_side = q × Cs × A_side
   M_yaw = q × Cyaw × A_side × L

10. Apply to vehicle:
    vehicle.RigidBody.AddForce(-V_rel_dir × F_drag)
    vehicle.RigidBody.AddForceAtPosition(
        Vector3.down × F_down_f, frontAxlePos)
    vehicle.RigidBody.AddForceAtPosition(
        Vector3.down × F_down_r, rearAxlePos)
    vehicle.RigidBody.AddForce(vehicleRight × F_side)
    vehicle.RigidBody.AddTorque(Vector3.up × M_yaw)

11. Update visual FX:
    AeroVisualFX.Update(vehicle, aeroForces, tier, dt)

12. Log telemetry:
    Telemetry.LogAero(Cd, Cl_f, Cl_r, Cs, F_drag, 
                      F_down_f, F_down_r, F_side, β, 
                      slipstreamActive, drsActive)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 12. RINGKASAN RUMUS AERODINAMIKA

| Rumus | Formula | Keterangan |
|---|---|---|
| Dynamic Pressure | `q = 0.5·ρ·V²` | Tekanan dinamis |
| Drag | `F = q·Cd·A` | Tahanan udara |
| Downforce | `F = q·Cl·A` | Gaya tekan |
| Side Force | `F = q·Cs·A` | Gaya samping |
| Yaw Moment | `M = q·Cyaw·A·L` | Torsi yaw |
| Yaw Angle | `β = atan(Vy/Vx)` | Sudut yaw |
| Slipstream Deficit | `Δv = f(dist, lat)` | Kecepatan berkurang di wake |
| Ground Effect | `F = q·Cl_ge·A·f(RH,pitch,yaw)` | Suction bawah mobil |
| DRS Effect | `Cl' = Cl×(1-r), Cd' = Cd×(1-r)` | DRS reduction |
| Crosswind Force | `F = q·Cs(β)·A_side` | Angin samping |
| Aero Balance | `bal = F_front/(F_front+F_rear)` | Distribusi downforce |
| Wake Width | `w = w₀ + d·expansion_rate` | Lebar wake vs jarak |
| Gust Model | `V_g = A·sin(ωt+φ) + noise` | Angin gust |
| Pitch Sensitivity | `f = 1 ± pitch × sensitivity` | Efek rake |
| Stall Condition | `IF RH < threshold: Cl ×= 0.3` | Ground effect stall |

---

## 13. PANDUAN IMPLEMENTASI DENGAN QWEN CODE

```
PROMPT SEQUENCE untuk Aero System:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

STEP 1 - GPU Detection & Tier System:
"Buat GPUDetect class di Unity C# yang mendeteksi GPU
 menggunakan SystemInfo. Buat scoring system dan 
 klasifikasi tier (0-4). Buat AeroQualityManager yang
 memilih IAeroModel berdasarkan tier. Sertakan 
 runtime quality adjustment jika FPS drop."

STEP 2 - Basic Aero Model (Tier 0):
"Buat AeroModel_Basic : IAeroModel dengan drag dan
 downforce sederhana. Cd dan Cl konstanta. 
 Tidak ada yaw, ride height, atau wing.
 Target: < 0.002 ms per frame."

STEP 3 - Aero Map System (Tier 1):
"Buat AeroModel_MapBased : IAeroModel dengan:
 - Yaw angle lookup table (1D)
 - Ride height lookup table (2D, bilinear interpolation)
 - Wing angle effects
 - DRS simulation dengan transition time
 - Aero balance calculation
 Buat juga AeroMapData class untuk menyimpan lookup tables
 dan method Sample() dengan interpolasi."

STEP 4 - Slipstream & Wake:
"Buat SlipstreamSystem yang menghitung:
 - Wake zone detection (cone model)
 - Velocity deficit berdasarkan jarak & lateral offset
 - Turbulence intensity
 - Multiple vehicle support
 - Wake data structure untuk visual FX
 Target: < 0.005 ms per pasangan mobil."

STEP 5 - Ground Effect (Tier 2):
"Buat GroundEffectModel dengan:
 - Ride height sensitivity (height factor curve)
 - Pitch sensitivity (rake effect)
 - Yaw sensitivity (seal degradation)
 - Diffuser stall detection
 - Porosity effect (brake ducts)
 Integrasi dengan AeroModel_Enhanced."

STEP 6 - Crosswind (Tier 1-2):
"Buat CrosswindSystem dengan:
 - Mean wind + gusts + turbulence
 - Perlin noise untuk turbulence
 - Side force & yaw moment calculation
 - Wind direction visualization (optional)
 3 level: constant (T1), gusts (T2), turbulence (T3)."

STEP 7 - Aero Damage Integration:
"Buat AeroDamageCoupling yang menghubungkan
 DamagePerformanceLink dengan aero system:
 - Front/rear damage → Cl reduction
 - Wing damage → wing efficiency loss
 - Underbody damage → ground effect loss
 - Missing parts → aero changes
 - Side damage → crosswind sensitivity"

STEP 8 - Visual FX (Tier 3-4):
"Buat AeroVisualFX manager dengan:
 - Streamline particles (spawn, update, deflect)
 - Vortex particles dari wing endplates
 - Tire spray (rain)
 - Wake turbulence visualization
 - Ground effect flow (Tier 4)
 - Wind tunnel mode (Tier 4)
 Gunakan Unity Particle System + GPU instancing.
 Budget berdasarkan tier."

STEP 9 - Full Pipeline Integration:
"Integrasikan semua ke AeroSystem.MainUpdate():
 - Tier-based feature selection
 - Force calculation pipeline
 - Slipstream → Ground Effect → Crosswind → Damage
 - Apply ke vehicle rigid body
 - Visual FX update
 - Telemetry logging
 Target: < 0.06 ms di Tier 4, < 0.002 ms di Tier 0."

STEP 10 - Testing & Tuning Tools:
"Buat Unity Editor tools untuk aero tuning:
 - Aero curve visualizer (Cd/Cl vs speed, yaw, RH)
 - Wind tunnel mode (freeze vehicle, vary params)
 - Aero balance display (front/rear pie chart)
 - Slipstream visualization (debug draw wake zones)
 - GPU tier override (untuk testing di semua tiers)"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 14. RINGKASAN ARSITEKTUR AERO ADAPTIF

```
ADAPTIVE AERODYNAMICS ENGINE - RINGKASAN:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

STARTUP:
  GPU Detection → Score → Tier Classification → Model Selection

RUNTIME (per frame):
  ┌─────────────────────────────────────────────────────┐
  │  AeroQualityManager                                  │
  │  → Select features based on tier                     │
  └──────────────┬──────────────────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────────────────┐
  │  Relative Wind Calculation                           │
  │  V_rel = V_vehicle - V_wind                          │
  └──────────────┬──────────────────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────────────────┐
  │  TIER 0: Basic (Cd/Cl fixed)                         │
  │  TIER 1: + Yaw, RH, Wing, DRS, Slipstream           │
  │  TIER 2: + Ground Effect, Gusts, Wake Model         │
  │  TIER 3: + Diffuser Stall, Tire Wake, Turbulence    │
  │  TIER 4: + Semi-CFD, Full VFX, Wind Tunnel          │
  └──────────────┬──────────────────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────────────────┐
  │  Aero Damage Coupling                                │
  │  (dari Hybrid Deformation Engine)                    │
  └──────────────┬──────────────────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────────────────┐
  │  Force Application → Vehicle Rigid Body              │
  │  + Visual FX Update (tier-based)                     │
  │  + Telemetry Logging                                 │
  └─────────────────────────────────────────────────────┘

PERFORMANCE BUDGET:
  Tier 0 (GTX 7):    ~0.002 ms  ✅ Very light
  Tier 1 (GTX 10):   ~0.008 ms  ✅ Light
  Tier 2 (RTX 20):   ~0.020 ms  ✅ Moderate
  Tier 3 (RTX 30):   ~0.035 ms  ✅ Moderate
  Tier 4 (RTX 40):   ~0.060 ms  ✅ Acceptable
  
  Semua dalam budget untuk 60 FPS di GTX 7 series.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

Saya sarankan mulai dari **GPU Detection & Tier System (Step 1)** dan **Basic Aero Model (Step 2)** sebagai fondasi, lalu lanjut ke **Aero Map System (Step 3)** dan **Slipstream (Step 4)**.