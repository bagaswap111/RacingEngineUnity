# Sistem Simulasi Suspensi di Game Engine Kompleks
## Arsitektur Lengkap dari Arcade hingga Engineering-Grade Simulation

---

## 1. TINGKATAN KOMPLEKSITAS SIMULASI SUSPENSI

Game engine yang berbeda mensimulasikan suspensi pada level yang sangat berbeda. Berikut adalah spektrum lengkapnya:

```
Spektrum Kompleksitas Suspensi:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Level 0: ARCADE (Mario Kart, Need for Speed)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Spring sederhana (1 pegas per roda)
  DOF: 1 per roda (vertical bounce)
  Geometri: TIDAK ADA (roda selalu vertikal)
  Damper: Konstanta tetap
  CPU: ~0.01 ms
  Contoh: NFS, Forza Horizon (arcade mode)

Level 1: SIMCADE (Forza Motorsport, GRID)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Spring + Damper + Anti-roll bar
  DOF: 1 per roda + body roll/pitch
  Geometri: Simplified camber curve (lookup table)
  Damper: Linear (compression = rebound)
  Load Transfer: Simplified formula
  CPU: ~0.05 ms
  Contoh: Forza Motorsport, Project CARS 1

Level 2: SIMULATION (Assetto Corsa, rFactor 2, iRacing)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Multi-link kinematics + hydraulic dampers
  DOF: 6 body + 4 roda + steering
  Geometri: Full kinematic solver (instant center, roll center)
  Damper: Non-linear 4-way (LS/HS comp/reb)
  Load Transfer: Full dynamic calculation
  Bushing Compliance: Ya
  CPU: ~0.2-0.5 ms
  Contoh: Assetto Corsa, iRacing, rFactor 2

Level 3: ENGINEERING SIMULATION (BeamNG, CarSim, MSC Adams)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Model: Multi-body dynamics + FEA + hydraulic circuit
  DOF: 50-200+ per kendaraan
  Geometri: Full 3D kinematics dengan compliance
  Damper: Full hydraulic circuit simulation
  Bushing: Non-linear elastomer model
  Tire: FEM-based (bukan Pacejka)
  CPU: ~5-50 ms
  Contoh: BeamNG.drive, CarSim, MSC Adams
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Untuk game balap simulasi yang Anda buat, target kita adalah **Level 2** dengan beberapa elemen Level 3.

---

## 2. JENIS-JENIS SUSPENSI YANG HARUS DISIMULASIKAN

### 2.1 Klasifikasi Suspensi

```
Jenis Suspensi untuk Racing Games:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

INDEPENDENT SUSPENSION (Roda bergerak independen):
┌─────────────────────────────────────────────────────────────────┐
│ 1. DOUBLE WISHBONE (Double A-Arm)                               │
│    → Paling umum di racing (F1, GT, Formula)                    │
│    → 2 control arm (upper + lower)                              │
│    → Camber control sangat baik                                 │
│    → Kompleksitas: TINGGI                                       │
│                                                                 │
│ 2. MacPHERSON STRUT                                             │
│    → Umum di mobil FWD & road car                               │
│    → 1 lower arm + strut (spring+damper jadi satu)              │
│    → Camber control kurang baik                                 │
│    → Kompleksitas: SEDANG                                       │
│                                                                 │
│ 3. MULTI-LINK (5-link)                                          │
│    → Suspensi belakang modern                                   │
│    → 3-5 link per roda                                          │
│    → Kontrol geometri sangat fleksibel                          │
│    → Kompleksitas: SANGAT TINGGI                                │
│                                                                 │
│ 4. PUSHROD / PULLROD                                            │
│    → F1, prototype racing                                       │
│    → Spring/damper mounted inboard                              │
│    → Rocker arm / bell crank                                    │
│    → Kompleksitas: TINGGI (motion ratio variable)               │
│                                                                 │
│ 5. TRAILING ARM / SEMI-TRAILING                                 │
│    → Suspensi belakang sederhana                                │
│    → 1 arm per roda                                             │
│    → Kompleksitas: RENDAH-SEDANG                                │
└─────────────────────────────────────────────────────────────────┘

SOLID AXLE / LIVE AXLE (Roda terhubung rigid):
┌─────────────────────────────────────────────────────────────────┐
│ 6. SOLID AXLE (Beam Axle)                                       │
│    → Truck, off-road, drag racing                               │
│    → Kedua roda terhubung oleh satu axle                        │
│    → Camber selalu 0°                                           │
│    → Kompleksitas: RENDAH                                       │
│                                                                 │
│ 7. DE DION                                                      │
│    → Hybrid solid axle + independent                            │
│    → Roda terhubung tapi unsprung mass rendah                   │
│    → Kompleksitas: SEDANG                                       │
└─────────────────────────────────────────────────────────────────┘
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 2.2 Diagram Geometri Suspensi

```
DOUBLE WISHBONE (Tampak Depan):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        Chassis
    ═══════════════════
    ║               ║
    ║  Upper Arm    ║
    ║  (UCA)        ║
    ║───────●────────║  ← UCA outer pivot (ball joint)
    ║       │       ║         │
    ║       │       ║         │ Upright / Hub Carrier
    ║       │  ●────║───●     │ (menghubungkan roda)
    ║       │  │    ║   │     │
    ║       │  │    ║   │  ◉──┤  ← Wheel hub + bearing
    ║       │  │    ║   │     │
    ║───────●──┼────║───●     │
    ║  Lower Arm    ║         │
    ║  (LCA)        ║         │
    ║               ║         │
    ════════════════╝    ◉────┘  ← Wheel center
                         │
                    ┌────┴────┐
                    │  TIRE   │
                    └─────────┘

    Spring + Damper:
    Biasanya mounted dari LCA ke chassis
    (atau melalui pushrod ke rocker)

    Geometri yang terbentuk:
    - Instant Center (IC): perpotongan UCA & LCA extension
    - Roll Center: ditentukan oleh IC + wheel center
    - Camber: sudut roda terhadap vertikal
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

MacPHERSON STRUT (Tampak Depan):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        Chassis
    ═══════════════════
    ║     ┃         ║
    ║     ┃ Strut   ║  ← Strut (spring + damper
    ║     ┃ (upper  ║     jadi satu unit)
    ║     ┃ mount)  ║
    ║     ┃         ║
    ║     ●─────────║  ← Strut body (fixed angle)
    ║     ┃         ║
    ║     ┃         ║
    ║     ●─────●───║  ← Hub carrier
    ║           │   ║
    ║───────────●───║  ← Lower arm (LCA)
    ║               ║
    ════════════════╝
              ◉─────── ← Wheel center

    Karakteristik:
    - Camber berubah saat travel (kurang ideal)
    - Lebih sederhana, lebih ringan
    - Umum di mobil FWD road car
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

PUSHROD SUSPENSION (F1-style, Tampak Samping):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    Chassis                    Wheel
    ═══════                    │
    ║                          │
    ║    ┌──● Rocker arm       │
    ║    │   ╲                 │
    ║    │    ╲ Spring/Damper  │
    ║    │     ╲ (inboard)     │
    ║    │      ╲              │
    ║    │       ●─────────────●  ← Pushrod
    ║    │                     │
    ║    │              ●──────●  ← UCA
    ║    │              │      │
    ║    │              │      │
    ║    │              ●──────●  ← LCA
    ║                          │
    ═══════                    ◉

    Karakteristik:
    - Spring/damper INBOARD (di dalam chassis)
    - Pushrod mentransfer gerakan roda ke rocker
    - Motion ratio TIDAK 1:1 (variable)
    - Unsprung mass sangat rendah
    - Center of gravity lebih rendah
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 3. KOMPONEN SUSPENSI YANG DISIMULASIKAN

### 3.1 Daftar Komponen Lengkap

```
Komponen Suspensi untuk Simulasi Level 2:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

SPRING (Pegas):
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Spring Rate (k): N/mm atau lb/in                            │
│   - Free Length: panjang tanpa beban (mm)                       │
│   - Installed Length: panjang saat terpasang (mm)               │
│   - Motion Ratio: rasio wheel travel : spring travel            │
│                                                                  │
│ Tipe yang disimulasikan:                                         │
│   1. Linear: F = k · x                                          │
│   2. Progressive: F = k₁·x + k₂·x²                             │
│   3. Dual-Rate: k berubah di travel tertentu                    │
│   4. Tender Spring: spring tambahan untuk free play             │
└─────────────────────────────────────────────────────────────────┘

DAMPER / SHOCK ABSORBER (Peredam Kejut):
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Compression Damping (Low Speed & High Speed)                │
│   - Rebound Damping (Low Speed & High Speed)                    │
│   - Knee Velocity: batas LS/HS (mm/s)                           │
│   - Force-Velocity Curve: lookup table                          │
│                                                                  │
│ Model:                                                           │
│   Level 1: Linear damper (F = c · v)                            │
│   Level 2: 4-way non-linear (LS/HS comp/reb terpisah)          │
│   Level 3: Full hydraulic circuit simulation                    │
└─────────────────────────────────────────────────────────────────┘

ANTI-ROLL BAR (ARB / Sway Bar):
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - ARB Rate (k_arb): N·m/rad atau N/mm                        │
│   - ARB Ratio: lever arm ratio                                  │
│   - Preload: torsi awal                                         │
│                                                                  │
│ Fungsi:                                                          │
│   Menghubungkan roda kiri-kanan untuk                            │
│   mengurangi body roll saat cornering                            │
│                                                                  │
│ Model:                                                           │
│   F_arb = k_arb · (travel_left - travel_right) · ratio          │
│   Torsi ditransfer antar roda melalui torsion bar              │
└─────────────────────────────────────────────────────────────────┘

BUMP STOP / JOUNCE BUMPER:
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Engage Travel: titik mulai kontak (mm)                      │
│   - Stiffness Curve: kekakuan vs kompresi (non-linear)         │
│   - Length: panjang bump stop (mm)                              │
│                                                                  │
│ Model:                                                           │
│   F_stop = 0                           jika x < engage           │
│   F_stop = k_stop · (x - engage)²      jika x > engage          │
│   (Progressive, semakin keras semakin terkompresi)              │
└─────────────────────────────────────────────────────────────────┘

BUSHING (Karet Sendi):
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Radial Stiffness (N/mm)                                     │
│   - Axial Stiffness (N/mm)                                      │
│   - Rotational Stiffness (N·m/rad)                              │
│   - Hysteresis: energy loss saat loading/unloading              │
│                                                                  │
│ Fungsi:                                                          │
│   Compliance di pivot points → geometri berubah                 │
│   di bawah beban (bukan rigid sempurna)                         │
│                                                                  │
│ Level 2: Simplified compliance (offset small)                   │
│ Level 3: Full bushing model dengan hysteresis                   │
└─────────────────────────────────────────────────────────────────┘

UPRIGHT / HUB CARRIER:
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Upright Mass (kg) → bagian dari unsprung mass              │
│   - Steering Axis geometry (KPI, caster)                        │
│   - Wheel mounting offset                                       │
│                                                                  │
│ Fungsi:                                                          │
│   Menghubungkan roda ke control arms                            │
│   Menentukan steering axis & wheel geometry                     │
└─────────────────────────────────────────────────────────────────┘

STEERING LINKAGE:
┌─────────────────────────────────────────────────────────────────┐
│ Parameter:                                                       │
│   - Steering Ratio (rack travel : wheel angle)                  │
│   - Ackermann Geometry (%)                                      │
│   - Bump Steer Curve (toe change vs travel)                     │
│   - Steering Rack Compliance                                    │
│                                                                  │
│ Model:                                                           │
│   Wheel_steering = steer_input / steering_ratio                 │
│   + bump_steer(travel)                                          │
│   + compliance_steer(lateral_force)                             │
└─────────────────────────────────────────────────────────────────┘
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.2 Data Structure Lengkap

```csharp
// Konfigurasi suspensi per roda
public struct SuspensionConfig
{
    // === GEOMETRI ===
    public SuspensionType Type;           // DoubleWishbone, MacPherson, dll
    
    // Hardpoints (posisi dalam local space, meter)
    public float3 UCA_Front;              // Upper Control Arm - front mount
    public float3 UCA_Rear;               // Upper Control Arm - rear mount
    public float3 UCA_Outboard;           // UCA ball joint (ke upright)
    public float3 LCA_Front;              // Lower Control Arm - front mount
    public float3 LCA_Rear;               // Lower Control Arm - rear mount
    public float3 LCA_Outboard;           // LCA ball joint (ke upright)
    public float3 Strut_Top;              // Strut top mount (MacPherson)
    public float3 SteeringAxis_Top;       // Steering axis upper point
    public float3 SteeringAxis_Bottom;    // Steering axis lower point
    public float3 WheelCenter;            // Pusat roda (static)
    public float3 ContactPatch;           // Titik kontak ban (static)
    
    // === SPRING ===
    public float SpringRate;              // N/mm
    public float SpringFreeLength;        // mm
    public float SpringInstalledLength;   // mm
    public float SpringMotionRatio;       // ratio (wheel:spring)
    public bool IsProgressiveSpring;
    public float ProgressiveRate;         // N/mm² (untuk progressive)
    
    // === DAMPER ===
    public DamperCurve DamperCompressionLS;  // Low-speed compression
    public DamperCurve DamperCompressionHS;  // High-speed compression
    public DamperCurve DamperReboundLS;      // Low-speed rebound
    public DamperCurve DamperReboundHS;      // High-speed rebound
    public float DamperKneeVelocityLS;       // Batas LS (mm/s)
    public float DamperKneeVelocityHS;       // Batas HS (mm/s)
    public float DamperMotionRatio;          // ratio (wheel:damper)
    
    // === ANTI-ROLL BAR ===
    public float ARBRate;                 // N/mm di roda
    public float ARBPreload;              // N preload
    
    // === BUMP STOP ===
    public float BumpStopEngageTravel;    // mm dari full bump
    public float BumpStopStiffness;       // N/mm
    public float BumpStopExponent;        // Exponent untuk progressive
    
    // === TRAVEL LIMITS ===
    public float MaxBumpTravel;           // mm (kompresi maks)
    public float MaxDroopTravel;          // mm (ekstensi maks)
    
    // === MASSA ===
    public float UnsprungMass;            // kg (roda + hub + brake + half arm)
    public float UprightMass;             // kg
    
    // === BUSHING COMPLIANCE ===
    public float BushingRadialStiffness;  // N/mm
    public float BushingAxialStiffness;   // N/mm
    public float BushingComplianceFactor; // 0 = rigid, 1 = full compliance
    
    // === STEERING ===
    public float BumpSteerGradient;       // deg toe per mm travel
    public float SteeringAxisInclination; // KPI (derajat)
    public float CasterAngle;             // derajat
    public float ScrubRadius;             // mm
}

// Kurva damper (force vs velocity)
public struct DamperCurve
{
    public float[] VelocityPoints;    // mm/s
    public float[] ForcePoints;       // Newton
    public int PointCount;
    
    public float Sample(float velocity)
    {
        // Linear interpolation antara points
        // ...
    }
}

// State runtime suspensi
public struct SuspensionState
{
    // Posisi & Travel
    public float Travel;              // mm (+ = bump, - = droop)
    public float TravelVelocity;      // mm/s
    public float RideHeight;          // mm (ground clearance)
    
    // Gaya
    public float SpringForce;         // N
    public float DamperForce;         // N
    public float ARBForce;            // N
    public float BumpStopForce;       // N
    public float TotalForce;          // N
    
    // Geometri aktual (berubah dengan travel)
    public float Camber;              // derajat
    public float Toe;                 // derajat
    public float Caster;              // derajat
    public float KingpinInclination;  // derajat
    public float ScrubRadius;         // mm
    public float RollCenterHeight;    // mm
    public float InstantCenterPos;    // float3
    
    // Kinematika
    public float MotionRatio;         // aktual (bisa variable)
    public float3 HubPosition;        // posisi hub saat ini
    public float3 ContactPatchPos;    // titik kontak saat ini
    public float3 WheelNormal;        // arah normal roda
    
    // Bushing deflection
    public float3 BushingOffset;      // defleksi bushing
    
    // Status
    public bool IsAtBumpStop;
    public bool IsAtDroopStop;
    public bool IsBroken;
    public float DamageLevel;         // 0-1
}
```

---

## 4. KINEMATIKA GEOMETRI SUSPENSI (Geometric Solver)

Ini adalah bagian paling kompleks. Kinematika menentukan bagaimana geometri roda berubah saat suspensi bergerak.

### 4.1 Instant Center & Roll Center

```
Instant Center (IC) & Roll Center (RC):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Instant Center:
  Titik di mana roda "berotasi" secara instan saat travel.
  Ditentukan oleh perpotongan garis UCA dan LCA.

  Untuk Double Wishbone (2D side view):
  
      UCA line: dari UCA_Front ke UCA_Outboard
      LCA line: dari LCA_Front ke LCA_Outboard
      
      IC = Intersection(UCA_line, LCA_line)
      
  Jika UCA dan LCA paralel → IC di infinity (parallel link)

Roll Center:
  Titik di mana gaya lateral ditransfer ke chassis.
  Ditentukan oleh IC dan wheel contact patch.

  Untuk depan (2D front view):
  
      Garis dari IC ke Contact Patch
      Roll Center = titik di mana garis ini 
                    memotong centerline kendaraan (x = 0)

  Rumus Roll Center Height:
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  RC_height = IC_height - (IC_to_CP_horizontal / track_half) 
              × (IC_height - CP_height)
  
  di mana:
    IC_height = tinggi instant center (mm)
    IC_to_CP_horizontal = jarak horizontal IC ke contact patch
    track_half = setengah track width (mm)
    CP_height = tinggi contact patch (biasanya 0)
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Roll Center Migration:
  Saat body roll, IC dan RC BERUBAH POSISI.
  Ini disebut "Roll Center Migration" dan sangat 
  mempengaruhi handling.
  
  RC migration dihitung ulang setiap frame berdasarkan:
  - Body roll angle
  - Wheel travel
  - Geometri suspensi aktual
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.2 Camber Curve

```
Camber Curve (Camber Gain vs Travel):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Camber adalah sudut roda terhadap vertikal:
  (-) = top tilt inward (negatif camber) → ideal untuk cornering
  (+) = top tilt outward (positif camber)

Saat suspensi bergerak (travel), camber BERUBAH.
Hubungan ini disebut "Camber Curve" atau "Camber Gain".

Untuk Double Wishbone:
  Camber change tergantung pada:
  - Panjang UCA vs LCA
  - Sudut mounting UCA/LCA
  - Posisi ball joints

Perhitungan Camber:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
1. Tentukan posisi upright berdasarkan travel:
   
   // Upright bergerak sepanjang "travel path"
   // Travel path ditentukan oleh constraint dari UCA dan LCA
   
   // Untuk setiap travel t:
   //   Upright_pos = solve_constraints(UCA_length, LCA_length, t)
   
2. Hitung wheel plane normal:
   
   wheel_axis = normalize(cross(
       UCA_vector,    // Arah UCA
       LCA_vector     // Arah LCA
   ))
   
   // Camber = sudut antara wheel_axis dan vertikal
   camber = asin(dot(wheel_axis, chassis_right))
   
3. Simpan sebagai lookup table:
   camber_curve[travel] = camber
   untuk travel dari -maxDroop ke +maxBump

Contoh Camber Curve:
  Travel (mm):  -60  -40  -20   0   +20  +40  +60
  Camber (°):   -0.5 -1.0 -1.8 -2.5 -3.2 -3.8 -4.2
  
  → Saat bump (kompresi), camber lebih negatif
  → Ini ideal untuk cornering (ban tetap flat)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.3 Toe Curve & Bump Steer

```
Toe & Bump Steer:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Toe:
  Sudut roda terhadap centerline kendaraan (horizontal plane)
  Toe-in: roda mengarah ke dalam
  Toe-out: roda mengarah ke luar

Bump Steer:
  Perubahan toe saat suspensi travel.
  Disebabkan oleh steering linkage yang tidak paralel
  dengan control arm.

Perhitungan Bump Steer:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
1. Steering tie-rod menghubungkan steering rack ke upright
2. Saat travel, tie-rod bergerak dengan arc berbeda dari LCA
3. Perbedaan arc ini menyebabkan toe berubah

Toe(travel) = Toe_static + BumpSteerGradient × travel
              + BumpSteerCurve(travel)  // Non-linear

Contoh Bump Steer Curve:
  Travel (mm):  -60  -40  -20   0   +20  +40  +60
  Toe (°):      +0.3 +0.2 +0.1  0   -0.1 -0.3 -0.5
  
  → Saat bump, roda toe-out (mengurangi stabilitas)
  → Saat droop, roda toe-in (menambah stabilitas)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Ackermann Steering Geometry:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Saat cornering, roda dalam harus belok lebih tajam
dari roda luar agar tidak ada tire scrub.

Ackermann Angle:
  δ_inner = arctan(L / (R - track/2))
  δ_outer = arctan(L / (R + track/2))
  
  L = wheelbase
  R = radius belok
  track = lebar track

Ackermann Ratio:
  A = (δ_inner - δ_outer) / δ_inner × 100%
  
  100% Ackermann = geometri sempurna
  50% Ackermann = parallel steer (umum di racing)
  0% Ackermann = kedua roda sudut sama

Di racing, Ackermann biasanya 40-70% karena
tire slip angle membuat geometri ideal berbeda.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.4 Anti-Dive & Anti-Squat Geometry

```
Anti-Dive & Anti-Squat:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Saat braking, body "dive" (depan turun).
Saat accelerating, body "squat" (belakang turun).

Geometri suspensi bisa mengurangi efek ini melalui
sudut control arm dan posisi instant center.

Anti-Dive (Depan):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  % Anti-Dive = (tan(θ_front) × h_cg / L) × 100%
  
  θ_front = sudut garis dari contact patch depan 
            ke instant center belakang (side view)
  h_cg = tinggi center of gravity
  L = wheelbase
  
  100% Anti-Dive = tidak ada dive saat braking
  0% Anti-Dive = dive penuh (geometri tidak melawan)
  
  Racing biasanya: 30-60% anti-dive
  (100% membuat steering feel buruk)

Anti-Squat (Belakang):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  % Anti-Squat = (tan(θ_rear) × h_cg / L) × 100%
  
  θ_rear = sudut garis dari contact patch belakang
           ke instant center depan (side view)
  
  Racing biasanya: 40-70% anti-squat
  (Terlalu tinggi → rear instability saat cornering)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.5 Algoritma Kinematic Solver Lengkap

```
ALGORITMA: SuspensionKinematics.Solve
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  config (SuspensionConfig), travel (mm), steerAngle (deg)
OUTPUT: SuspensionState (geometri aktual)

1. Normalisasi travel:
   t = travel / 1000.0  // mm → meter
   t_clamped = clamp(t, -config.MaxDroop/1000, config.MaxBump/1000)

2. Hitung posisi upright berdasarkan travel:
   
   IF config.Type == DoubleWishbone:
       // Constraint-based solving
       // Upright harus memenuhi:
       //   |upright - UCA_outboard_base| = UCA_length (fixed)
       //   |upright - LCA_outboard_base| = LCA_length (fixed)
       //   upright.y = base.y + t (vertical travel)
       
       // Iterative solving (Newton-Raphson, 3-5 iterasi):
       upright_pos = SolveDoubleWishboneConstraints(
           config.UCA_Front, config.UCA_Rear,
           config.UCA_Outboard, config.LCA_Front,
           config.LCA_Rear, config.LCA_Outboard,
           t_clamped
       )
   
   ELSE IF config.Type == MacPherson:
       // Strut menentukan angle, LCA menentukan posisi
       upright_pos = SolveMacPhersonConstraints(
           config.Strut_Top, config.LCA_Front,
           config.LCA_Rear, config.LCA_Outboard,
           t_clamped
       )
   
   ELSE IF config.Type == Pushrod:
       // Pushrod → rocker → spring (inboard)
       wheel_travel = t_clamped
       spring_travel = wheel_travel * config.SpringMotionRatio
       upright_pos = SolvePushrodConstraints(...)

3. Hitung wheel position & orientation:
   
   wheel_center = upright_pos + wheel_offset
   wheel_normal = CalculateWheelNormal(upright_pos, config)
   
   // Camber dari wheel normal
   camber = asin(dot(wheel_normal, chassis_right_vector))
   camber_deg = camber * RAD2DEG

4. Hitung steering axis & Ackermann:
   
   steer_axis = normalize(
       config.SteeringAxis_Top - config.SteeringAxis_Bottom
   )
   
   // KPI (Kingpin Inclination)
   kpi = asin(dot(steer_axis, chassis_right_vector)) * RAD2DEG
   
   // Caster
   caster = asin(dot(steer_axis, chassis_forward_vector)) * RAD2DEG
   
   // Scrub radius
   scrub_radius = CalculateScrubRadius(
       steer_axis, wheel_center, config.ContactPatch
   )

5. Hitung toe (termasuk bump steer):
   
   toe_static = config.StaticToe
   bump_steer = config.BumpSteerGradient * travel
                + SampleBumpSteerCurve(config, travel)
   
   toe_total = toe_static + bump_steer
   
   // Apply steering input
   IF isFrontWheel:
       steer_angle = steerInput / config.SteeringRatio
       toe_total += steer_angle
       
       // Ackermann correction
       IF steerInput != 0:
           ackermann_correction = CalculateAckermann(
               steer_angle, config.AckermannRatio, isInnerWheel
           )
           toe_total += ackermann_correction

6. Hitung Instant Center & Roll Center:
   
   // Project UCA dan LCA ke 2D (front view)
   uca_line_2d = Line2D(config.UCA_Front.yz, config.UCA_Outboard.yz)
   lca_line_2d = Line2D(config.LCA_Front.yz, config.LCA_Outboard.yz)
   
   instant_center = Intersect2D(uca_line_2d, lca_line_2d)
   
   // Roll center
   rc_line = Line2D(instant_center, contact_patch_2d)
   roll_center_height = rc_line.EvaluateAtX(track_half)
   
   // Adjust untuk body roll
   roll_center_height += bodyRollEffect * rollCenterMigrationFactor

7. Hitung motion ratio aktual:
   
   // Motion ratio bisa berubah dengan travel
   // (terutama untuk pushrod dengan rocker)
   motion_ratio = CalculateMotionRatio(config, travel)
   
   // Untuk pushrod:
   // MR = (pushrod_lever / rocker_lever) × cos(angle_correction)

8. Hitung anti-dive / anti-squat:
   
   anti_dive = CalculateAntiDive(config, upright_pos)
   anti_squat = CalculateAntiSquat(config, upright_pos)

9. Apply bushing compliance:
   
   IF config.BushingComplianceFactor > 0:
       // Bushing defleksi berdasarkan gaya
       bushing_offset = CalculateBushingDeflection(
           lateralForce, longitudinalForce,
           config.BushingRadialStiffness,
           config.BushingAxialStiffness
       ) * config.BushingComplianceFactor
       
       // Adjust geometri
       camber += bushing_offset.x * camberComplianceFactor
       toe += bushing_offset.y * toeComplianceFactor

10. Pack results:
    state.Travel = travel
    state.Camber = camber_deg
    state.Toe = toe_total
    state.Caster = caster
    state.KingpinInclination = kpi
    state.ScrubRadius = scrub_radius
    state.RollCenterHeight = roll_center_height
    state.HubPosition = wheel_center
    state.MotionRatio = motion_ratio
    state.IsAtBumpStop = (travel >= config.MaxBumpTravel)
    state.IsAtDroopStop = (travel <= -config.MaxDroopTravel)

RETURN state
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 5. MODEL DINAMIKA SUSPENSI (Force Calculation)

### 5.1 Spring Force Model

```
Spring Force Models:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. LINEAR SPRING:
   F_spring = k × x
   
   k = spring rate (N/mm)
   x = defleksi dari installed length (mm)
   
   Defleksi:
   x = (SpringInstalledLength - SpringFreeLength) + travel × MotionRatio
   
   // Preload:
   preload = SpringInstalledLength - SpringFreeLength
   // Jika preload < 0, spring dalam keadaan terkompresi saat rest

2. PROGRESSIVE SPRING:
   F_spring = k₁ × x + k₂ × x²
   
   k₁ = linear rate (N/mm)
   k₂ = progressive rate (N/mm²)
   
   // Semakin terkompresi, semakin keras

3. DUAL-RATE SPRING:
   IF x < x_transition:
       F_spring = k_soft × x
   ELSE:
       F_spring = k_soft × x_transition + k_stiff × (x - x_transition)
   
   // Dua spring dengan rate berbeda
   // Tender spring + main spring

4. SPRING DENGAN MOTION RATIO:
   // Force di roda vs force di spring berbeda
   F_at_wheel = F_spring × MotionRatio
   
   // Travel di roda vs travel di spring berbeda
   spring_travel = wheel_travel × MotionRatio
   
   // Effective wheel rate:
   k_wheel = k_spring × MotionRatio²
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.2 Damper Force Model (4-Way)

```
Damper Model (4-Way Adjustable):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Damper racing memiliki 4 pengaturan terpisah:
  1. Low-Speed Compression (LSC)
  2. High-Speed Compression (HSC)
  3. Low-Speed Rebound (LSR)
  4. High-Speed Rebound (HSR)

"Speed" di sini = kecepatan shaft damper (mm/s),
BUKAN kecepatan kendaraan.

Force-Velocity Curve:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Force (N)
  ▲
  │         HSC (High-Speed Compression)
  │        ╱
  │       ╱
  │      ╱  LSC (Low-Speed Compression)
  │     ╱
  │    ╱
  │   ╱
  ├──┼──────────────────────→ Velocity (mm/s)
  │  │  ╲
  │  │   ╲  LSR (Low-Speed Rebound)
  │  │    ╲
  │  │     ╲
  │  │      ╲
  │  │       ╲  HSR (High-Speed Rebound)
  │
  
  Compression: velocity < 0 (shaft masuk)
  Rebound: velocity > 0 (shaft keluar)
  
  Knee velocity: titik transisi LS → HS
  Biasanya: 20-80 mm/s
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Perhitungan Force:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
v_damper = TravelVelocity × DamperMotionRatio  // mm/s

IF v_damper < 0:  // COMPRESSION
    v_abs = |v_damper|
    
    IF v_abs < KneeVelocityLS:
        // Low-speed compression (linear)
        F_damper = LSC_Rate × v_abs
    ELSE:
        // High-speed compression (linear dengan slope berbeda)
        F_knee = LSC_Rate × KneeVelocityLS
        F_damper = F_knee + HSC_Rate × (v_abs - KneeVelocityLS)
    
    // Direction: melawan kompresi (push up)
    F_damper = +F_damper

ELSE:  // REBOUND (v_damper > 0)
    v_abs = |v_damper|
    
    IF v_abs < KneeVelocityLS:
        F_damper = LSR_Rate × v_abs
    ELSE:
        F_knee = LSR_Rate × KneeVelocityLS
        F_damper = F_knee + HSR_Rate × (v_abs - KneeVelocityLS)
    
    // Direction: melawan rebound (pull down)
    F_damper = -F_damper

// Apply motion ratio
F_at_wheel = F_damper × DamperMotionRatio

// Optional: Damper hysteresis (lag)
F_actual = lerp(F_previous, F_at_wheel, damperResponse)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Contoh Parameter Damper Racing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Parameter              │ Value
───────────────────────┼──────────────
LSC Rate               │ 80 N/mm/s
HSC Rate               │ 30 N/mm/s
LSR Rate               │ 120 N/mm/s
HSR Rate               │ 45 N/mm/s
Knee Velocity LS       │ 40 mm/s
Knee Velocity HS       │ 150 mm/s
Motion Ratio           │ 0.9
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.3 Anti-Roll Bar Model

```
Anti-Roll Bar (ARB) Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ARB menghubungkan roda kiri dan kanan.
Saat body roll, satu roda naik dan satu turun.
ARB mentransfer gaya untuk mengurangi roll.

Perhitungan:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
travel_diff = travel_left - travel_right  // mm

// Gaya ARB di setiap roda:
F_ARB_left = -k_arb × travel_diff × ARB_ratio
F_ARB_right = +k_arb × travel_diff × ARB_ratio

di mana:
  k_arb = ARB rate (N/mm di roda)
  ARB_ratio = lever ratio (biasanya 0.5-1.0)
  
  // Tanda: ARB melawan perbedaan travel
  // Jika kiri naik (travel_left > 0), ARB mendorong kiri turun
  // dan menarik kanan naik

ARB Preload:
  F_ARB_left += ARB_preload
  F_ARB_right -= ARB_preload
  
  // Preload untuk setup asymmetric
  // (misal: preload untuk kompensasi cross-weight)

ARB Rate dari Physical Dimensions:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk torsion bar ARB:

k_arb = (G × d⁴) / (8 × L_arm² × (L_torsion + 2×L_arm×(3L_arm/L_torsion)))

G = shear modulus material (~79 GPa untuk steel)
d = diameter torsion bar (mm)
L_torsion = panjang torsion bar (mm)
L_arm = panjang lever arm (mm)

Tapi untuk game, biasanya cukup pakai k_arb langsung (N/mm).
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.4 Bump Stop Model

```
Bump Stop Model:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Bump stop = karet/polyurethane yang mencegah suspensi
bottoming out (mentok).

Perhitungan:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
engagement_point = MaxBumpTravel - BumpStopLength

IF travel > engagement_point:
    compression = travel - engagement_point
    max_compression = BumpStopLength
    
    // Normalisasi kompresi (0-1)
    ratio = compression / max_compression
    
    // Progressive force (semakin keras semakin terkompresi)
    F_stop = BumpStopStiffness × pow(ratio, BumpStopExponent) 
             × max_compression
    
    // Contoh: exponent = 2 → quadratic
    // ratio 0.5 → force = 0.25 × max
    // ratio 0.9 → force = 0.81 × max
    
ELSE:
    F_stop = 0

Droop Stop (jika ada):
IF travel < -MaxDroopTravel:
    F_droop_stop = -DroopStopStiffness × (|travel| - MaxDroopTravel)
ELSE:
    F_droop_stop = 0
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.5 Total Suspension Force

```
Total Force Calculation:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Untuk setiap roda:

F_total = F_spring + F_damper + F_ARB + F_bump_stop + F_droop_stop

di mana:
  F_spring = k_wheel × (travel + preload_travel)
  F_damper = DamperModel(travel_velocity)
  F_ARB = ARBModel(travel, travel_opposite)
  F_bump_stop = BumpStopModel(travel)
  F_droop_stop = DroopStopModel(travel)

Vertical load di ban:
  Fz_wheel = F_total + UnsprungMass × g + AeroDownforce_wheel

Update travel velocity:
  travel_velocity += (F_total / UnsprungMass) × dt
  travel_velocity += (F_chassis_reaction / ChassisMass) × dt

Integrate travel:
  travel += travel_velocity × dt
  travel = clamp(travel, -MaxDroop, MaxBump)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 6. ROLL DYNAMICS & LOAD TRANSFER

### 6.1 Body Roll Model

```
Body Roll Dynamics:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Saat cornering, body mobil roll (miring).
Ini dipengaruhi oleh:
  - Lateral acceleration (ay)
  - Center of gravity height (h_cg)
  - Roll stiffness total (K_roll)
  - Track width

Roll Angle (Steady-State):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
φ = (m × ay × h_cg) / K_roll_total

di mana:
  φ = roll angle (rad)
  m = massa kendaraan (kg)
  ay = lateral acceleration (m/s²)
  h_cg = tinggi CoG (m)
  K_roll_total = total roll stiffness (N·m/rad)

K_roll_total = K_roll_front + K_roll_rear + K_roll_tires

K_roll_front = (k_front_left + k_front_right) × (track_front/2)²
             + k_arb_front × ARB_effective_ratio

K_roll_rear = (k_rear_left + k_rear_right) × (track_rear/2)²
            + k_arb_rear × ARB_effective_ratio

K_roll_tires = (k_tire_FL + k_tire_FR) × (track_front/2)²
             + (k_tire_RL + k_tire_RR) × (track_rear/2)²
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Roll Distribution (Front/Rear):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Front roll stiffness fraction:
  f_front = K_roll_front / K_roll_total
  
  // Menentukan balance:
  // f_front tinggi → understeer
  // f_rear tinggi → oversteer
  
  // Racing setup biasanya:
  // f_front = 0.50 - 0.65 (front-biased untuk stability)
  // f_front = 0.35 - 0.50 (rear-biased untuk rotation)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Dynamic Roll (dengan roll inertia):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
I_roll × φ̈ = m × ay × h_cg - K_roll × φ - C_roll × φ̇

di mana:
  I_roll = roll moment of inertia (kg·m²)
  φ̈ = roll angular acceleration (rad/s²)
  φ̇ = roll angular velocity (rad/s)
  C_roll = roll damping (N·m·s/rad)

Ini adalah ODE orde 2 yang diintegrasi setiap frame:
  φ̇ += (m × ay × h_cg - K_roll × φ - C_roll × φ̇) / I_roll × dt
  φ += φ̇ × dt
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 6.2 Load Transfer Calculation

```
Load Transfer (Distribusi Beban):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Load transfer terjadi saat:
  1. Braking / Accelerating (longitudinal)
  2. Cornering (lateral)
  3. Combination (combined)

Longitudinal Load Transfer:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ΔFz_long = (m × ax × h_cg) / L

di mana:
  ax = longitudinal acceleration (m/s²)
  L = wheelbase (m)
  
  Saat braking (ax < 0): beban pindah ke DEPAN
  Saat accelerating (ax > 0): beban pindah ke BELAKANG

Per roda:
  Fz_FL += ΔFz_long × (1 - lateral_fraction_FL)
  Fz_FR += ΔFz_long × (1 - lateral_fraction_FR)
  Fz_RL -= ΔFz_long × lateral_fraction_RL
  Fz_RR -= ΔFz_long × lateral_fraction_RR

Lateral Load Transfer:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ΔFz_lat_front = (m × ay × h_cg × b/L) / (track_front × 2)
ΔFz_lat_rear = (m × ay × h_cg × a/L) / (track_rear × 2)

di mana:
  a = jarak CoG ke as depan (m)
  b = jarak CoG ke as belakang (m)
  
  Saat cornering kanan (ay > 0): beban pindah ke KIRI

Per roda (cornering kanan):
  Fz_FL -= ΔFz_lat_front  (dalam, berkurang)
  Fz_FR += ΔFz_lat_front  (luar, bertambah)
  Fz_RL -= ΔFz_lat_rear
  Fz_RR += ΔFz_lat_rear

Geometric Load Transfer vs Elastic Load Transfer:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total load transfer terdiri dari 2 komponen:

1. GEOMETRIC (melalui roll center):
   ΔFz_geo = m × ay × (h_rc / track)
   h_rc = tinggi roll center
   
   // Langsung ditransfer melalui linkage
   // Tidak melalui spring/damper

2. ELASTIC (melalui spring/ARB):
   ΔFz_elastic = m × ay × (h_cg - h_rc) / track
   
   // Melalui spring, damper, ARB
   // Tergantung roll stiffness

Total:
  ΔFz_total = ΔFz_geo + ΔFz_elastic
  
  // h_rc tinggi → lebih banyak geometric (respons lebih cepat)
  // h_rc rendah → lebih banyak elastic (respons lebih lambat)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 7. IMPLEMENTASI DI GAME ENGINE

### 7.1 Arsitektur di Unity

```
Unity Implementation Architecture:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Unity TIDAK memiliki sistem suspensi built-in yang cocok
untuk racing simulation. WheelCollider Unity sangat terbatas.

Yang harus dilakukan:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. DISABLE Unity WheelCollider
   → Tidak digunakan sama sekali
   
2. Buat Custom Suspension Solver
   → Berjalan di FixedUpdate
   → Gunakan Job System + Burst untuk performa
   
3. Raycast untuk Ground Detection
   → 4 raycast per kendaraan (1 per roda)
   → Atau spherecast untuk lebih stabil
   
4. Custom Tire Model (Pacejka)
   → Seperti yang sudah dirancang sebelumnya
   
5. Custom Rigid Body Integration
   → Bisa pakai Unity Rigidbody untuk chassis
   → ATAU custom integrator untuk kontrol penuh

Update Order per Frame:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
FixedUpdate (240 Hz):
│
├── 1. Ground Detection (Raycast)
│      → Dapatkan ground height & normal per roda
│
├── 2. Kinematic Solver
│      → Hitung geometri suspensi untuk travel saat ini
│      → Camber, toe, roll center, dll
│
├── 3. Force Calculation
│      → Spring + Damper + ARB + Bump Stop
│      → Tire forces (dari Pacejka)
│
├── 4. Load Transfer
│      → Hitung distribusi Fz ke 4 roda
│
├── 5. Integration
│      → Update travel, velocity, posisi
│
├── 6. Apply ke Rigidbody
│      → Force & torque ke chassis
│
└── 7. Visual Update
       → Posisi roda, steering, body roll
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 7.2 Algoritma Suspensi Update Lengkap

```
ALGORITMA: SuspensionSystem.UpdateAll
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  vehicle state, ground data, dt
OUTPUT: updated suspension states, forces

1. UNTUK SETIAP RODA (FL, FR, RL, RR):

   1a. Ground Detection:
       rayOrigin = vehicle.Transform.TransformPoint(wheel.AttachPoint)
       rayDir = vehicle.Transform.TransformDirection(Vector3.down)
       
       IF Physics.Raycast(rayOrigin, rayDir, out hit, maxDistance):
           groundHeight = hit.distance
           groundNormal = hit.normal
           groundSurface = hit.collider.material  // Surface type
       ELSE:
           groundHeight = maxDistance  // No ground (airborne)
           groundNormal = Vector3.up

   1b. Calculate Travel:
       // Travel = seberapa jauh roda terkompresi
       targetTravel = restRideHeight - groundHeight
       targetTravel = clamp(targetTravel, -MaxDroop, MaxBump)
       
       // Smooth transition (hindari jolt)
       travel = lerp(travel_prev, targetTravel, suspensionResponse)

   1c. Calculate Travel Velocity:
       travelVelocity = (travel - travel_prev) / dt

   1d. Solve Kinematics:
       kinState = SuspensionKinematics.Solve(
           config, travel, steerInput
       )

   1e. Calculate Forces:
       // Spring
       F_spring = config.SpringRate 
                  × (travel + preloadTravel) 
                  × kinState.MotionRatio
       
       // Damper (4-way)
       F_damper = CalculateDamperForce(
           travelVelocity, config.Damper, kinState.MotionRatio
       )
       
       // ARB
       oppositeTravel = GetOppositeWheelTravel(wheelIndex)
       F_arb = config.ARBRate × (travel - oppositeTravel)
       
       // Bump Stop
       F_stop = CalculateBumpStopForce(travel, config)
       
       // Total
       F_total = F_spring + F_damper + F_arb + F_stop

   1f. Calculate Vertical Load:
       Fz = F_total 
            + unsprungMass × g 
            + aeroDownforce[wheelIndex]
            + loadTransfer[wheelIndex]
       Fz = max(Fz, 0)  // Ban tidak bisa "menarik" aspal

   1g. Calculate Tire Forces (Pacejka):
       slipAngle = CalculateSlipAngle(wheelIndex, vehicleState)
       slipRatio = CalculateSlipRatio(wheelIndex, vehicleState)
       
       tireForces = PacejkaTire.Calculate(
           slipAngle, slipRatio, Fz, 
           kinState.Camber, tireTemp, tirePressure
       )

   1h. Apply Forces to Chassis:
       // Convert tire forces to chassis space
       F_world = TransformForce(tireForces, kinState.WheelNormal)
       
       // Apply ke rigid body di posisi roda
       vehicle.RigidBody.AddForceAtPosition(
           F_world, kinState.ContactPatchPos, ForceMode.Force
       )

   1i. Update Visual:
       wheelVisual.Position = kinState.HubPosition
       wheelVisual.Rotation = CalculateWheelRotation(
           kinState.Camber, kinState.Toe, steerAngle, wheelSpin
       )

2. Update Body Roll:
   CalculateBodyRoll(vehicleState, dt)
   ApplyBodyRollToVisuals(vehicleState)

3. Update Load Transfer:
   CalculateLoadTransfer(vehicleState, dt)

4. Telemetry:
   LogSuspensionData(vehicleState)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 7.3 Ground Detection Strategy

```
Ground Detection untuk Racing Sim:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Option 1: RAYCAST (Paling umum)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - 1 ray per roda, arah down dari attach point
  - Cepat, cukup akurat untuk flat/curved surfaces
  - Masalah: tidak akurat untuk kerb/uneven surfaces
  
  Optimasi:
  - Gunakan LayerMask untuk hanya hit track surface
  - Cache hit results, hanya raycast saat perlu
  - Gunakan Physics.RaycastNonAlloc untuk avoid GC

Option 2: SPHERECAST (Lebih stabil)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - Sphere kecil (radius ~5cm) di-cast ke bawah
  - Lebih stabil di permukaan tidak rata
  - Sedikit lebih mahal dari raycast
  
Option 3: MULTI-RAY (Paling akurat)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - 3-5 ray per roda (center + corners)
  - Hitung average height & normal
  - Paling akurat untuk kerb & uneven surfaces
  - 3-5× lebih mahal dari single ray
  
  Rekomendasi untuk racing sim:
  - Gunakan SPHERECAST dengan radius 3-5 cm
  - Atau MULTI-RAY (3 rays) untuk akurasi tinggi
  - Cache results dan hanya update saat travel berubah > 1mm

Option 4: MESH SAMPLING (Paling cepat, offline)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  - Track surface di-bake menjadi heightmap
  - Lookup height & normal dari heightmap
  - Tidak perlu raycast sama sekali
  - Sangat cepat, tapi tidak bisa untuk dynamic objects
  
  Cocok untuk: circuit racing (track static)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 8. DAMPER MODEL LANJUTAN (Hydraulic Simulation)

### 8.1 Full Hydraulic Damper Model (Level 3)

```
Hydraulic Damper Circuit:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Damper racing sebenarnya adalah sistem hidrolik kompleks.
Untuk simulasi Level 3, kita modelkan sirkuit hidroliknya.

Komponen Damper:
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ┌─────────────────────────────────────────────┐              │
│   │              CYLINDER (Body)                 │              │
│   │                                             │              │
│   │   ┌───┐  ┌──────────────────────────┐     │              │
│   │   │ P │  │      PISTON              │     │              │
│   │   │ i │  │  ┌────┐  ┌────┐  ┌────┐ │     │              │
│   │   │ s │  │  │Port│  │Port│  │Port│ │     │              │
│   │   │ t │  │  │ 1  │  │ 2  │  │ 3  │ │     │              │
│   │   │ o │  │  └────┘  └────┘  └────┘ │     │              │
│   │   │ n │  └──────────────────────────┘     │              │
│   │   │   │         │                         │              │
│   │   └───┘         │  SHAFT                  │              │
│   │                 │                         │              │
│   └─────────────────┼─────────────────────────┘              │
│                     │                                         │
│   ┌─────────────────┼─────────────────────────┐              │
│   │   EXTERNAL RESERVOIR (Emulsion)           │              │
│   │   ┌─────────────────────────────────┐    │              │
│   │   │  N₂ Gas  │  Oil                │    │              │
│   │   │  (sep.)  │                     │    │              │
│   │   └─────────────────────────────────┘    │              │
│   └───────────────────────────────────────────┘              │
│                                                                 │
│   Adjustment:                                                   │
│   - Compression adjuster (needle valve) → LSC/HSC              │
│   - Rebound adjuster (needle valve) → LSR/HSR                  │
│   - Gas pressure → affects cavitation threshold                │
└─────────────────────────────────────────────────────────────────┘

Model Hidrolik:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Flow melalui port/orifice:
  Q = Cd × A × √(2 × ΔP / ρ)
  
  Cd = discharge coefficient (~0.6-0.8)
  A = orifice area (m²)
  ΔP = pressure differential (Pa)
  ρ = oil density (~850 kg/m³)

Force dari pressure:
  F = ΔP × A_piston
  
  A_piston = π × (D/2)² - π × (d/2)²
  
  D = piston diameter
  d = shaft diameter (untuk rebound side)

Cavitation:
  IF P_local < P_vapor:
      // Oil mendidih → cavitation
      // Force drop drastis
      F_actual = F × cavitation_factor
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 8.2 Simplified 4-Way Damper untuk Game

```
ALGORITMA: DamperModel.CalculateForce
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  velocity (mm/s), damperConfig, dt
OUTPUT: force (N)

1. Tentukan regime (compression/rebound):
   
   IF velocity < 0:  // COMPRESSION
       v = |velocity|
       
       // Low-speed compression
       IF v < config.KneeVelocityComp:
           F = config.LSC_Rate × v
           
       // High-speed compression
       ELSE:
           F_knee = config.LSC_Rate × config.KneeVelocityComp
           F = F_knee + config.HSC_Rate × (v - config.KneeVelocityComp)
       
       // Optional: digressive (force plateau)
       IF config.IsDigressiveComp:
           F = min(F, config.MaxCompForce)
       
       F_direction = +1  // Melawan kompresi
   
   ELSE:  // REBOUND
       v = |velocity|
       
       IF v < config.KneeVelocityReb:
           F = config.LSR_Rate × v
       ELSE:
           F_knee = config.LSR_Rate × config.KneeVelocityReb
           F = F_knee + config.HSR_Rate × (v - config.KneeVelocityReb)
       
       IF config.IsDigressiveReb:
           F = min(F, config.MaxRebForce)
       
       F_direction = -1  // Melawan rebound

2. Apply temperature correction:
   // Oil viscosity berubah dengan suhu
   tempFactor = 1.0 + (damperTemp - 40) × tempCoeff
   // 40°C = reference temperature
   // tempCoeff ≈ -0.003 per °C (oil thinner saat hot)
   F *= tempFactor

3. Apply damage correction:
   IF damperDamage > 0:
       F *= (1.0 - damperDamage × 0.5)
       // Damper rusak → force berkurang

4. Smooth transition (hindari jolt):
   F = lerp(F_previous, F, damperSmoothing)
   F_previous = F

5. RETURN F × F_direction
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 9. BUSHING COMPLIANCE MODEL

### 9.1 Bushing Model

```
Bushing Compliance:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Bushing = karet/elastomer di pivot points.
Dalam reality, bushing TIDAK rigid sempurna.
Ada defleksi kecil yang mengubah geometri.

Efek bushing compliance:
  - Camber berubah di bawah lateral load
  - Toe berubah di bawah longitudinal load
  - "Steer compliance" → self-steering effect

Model Simplified:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk setiap bushing di control arm:

1. Hitung defleksi:
   δ_radial = F_lateral / K_radial    // mm
   δ_axial = F_longitudinal / K_axial  // mm
   
   K_radial = radial stiffness (N/mm), biasanya 500-2000
   K_axial = axial stiffness (N/mm), biasanya 300-1500

2. Hitung perubahan geometri:
   Δcamber = δ_radial × camberComplianceFactor
   Δtoe = δ_axial × toeComplianceFactor
   
   camberComplianceFactor ≈ 0.01-0.05 deg/mm
   toeComplianceFactor ≈ 0.005-0.03 deg/mm

3. Apply ke kinematika:
   camber_actual = camber_kinematic + Δcamber
   toe_actual = toe_kinematic + Δtoe

4. Hysteresis (energy loss):
   // Loading dan unloading path berbeda
   IF F_increasing:
       δ = F / K_loading
   ELSE:
       δ = F / K_unloading + hysteresis_offset
       
   K_unloading < K_loading (karet kehilangan energi)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Contoh Parameter Bushing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bushing Location    │ K_radial │ K_axial │ Compliance
────────────────────┼──────────┼─────────┼────────────
UCA Front Pivot     │ 1200 N/mm│ 800 N/mm│ Low
UCA Rear Pivot      │ 1200 N/mm│ 800 N/mm│ Low
LCA Front Pivot     │ 1500 N/mm│ 1000 N/mm│ Low
LCA Rear Pivot      │ 1500 N/mm│ 1000 N/mm│ Low
Tie-rod Inner       │ 600 N/mm │ 400 N/mm│ Medium
Tie-rod Outer       │ 400 N/mm │ 300 N/mm│ High
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 10. KONFIGURASI UNTUK BERBAGAI JENIS MOBIL

### 10.1 Setup Presets

```
Suspension Setup Presets:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

FORMULA 1 / OPEN WHEEL:
┌─────────────────────────────────────────────────────────────────┐
│ Type: Pushrod (front & rear)                                    │
│ Spring Rate: 250-400 N/mm (sangat kaku)                       │
│ Damper: 4-way, very high compression                           │
│ ARB: Very stiff (torsion bar)                                  │
│ Travel: 25-40 mm (sangat sedikit)                              │
│ Camber: -3.5° to -4.5° (sangat negatif)                       │
│ Ride Height: 30-50 mm                                          │
│ Unsprung Mass: 8-12 kg per corner                              │
│ Bushing: Metal spherical joints (NO compliance)                │
└─────────────────────────────────────────────────────────────────┘

GT3 / SPORTS CAR:
┌─────────────────────────────────────────────────────────────────┐
│ Type: Double Wishbone (front & rear)                           │
│ Spring Rate: 120-200 N/mm                                      │
│ Damper: 4-way adjustable                                       │
│ ARB: Adjustable (front & rear)                                 │
│ Travel: 60-90 mm                                               │
│ Camber: -2.5° to -3.5°                                        │
│ Ride Height: 55-75 mm                                          │
│ Unsprung Mass: 25-40 kg per corner                             │
│ Bushing: Stiff rubber / spherical                              │
└─────────────────────────────────────────────────────────────────┘

ROAD CAR / SEDAN:
┌─────────────────────────────────────────────────────────────────┐
│ Type: MacPherson (front) + Multi-link (rear)                   │
│ Spring Rate: 30-60 N/mm                                        │
│ Damper: Twin-tube, limited adjustment                          │
│ ARB: Front only (atau soft rear)                               │
│ Travel: 100-150 mm                                             │
│ Camber: -0.5° to -1.5°                                        │
│ Ride Height: 120-160 mm                                        │
│ Unsprung Mass: 40-60 kg per corner                             │
│ Bushing: Soft rubber (comfort-oriented)                        │
└─────────────────────────────────────────────────────────────────┘

OFF-ROAD / RALLY:
┌─────────────────────────────────────────────────────────────────┐
│ Type: Double Wishbone atau Trailing Arm                        │
│ Spring Rate: 40-80 N/mm                                        │
│ Damper: Long-travel, remote reservoir                          │
│ ARB: None atau disconnectable                                  │
│ Travel: 200-350 mm (sangat panjang)                            │
│ Camber: -1° to -2°                                            │
│ Ride Height: 200-300 mm                                        │
│ Unsprung Mass: 50-80 kg per corner                             │
│ Bushing: Polyurethane (durable)                                │
└─────────────────────────────────────────────────────────────────┘

OVAL / NASCAR:
┌─────────────────────────────────────────────────────────────────┐
│ Type: Solid Axle (rear) + Double Wishbone (front)              │
│ Spring Rate: 200-350 N/mm                                      │
│ Damper: Mono-tube, high compression                            │
│ ARB: Very stiff front                                          │
│ Travel: 50-80 mm                                               │
│ Camber: -1° to -3° (asymmetric, tergantung track)             │
│ Ride Height: 70-100 mm                                         │
│ Cross Weight: 50-52%                                           │
│ Wedge: Adjustable spring perch                                 │
└─────────────────────────────────────────────────────────────────┘
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 11. RINGKASAN RUMUS SUSPENSI

| Rumus | Formula | Keterangan |
|---|---|---|
| Spring Force | `F = k × x` | Hooke's Law |
| Progressive Spring | `F = k₁x + k₂x²` | Non-linear |
| Damper Force (LS) | `F = c × v` | Linear damping |
| Damper Force (HS) | `F = F_knee + c_hs × (v - v_knee)` | High-speed |
| ARB Force | `F = k_arb × (travel_L - travel_R)` | Anti-roll |
| Bump Stop | `F = k_stop × (x - engage)^n` | Progressive stop |
| Roll Angle | `φ = m·ay·h_cg / K_roll` | Steady-state roll |
| Roll ODE | `I·φ̈ = m·ay·h - K·φ - C·φ̇` | Dynamic roll |
| Long Load Transfer | `ΔFz = m·ax·h / L` | Braking/accel |
| Lat Load Transfer | `ΔFz = m·ay·h / (2·track)` | Cornering |
| Geometric LT | `ΔFz = m·ay·h_rc / track` | Via roll center |
| Elastic LT | `ΔFz = m·ay·(h-h_rc) / track` | Via spring |
| Camber | `γ = asin(n·right)` | From wheel normal |
| Ackermann | `δ_i = atan(L/(R-t/2))` | Steering geometry |
| Anti-dive | `% = tan(θ)·h/L × 100` | Braking geometry |
| Motion Ratio | `MR = wheel_travel / spring_travel` | Lever ratio |
| Wheel Rate | `k_w = k_s × MR²` | Effective rate |
| Bushing Defl | `δ = F / K_bush` | Compliance |

---

## 12. PANDUAN IMPLEMENTASI DENGAN QWEN CODE

```
PROMPT SEQUENCE untuk Suspension System:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

STEP 1 - Data Structures:
"Buat semua struct untuk suspension system di Unity C#:
 SuspensionConfig, SuspensionState, DamperCurve, 
 SuspensionType enum. Gunakan Unity.Mathematics.
 Sertakan semua parameter yang disebutkan:
 spring, damper 4-way, ARB, bump stop, bushing,
 geometri hardpoints, travel limits, unsprung mass."

STEP 2 - Kinematic Solver (Double Wishbone):
"Buat class SuspensionKinematics dengan method Solve()
 untuk Double Wishbone. Implementasikan:
 - Constraint-based upright position solving
 - Camber calculation dari wheel normal
 - Toe calculation dengan bump steer
 - Instant center & roll center calculation
 - Motion ratio calculation
 - Ackermann steering
 Gunakan iterative solving (Newton-Raphson, 5 iterasi)."

STEP 3 - Kinematic Solver (MacPherson):
"Buat extension untuk MacPherson strut kinematics.
 Strut angle fixed, LCA menentukan posisi.
 Camber curve berbeda dari double wishbone."

STEP 4 - Force Models:
"Buat class SuspensionForces dengan:
 - LinearSpring, ProgressiveSpring, DualRateSpring
 - DamperModel 4-way (LS/HS comp/reb) dengan knee velocity
 - ARBModel dengan preload
 - BumpStopModel progressive
 - TotalForce calculation
 Semua dalam native C# untuk performa."

STEP 5 - Roll Dynamics:
"Buat class RollDynamics dengan:
 - Roll stiffness calculation (front/rear/total)
 - Steady-state roll angle
 - Dynamic roll ODE integration
 - Roll center migration
 - Load transfer (geometric + elastic)"

STEP 6 - Bushing Compliance:
"Buat class BushingCompliance dengan:
 - Radial/axial stiffness model
 - Camber/toe compliance
 - Hysteresis model
 - Apply ke kinematic output"

STEP 7 - Ground Detection:
"Buat class GroundDetection dengan:
 - Spherecast per roda
 - Multi-ray option (3 rays)
 - Surface type detection
 - Caching strategy
 - GC-free implementation"

STEP 8 - Integration & Main Loop:
"Integrasikan semua ke SuspensionSystem.UpdateAll():
 - Ground detection → travel calculation
 - Kinematic solver → geometry
 - Force calculation → spring/damper/ARB/stop
 - Load transfer → Fz distribution
 - Tire interface → Pacejka input
 - Visual update → wheel positions
 Target: < 0.5 ms untuk 4 roda di GTX 7 series."

STEP 9 - Setup Editor Tool:
"Buat Unity Editor tool untuk setup suspensi:
 - Visual gizmo untuk hardpoints
 - Camber curve graph
 - Toe curve graph
 - Damper force-velocity curve graph
 - Spring rate calculator
 - Real-time preview di Scene View"

STEP 10 - Preset Configurations:
"Buat ScriptableObject presets untuk:
 - Formula car (pushrod)
 - GT3 car (double wishbone)
 - Road car (MacPherson + multi-link)
 - Rally car (long travel)
 - NASCAR (solid axle)
 Dengan parameter realistis untuk masing-masing."
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

Saya sarankan mulai dari **Data Structures (Step 1)** dan **Kinematic Solver (Step 2)** karena itu adalah fondasi dari seluruh sistem suspensi.