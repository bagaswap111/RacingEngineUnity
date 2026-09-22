# Soft-Body Deformation Engine Hybrid untuk Unity
## Arsitektur Lengkap untuk Game Balap Simulasi (GTX 7 Series Ready)

---

## 1. KONSEP HYBRID: Filosofi Desain

Ide inti: **Jangan simulasi seluruh mobil sebagai soft-body** (seperti BeamNG dengan 5.000+ node). Sebagai gantinya, **bagi mobil menjadi zona-zona dengan tingkat deformasi berbeda**, sesuai kebutuhan visual dan performa.

```
Prinsip Hybrid Damage:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"Deformasi hanya di tempat yang terlihat dan diperlukan,
 rigid di tempat yang membutuhkan performa."
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Level 0: RIGID BODY (Chassis Core)
  → Tidak pernah berubah bentuk
  → Menanggung semua fisika kendaraan
  → 6 DOF rigid body simulation
  → CPU cost: SANGAT RENDAH

Level 1: PANEL DEFORMATION (Vertex Displacement)
  → Deformasi lokal pada panel body
  → Bumper, fender, hood, pintu, quarter panel
  → Vertex-level displacement berdasarkan impact
  → CPU cost: RENDAH-MENENGAH

Level 2: NODE-BEAM ZONES (Simplified Soft Body)
  → Zona struktural yang bisa melengkung/patah
  → Roll cage, chassis rails, suspension mounts
  → 20-80 node per zona (bukan ribuan)
  → CPU cost: MENENGAH

Level 3: FRACTURE & DETACHMENT (Breakable Parts)
  → Bagian yang bisa lepas/pecah
  → Kaca, lampu, spoiler, side mirror, bumper cover
  → Fracture threshold + debris spawning
  → CPU cost: RENDAH (event-based)
```

### Perbandingan dengan BeamNG

| Aspek | BeamNG (Full Soft-Body) | Hybrid Engine (Rancangan Ini) |
|---|---|---|
| Node per mobil | 3.000 - 10.000+ | 200 - 500 total |
| Beam per mobil | 10.000 - 50.000+ | 500 - 1.500 total |
| Physics tick | 60-2000 Hz | 60-120 Hz (hanya zona deformable) |
| CPU per frame | Sangat tinggi | Rendah-Menengah |
| GTX 7 feasible? | ❌ Tidak | ✅ Ya |
| Visual quality crash | 100% | ~75-85% (cukup untuk racing) |
| Racing physics accuracy | ⚠️ Kurang | ✅ Tinggi (chassis tetap rigid) |

---

## 2. ARSITEKTUR ZONA DEFORMASI (Damage Zone Map)

Setiap mobil dibagi menjadi zona-zona deformasi. Setiap zona memiliki tipe, parameter, dan mesh tersendiri.

```
Damage Zone Map (Tampak Atas):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

                    [FRONT BUMPER]
                    Level 3: Fracture
                         ┌──────┐
              ┌──────────┤ HOOD ├──────────┐
              │          └──────┘          │
     [FENDER_L]                    [FENDER_R]
     Level 1: Panel               Level 1: Panel
              │                      │
              ├──────────────────────┤
              │                      │
              │   [CHASSIS CORE]     │
              │   Level 0: Rigid     │
              │   (tidak deform)     │
              │                      │
              │   [ROLL CAGE]        │
              │   Level 2: Node-Beam │
              │                      │
              ├──────────────────────┤
              │                      │
     [DOOR_L]                        [DOOR_R]
     Level 1: Panel               Level 1: Panel
              │                      │
              ├──────────────────────┤
              │   [TRUNK/ENGINE]     │
              │   Level 1: Panel     │
              │                      │
              └──────────┬───────────┘
                    [REAR BUMPER]
                    Level 3: Fracture

         [WHEEL_FL]  [WHEEL_FR]
         Level 2: Node-Beam (suspension deformation)
         [WHEEL_RL]  [WHEEL_RR]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Definisi Zona

```csharp
// Enum untuk tipe zona
public enum DamageZoneType
{
    Rigid,          // Level 0: Tidak pernah deform
    PanelDeform,    // Level 1: Vertex displacement
    NodeBeam,       // Level 2: Simplified soft-body
    Fracture        // Level 3: Bisa pecah/lepas
}

// Konfigurasi setiap zona
public struct DamageZoneConfig
{
    public string ZoneName;
    public DamageZoneType Type;
    
    // Parameter deformasi
    public float ElasticLimit;        // Batas elastis (Newton)
    public float PlasticThreshold;    // Batas deformasi permanen
    public float BreakThreshold;      // Batas patah/lepas
    public float Stiffness;           // Kekakuan zona (N/m)
    public float DampingRatio;        // Rasio peredaman
    
    // Parameter visual
    public float MaxDeformDepth;      // Kedalaman deformasi maks (meter)
    public float DeformRadius;        // Radius pengaruh impact
    public float WrinkleIntensity;    // Intensitas kerutan
    
    // Fracture (untuk Level 3)
    public int FragmentCount;         // Jumlah pecahan
    public float FragmentMass;        // Massa tiap pecahan
    
    // Node-Beam (untuk Level 2)
    public int NodeCount;             // Jumlah node
    public int BeamCount;             // Jumlah beam
    public float BeamBreakForce;      // Gaya untuk memutus beam
    
    // Koneksi ke chassis
    public float3[] AttachmentPoints; // Titik sambung ke chassis rigid
    public float AttachmentStrength;  // Kekuatan sambungan
}
```

### Contoh Konfigurasi Zona untuk Sedan

```
Zone Configuration Example (Sedan):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Zone             │ Type      │ Nodes │ Beams │ Break Force
─────────────────┼───────────┼───────┼───────┼────────────
Chassis Core     │ Rigid     │ -     │ -     │ ∞
Front Bumper     │ Fracture  │ 12    │ 28    │ 8.000 N
Rear Bumper      │ Fracture  │ 12    │ 28    │ 8.000 N
Hood             │ Panel     │ 16    │ 36    │ 6.000 N
Front Fender L   │ Panel     │ 12    │ 26    │ 5.000 N
Front Fender R   │ Panel     │ 12    │ 26    │ 5.000 N
Door L           │ Panel     │ 14    │ 30    │ 7.000 N
Door R           │ Panel     │ 14    │ 30    │ 7.000 N
Trunk            │ Panel     │ 12    │ 26    │ 5.500 N
Roof             │ Panel     │ 16    │ 36    │ 9.000 N
Windshield       │ Fracture  │ 8     │ 14    │ 2.000 N
Rear Window      │ Fracture  │ 8     │ 14    │ 2.000 N
Side Windows     │ Fracture  │ 6     │ 10    │ 1.500 N
Side Mirrors     │ Fracture  │ 4     │ 6     │ 800 N
Spoiler          │ Fracture  │ 6     │ 10    │ 3.000 N
Roll Cage        │ NodeBeam  │ 24    │ 48    │ 25.000 N
Chassis Rails    │ NodeBeam  │ 20    │ 42    │ 30.000 N
Susp Mounts      │ NodeBeam  │ 8×4   │ 12×4  │ 20.000 N
─────────────────┼───────────┼───────┼───────┼────────────
TOTAL            │           │ ~280  │ ~560  │
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 3. SIMPLIFIED NODE-BEAM SYSTEM (Level 2)

Ini adalah inti dari hybrid engine. Kita menggunakan konsep node-beam BeamNG tapi **sangat disederhanakan**.

### 3.1 Struktur Data Node

```csharp
public struct DeformNode
{
    // State
    public float3 Position;           // Posisi saat ini (local space)
    public float3 OriginalPosition;   // Posisi awal (untuk repair)
    public float3 Velocity;           // Kecepatan node
    public float3 Force;              // Akumulasi gaya
    public float  Mass;               // Massa node (kg)
    
    // Damage state
    public float  AccumulatedDamage;  // 0.0 - 1.0
    public bool   IsBroken;           // Node terlepas
    public float  PlasticOffset;      // Deformasi permanen
    
    // Koneksi
    public int[]  ConnectedBeams;     // Index beam yang terhubung
    public int    ZoneIndex;          // Index zona
    
    // Skinning (untuk mesh deformation)
    public int[]  SkinnedVertices;    // Vertex mesh yang terikat
    public float[] SkinnedWeights;    // Bobot ikatan
}

public struct DeformBeam
{
    public int NodeA;                 // Index node A
    public int NodeB;                 // Index node B
    public float RestLength;          // Panjang awal
    public float CurrentLength;       // Panjang saat ini
    public float Stiffness;           // Spring constant (N/m)
    public float Damping;             // Damping coefficient
    public float BreakForce;          // Gaya untuk patah (N)
    public float CurrentStress;       // Stress saat ini (0-1+)
    public bool  IsBroken;            // Beam sudah patah?
    public BeamType Type;             // Tipe beam
}

public enum BeamType
{
    Structural,    // Beam utama (chassis rail) - sangat kaku
    Support,       // Beam pendukung - kaku sedang
    Panel,         // Beam panel body - lebih fleksibel
    Attachment     // Beam sambungan ke chassis rigid
}
```

### 3.2 Rumus Node-Beam Physics

```
Node-Beam Physics (per physics tick):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Untuk setiap BEAM yang belum patah:

1. Hitung panjang saat ini:
   L_current = |Pos_A - Pos_B|

2. Hitung strain (regangan):
   ε = (L_current - L_rest) / L_rest

3. Hitung gaya spring (Hooke's Law):
   F_spring = -k · (L_current - L_rest)
   
   k = stiffness beam (N/m)
   Arah gaya: sepanjang beam (dari A ke B atau sebaliknya)

4. Hitung gaya damping:
   v_rel = Vel_A - Vel_B
   v_along = dot(v_rel, normalize(Pos_A - Pos_B))
   F_damp = -c · v_along
   
   c = damping coefficient

5. Hitung total gaya pada beam:
   F_beam = F_spring + F_damp
   Arah: normalize(Pos_B - Pos_A)

6. Hitung stress:
   stress = |F_beam| / BreakForce
   
   IF stress > 1.0:
       beam.IsBroken = true
       SpawnFractureEffect(beam.Position)

7. Apply gaya ke node:
   Node_A.Force += F_beam · direction
   Node_B.Force -= F_beam · direction
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Untuk setiap NODE yang belum broken:

1. Akumulasi semua gaya:
   F_total = F_gravity + F_beam_sum + F_impact + F_attachment

2. Integrasi (Semi-implicit Euler):
   Vel += (F_total / Mass) · dt
   Vel *= (1 - damping_global)     // Global damping
   Pos += Vel · dt

3. Plastic deformation check:
   displacement = |Pos - OriginalPos|
   IF displacement > ElasticLimit AND NOT in_plastic_mode:
       // Mulai deformasi plastis
       PlasticOffset += (displacement - ElasticLimit) · plastic_factor
       AccumulatedDamage += (displacement - ElasticLimit) / BreakThreshold
   
4. Constraint ke chassis rigid:
   // Node tidak boleh menembus chassis core
   IF InsideChassisCore(Pos):
       Pos = ProjectToChassisSurface(Pos)
       Vel = Reflect(Vel, chassis_normal) · restitution
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3.3 Algoritma Node-Beam Update

```
ALGORITMA: NodeBeamSystem.Update
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  dt (delta time), activeZones (zona yang perlu update)
OUTPUT: updated node positions, broken beams

1. UNTUK SETIAP zona aktif (NodeBeam type):

   1a. Reset forces:
       FOR each node in zone:
           node.Force = float3(0, 0, 0)
           node.Force += float3(0, -9.81, 0) * node.Mass  // Gravity

   1b. Calculate beam forces:
       FOR each beam in zone WHERE NOT beam.IsBroken:
           
           dir = normalize(node[beam.A].Pos - node[beam.B].Pos)
           L_current = distance(node[beam.A].Pos, node[beam.B].Pos)
           strain = (L_current - beam.RestLength) / beam.RestLength
           
           // Spring force
           F_spring = beam.Stiffness * (L_current - beam.RestLength)
           
           // Damping force
           v_rel = dot(node[beam.A].Vel - node[beam.B].Vel, dir)
           F_damp = beam.Damping * v_rel
           
           // Total
           F_total = (F_spring + F_damp) * dir
           
           // Apply
           node[beam.A].Force -= F_total
           node[beam.B].Force += F_total
           
           // Stress check
           beam.CurrentStress = abs(F_spring) / beam.BreakForce
           IF beam.CurrentStress > 1.0:
               beam.IsBroken = true
               zone.AccumulatedDamage += 0.05
               ScheduleFractureFX(beam.MidPoint)

   1c. Integrate nodes:
       FOR each node in zone WHERE NOT node.IsBroken:
           // Semi-implicit Euler
           node.Vel += (node.Force / node.Mass) * dt
           node.Vel *= 0.98  // Global damping
           node.Pos += node.Vel * dt
           
           // Plastic deformation
           disp = length(node.Pos - node.OriginalPos)
           IF disp > zone.ElasticLimit:
               plastic_ratio = (disp - zone.ElasticLimit) / zone.MaxDeformDepth
               node.AccumulatedDamage += plastic_ratio * dt * 2.0
               node.AccumulatedDamage = clamp(node.AccumulatedDamage, 0, 1)
           
           // Clamp maximum deformation
           IF disp > zone.MaxDeformDepth:
               node.Pos = node.OriginalPos + 
                          normalize(node.Pos - node.OriginalPos) * zone.MaxDeformDepth
               node.Vel *= 0.1  // Kill velocity at max deform

   1d. Attachment constraints:
       FOR each attachment point:
           // Spring connection to rigid chassis
           F_attach = -k_attach * (node.Pos - attachTarget) 
                      - c_attach * node.Vel
           node.Force += F_attach
           
           // Break if force too high
           IF |F_attach| > zone.AttachmentStrength:
               DetachZone(zone)

2. RETURN updated positions, new broken beams
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 4. PANEL DEFORMATION SYSTEM (Level 1)

Untuk panel body (bumper, fender, hood, pintu), gunakan **vertex displacement** yang lebih ringan daripada node-beam penuh.

### 4.1 Konsep Panel Deformation

```
Panel Deformation Approach:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Panel body TIDAK menggunakan node-beam penuh.
Sebagai gantinya:

1. Setiap panel memiliki SIMPLIFIED SKELETON:
   - 4-16 control points (bukan ratusan)
   - Control points terhubung ke chassis rigid
   - Control points bisa bergeser saat impact

2. Mesh vertices di-SKIN ke control points:
   - Setiap vertex terikat ke 1-3 control points
   - Weight-based interpolation
   - Saat control point bergeser, vertex ikut

3. Deformasi lokal (dent/dimple):
   - Vertex displacement langsung di sekitar impact point
   - Menggunakan falloff function
   - Tidak mempengaruhi seluruh panel

Hasil: Deformasi terlihat realistis dengan CPU cost minimal
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.2 Control Point System

```csharp
public struct PanelControlPoint
{
    public float3 Position;
    public float3 OriginalPosition;
    public float3 Velocity;
    public float  Mass;
    public float  Stiffness;
    public float  Damping;
    public float  Damage;           // 0.0 - 1.0
    public bool   IsDetached;       // Lepas dari chassis
    public int    AttachedToZone;   // Index zona chassis
}

public struct PanelMeshBinding
{
    public int[] VertexIndices;         // Index vertex mesh
    public int[] ControlPointIndices;   // Control point yang mempengaruhi
    public float[] Weights;             // Bobot pengaruh (0-1)
}
```

### 4.3 Algoritma Panel Deformation

```
ALGORITMA: PanelDeformation.ApplyImpact
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  panel, impactPoint, impactForce, impactNormal
OUTPUT: deformed mesh

1. Transform impact point ke local space panel:
   localImpact = panel.Transform.InverseTransformPoint(impactPoint)
   localNormal = panel.Transform.InverseTransformDirection(impactNormal)

2. Hitung energi impact:
   E_impact = 0.5 * effectiveMass * |impactVelocity|²
   
3. Tentukan radius deformasi:
   R_deform = panelConfig.DeformRadius * pow(E_impact / E_reference, 0.33)
   R_deform = clamp(R_deform, 0.05, panelConfig.MaxDeformRadius)

4. UNTUK SETIAP CONTROL POINT dalam panel:
   dist = distance(controlPoint.Position, localImpact)
   
   IF dist < R_deform:
       // Falloff (smoothstep)
       t = dist / R_deform
       falloff = 1 - t*t*(3 - 2*t)  // Smoothstep
       
       // Hitung displacement
       displacement = localNormal * E_impact * falloff * deformFactor
       
       // Tambahkan buckle/wrinkle effect
       IF E_impact > panelConfig.BuckleThreshold:
           wrinkle = sin(dist * wrinkleFreq) * wrinkleAmp * falloff
           displacement += panelNormal * wrinkle
       
       // Apply ke control point
       controlPoint.Position += displacement
       controlPoint.Damage += falloff * (E_impact / panelConfig.BreakThreshold)
       
       // Plastic deformation (tidak kembali sepenuhnya)
       IF controlPoint.Damage > panelConfig.ElasticLimit:
           plasticRatio = (controlPoint.Damage - panelConfig.ElasticLimit) 
                          / (1.0 - panelConfig.ElasticLimit)
           controlPoint.Position = lerp(
               controlPoint.Position,
               controlPoint.OriginalPosition + displacement * plasticRatio,
               plasticRatio
           )

5. Update mesh vertices berdasarkan control points:
   FOR each vertex in panel.Mesh:
       newPos = float3(0,0,0)
       totalWeight = 0
       
       FOR each binding (vertexIndex, cpIndex, weight):
           IF binding.VertexIndex == currentVertex:
               newPos += panel.ControlPoints[cpIndex].Position * weight
               totalWeight += weight
       
       IF totalWeight > 0:
           vertex.Position = newPos / totalWeight

6. Recalculate normals:
   panel.Mesh.RecalculateNormals()
   panel.Mesh.RecalculateTangents()

7. Update damage state:
   panel.TotalDamage = average(allControlPoints.Damage)
   IF panel.TotalDamage > 0.9:
       ScheduleDetach(panel)

8. RETURN deformed mesh
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4.4 Vertex-Level Dent (Detail Deformation)

Untuk dent/lekuk kecil yang tidak memerlukan control point movement:

```
ALGORITMA: PanelDeformation.ApplyDent
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  mesh, impactPoint, impactDir, dentDepth, dentRadius
OUTPUT: dented mesh

1. UNTUK SETIAP VERTEX dalam mesh:
   dist = distance(vertex.Position, impactPoint)
   
   IF dist < dentRadius:
       // Falloff curve (cosine untuk smooth edge)
       t = dist / dentRadius
       falloff = cos(t * PI * 0.5)  // 1 di center, 0 di edge
       falloff = falloff * falloff   // Square untuk lebih tajam
       
       // Displacement
       dent = impactDir * dentDepth * falloff
       
       // Tambahkan noise untuk realism (hindari dent sempurna)
       noise = perlinNoise(vertex.Position * noiseScale) * 0.1
       dent += impactDir * dentDepth * noise * falloff
       
       // Apply
       vertex.Position += dent
       
       // Simpan untuk repair
       vertex.DeformationOffset += dent

2. Recalculate normals & tangents
3. Update mesh bounding box

RETURN dented mesh
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 5. FRACTURE & DETACHMENT SYSTEM (Level 3)

Untuk bagian yang bisa pecah atau lepas: kaca, lampu, bumper, spoiler, side mirror.

### 5.1 Fracture Model

```
Fracture System Design:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Pre-Fractured Meshes:
  Setiap breakable part sudah di-pre-fracture saat authoring:
  
  Kaca depan → 8-12 fragmen (Voronoi pattern)
  Bumper     → 4-6 fragmen
  Lampu      → 3-5 fragmen
  Spoiler    → 3-4 fragmen
  
  Fragmen-fragmen ini "tersembunyi" sampai fracture terjadi.
  Saat fracture: mesh utuh disembunyikan, fragmen diaktifkan.

Attachment Points:
  Setiap part memiliki 2-6 attachment point ke chassis.
  Setiap attachment memiliki break force threshold.
  
  IF impact_force > attachment.BreakForce:
      attachment.IsBroken = true
      IF semua attachment broken:
          DetachPart()
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.2 Algoritma Fracture

```
ALGORITMA: FractureSystem.CheckAndBreak
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  part, impactForce, impactPoint
OUTPUT: fractured parts (jika terjadi fracture)

1. Hitung jarak impact ke setiap attachment point:
   FOR each attachment in part.Attachments:
       dist = distance(attachment.Position, impactPoint)
       
       // Force distribution (inverse distance weighting)
       influence = 1.0 / max(dist, 0.01)
       localForce = impactForce * influence / totalInfluence
       
       // Check break
       IF localForce > attachment.BreakForce:
           attachment.IsBroken = true
           attachment.BreakTime = currentTime
           SpawnSparkFX(attachment.Position)

2. Check apakah part harus detach:
   brokenCount = count(attachments WHERE IsBroken)
   totalCount = count(attachments)
   
   IF brokenCount == totalCount:
       // Full detach
       DetachPart(part)
   ELSE IF brokenCount >= ceil(totalCount * 0.5):
       // Partial detach (part masih menempel tapi miring)
       part.IsHanging = true
       part.HangingPivot = FindRemainingAttachment()
       part.SwingFactor = 0.5  // Part bergoyang

3. Check fracture (pecah):
   IF impactForce > part.FractureThreshold:
       FracturePart(part, impactPoint, impactForce)

4. RETURN fracture results
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

ALGORITMA: FractureSystem.FracturePart
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  part, impactPoint, impactForce
OUTPUT: fragment rigid bodies

1. Sembunyikan mesh utuh:
   part.IntactMesh.SetActive(false)

2. Aktifkan fragmen-fragmen:
   FOR each fragment in part.Fragments:
       fragment.Mesh.SetActive(true)
       
       // Berikan physics
       fragment.RigidBody.isKinematic = false
       fragment.RigidBody.mass = fragment.Mass
       
       // Hitung kecepatan awal fragmen
       distToImpact = distance(fragment.Center, impactPoint)
       falloff = 1.0 / max(distToImpact, 0.1)
       
       fragment.RigidBody.velocity = 
           partVelocity + impactDirection * impactForce * falloff * fragmentSpeedFactor
       
       // Tambahkan angular velocity acak
       fragment.RigidBody.angularVelocity = 
           randomRotation * fragmentSpinFactor

3. Spawn efek:
   SpawnGlassShatterFX(impactPoint)  // Untuk kaca
   SpawnPlasticDebrisFX(impactPoint) // Untuk plastik
   SpawnMetalScrapsFX(impactPoint)   // Untuk metal

4. Set timer untuk cleanup:
   FOR each fragment:
       ScheduleDestroy(fragment, lifetime: 10-30 seconds)

5. Update vehicle state:
   vehicle.MissingParts.Add(part.PartID)
   vehicle.AeroDamage += part.AeroContribution  // Aero berubah
   vehicle.Weight -= part.Mass  // Berat berkurang

RETURN fragments
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5.3 Voronoi Pre-Fracture (Authoring Time)

```
Pre-Fracture Pipeline (Offline / Editor):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk setiap breakable part:

1. Generate seed points dalam volume part:
   N = fragmentCount
   seeds = PoissonDiskSampling(part.Bounds, N)
   
2. Generate Voronoi cells:
   cells = Voronoi3D(seeds, part.Bounds)
   
3. Clip cells dengan mesh part:
   FOR each cell:
       fragment = BooleanIntersect(part.Mesh, cell)
       IF fragment.IsValid:
           fragments.Add(fragment)
   
4. Generate contact points antar fragmen:
   FOR each pair (fragA, fragB):
       IF fragmentsTouch(fragA, fragB):
           contactPoints.Add(fragA, fragB, sharedVertices)
   
5. Simpan data fracture ke asset:
   - Fragment meshes
   - Fragment masses
   - Contact point data
   - Attachment points ke chassis

Tool ini dijalankan di EDITOR, bukan runtime.
Hasil di-save sebagai asset dan di-load saat game berjalan.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 6. COLLISION DETECTION & IMPACT RESOLUTION

### 6.1 Collision Mesh Strategy

```
Collision Mesh Hierarchy:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk performa di GTX 7, gunakan MULTI-LAYER collision:

Layer 1: SIMPLE COLLIDER (untuk physics utama)
  - Box collider / Convex hull sederhana
  - Digunakan untuk vehicle-to-vehicle collision
  - Sangat cepat
  - Update: setiap physics tick

Layer 2: ZONE COLLIDERS (untuk damage detection)
  - Satu collider per damage zone
  - Box/Sphere/Capsule sederhana
  - Digunakan untuk menentukan ZONA mana yang kena
  - Update: setiap physics tick

Layer 3: DEFORM COLLIDERS (untuk detail impact)
  - Mesh collider simplified (50-200 triangles)
  - Hanya aktif saat terjadi impact
  - Digunakan untuk menghitung impact point & normal presisi
  - Update: HANYA saat impact terdeteksi (on-demand)

Dengan strategi ini:
  - 99% frame: hanya Layer 1 & 2 (murah)
  - 1% frame (saat crash): Layer 3 aktif (lebih mahal tapi jarang)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 6.2 Impact Detection Algorithm

```
ALGORITMA: CollisionSystem.DetectAndResolve
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  collision event dari Unity Physics
OUTPUT: impact data, damage application

1. Early rejection:
   IF collision.relativeVelocity < minDamageVelocity (0.5 m/s):
       RETURN  // Terlalu pelan, tidak ada damage

2. Hitung impact parameters:
   impactPoint = collision.GetContact(0).point
   impactNormal = collision.GetContact(0).normal
   impactVelocity = collision.relativeVelocity
   impactSpeed = |impactVelocity|
   
   // Effective mass (tergantung sudut impact)
   cosAngle = dot(impactVelocity, impactNormal) / impactSpeed
   effectiveMass = vehicle.Mass * cosAngle
   
   // Impact energy
   E_impact = 0.5 * effectiveMass * impactSpeed²

3. Tentukan zona yang terkena:
   hitZone = DetermineDamageZone(impactPoint)
   
   IF hitZone == null:
       RETURN  // Tidak ada zona damage di titik ini

4. Distribusikan impact ke zona:
   // Impact tidak hanya di satu titik, tapi menyebar
   affectedZones = GetNearbyZones(impactPoint, spreadRadius)
   
   FOR each zone in affectedZones:
       distToZone = distance(zone.Center, impactPoint)
       influence = 1.0 - (distToZone / spreadRadius)
       influence = max(influence, 0)
       
       zoneImpulse = impactVelocity * effectiveMass * influence
       ApplyImpactToZone(zone, impactPoint, zoneImpulse, impactNormal)

5. Apply ke physics kendaraan:
   // Impulse ke rigid body utama
   vehicle.RigidBody.AddForceAtPosition(
       impactVelocity * effectiveMass * 0.7,  // 70% ke rigid body
       impactPoint,
       ForceMode.Impulse
   )
   // 30% diserap oleh deformasi

6. Spawn effects:
   IF impactSpeed > 2.0:
       SpawnSparks(impactPoint, impactNormal, impactSpeed)
   IF impactSpeed > 5.0:
       SpawnDebris(impactPoint, impactNormal)
   IF hitZone.Type == Fracture AND impactSpeed > 3.0:
       SpawnGlassShatter(impactPoint)

7. Play audio:
   PlayImpactSound(hitZone.Material, impactSpeed)

8. Log untuk telemetry:
   Telemetry.LogImpact(impactPoint, impactSpeed, E_impact, hitZone.Name)

RETURN impact results
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 6.3 Impact Force Distribution ke Node-Beam

```
ALGORITMA: CollisionSystem.ApplyImpactToNodeBeamZone
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  zone, impactPoint, impactImpulse, impactNormal
OUTPUT: node forces

1. Transform ke local space zona:
   localImpact = zone.Transform.InverseTransformPoint(impactPoint)
   localImpulse = zone.Transform.InverseTransformDirection(impactImpulse)

2. Hitung affected nodes:
   FOR each node in zone.Nodes:
       dist = distance(node.Position, localImpact)
       
       IF dist < zone.ImpactRadius:
           // Falloff
           falloff = 1.0 - (dist / zone.ImpactRadius)
           falloff = falloff * falloff  // Quadratic
           
           // Apply impulse
           node.Force += localImpulse * falloff
           
           // Tambahkan slight random untuk realism
           node.Force += randomDir * |localImpulse| * 0.05 * falloff

3. IF zone.Type == PanelDeform:
       // Juga apply vertex-level dent
       PanelDeformation.ApplyDent(
           zone.Mesh,
           localImpact,
           normalize(localImpulse),
           dentDepth: |impactImpulse| * dentFactor,
           dentRadius: zone.ImpactRadius * 0.5
       )

4. IF zone.Type == Fracture:
       FractureSystem.CheckAndBreak(zone, |impactImpulse|, impactPoint)

RETURN node forces applied
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 7. MESH SKINNING & VISUAL UPDATE

### 7.1 Vertex-to-Node Binding

```
Mesh Skinning untuk Deformasi:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Setiap vertex pada mesh zona deformable terikat ke 
1-3 node/control point terdekat.

Binding dilakukan saat AUTHORING TIME (di Editor):

1. Untuk setiap vertex pada mesh:
   a. Cari 3 node/control point terdekat
   b. Hitung weight berdasarkan inverse distance:
      w_i = (1/d_i) / Σ(1/d_j)  untuk j = 1,2,3
   c. Simpan binding: (vertexIndex, nodeIndices, weights)

2. Simpan sebagai asset:
   MeshBindingAsset {
       vertexBindings: [
           { vertex: 0, nodes: [2,3,5], weights: [0.6, 0.3, 0.1] },
           { vertex: 1, nodes: [3,5],   weights: [0.7, 0.3] },
           ...
       ]
   }

Saat RUNTIME:
   Setiap kali node bergerak, update vertex:
   
   FOR each binding:
       vertex.Pos = Σ (node[i].Pos * weight[i])
   
   Ini sangat cepat karena hanya matrix-vector multiply.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 7.2 Algoritma Visual Update

```
ALGORITMA: DeformMeshUpdater.UpdateVisuals
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  zones dengan node positions yang sudah diupdate
OUTPUT: updated meshes

1. UNTUK SETIAP zona yang berubah (dirty flag):

   1a. Update mesh vertices dari node positions:
       FOR each vertexBinding in zone.MeshBindings:
           newPos = float3(0, 0, 0)
           
           FOR i in 0..binding.NodeCount:
               nodeIdx = binding.NodeIndices[i]
               weight = binding.Weights[i]
               newPos += zone.Nodes[nodeIdx].Position * weight
           
           meshVertices[binding.VertexIndex] = newPos

   1b. Apply vertex-level deformation offsets:
       FOR each vertex in mesh:
           meshVertices[vertex] += vertex.DeformationOffset

   1c. Write ke mesh:
       mesh.SetVertices(meshVertices)
       mesh.RecalculateNormals()
       mesh.RecalculateTangents()
       mesh.RecalculateBounds()

   1d. Update LOD jika perlu:
       IF distance(camera, vehicle) > lodDistance:
           // Gunakan LOD mesh yang lebih sederhana
           SwitchToLODMesh(zone, lodLevel)

2. Clear dirty flags
3. Batch mesh updates (jika multiple zones)

RETURN updated meshes
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 7.3 Normal Recalculation Optimization

```
Optimasi RecalculateNormals:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RecalculateNormals() untuk seluruh mesh setiap frame 
SANGAT MAHAL. Optimasi:

1. Dirty Region Only:
   Hanya recalculate normals untuk vertex yang berubah.
   Track vertex mana yang berubah, hanya proses tetangga.

2. Incremental Normal Update:
   // Simpan normal awal
   // Hitung delta posisi vertex
   // Adjust normal berdasarkan delta (approximate)
   
   deltaPos = newPos - oldPos
   deltaNormal = cross(deltaPos, faceNormal) * normalAdjustFactor
   newNormal = normalize(oldNormal + deltaNormal)

3. Async Normal Update:
   // Update posisi mesh langsung (frame N)
   // Update normals di frame N+1 atau N+2
   // Mata manusia tidak notice delay 1-2 frame pada normals

4. Pre-baked Normal Maps:
   // Untuk damage ringan, gunakan normal map yang sudah
   // di-bake untuk scratches dan dents kecil
   // Hanya deformasi besar yang recalculate normals

Implementasi: Gunakan Job System untuk parallel 
normal recalculation.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 8. INTEGRASI DENGAN VEHICLE PHYSICS

### 8.1 Koneksi Damage → Performance

Kerusakan visual harus mempengaruhi performa kendaraan:

```
Damage-to-Performance Mapping:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. AERO DAMAGE:
   frontDamage = AverageDamage(zones: FrontBumper, Hood, FrontFenders)
   rearDamage = AverageDamage(zones: RearBumper, Trunk, Spoiler)
   
   // Downforce berkurang
   aero.Cl_front *= (1.0 - frontDamage * 0.4)
   aero.Cl_rear *= (1.0 - rearDamage * 0.4)
   
   // Drag bertambah (body penyok = tidak aerodinamis)
   aero.Cd *= (1.0 + (frontDamage + rearDamage) * 0.15)
   
   // Balance berubah
   aero.AeroBalance_front += (rearDamage - frontDamage) * 0.1

2. SUSPENSION DAMAGE:
   FOR each wheel:
       suspDamage = AverageDamage(zones: SuspMount_FL/FR/RL/RR)
       
       IF suspDamage > 0.3:
           // Camber berubah (mobil miring)
           suspension.Camber[i] += suspDamage * 3.0  // derajat
           
       IF suspDamage > 0.6:
           // Spring rate berkurang (suspensi lemah)
           suspension.SpringRate[i] *= (1.0 - suspDamage * 0.5)
           
       IF suspDamage > 0.9:
           // Suspensi patah
           suspension.IsBroken[i] = true

3. ENGINE DAMAGE:
   engineDamage = AverageDamage(zones: Hood, FrontBumper)
   
   IF engineDamage > 0.4:
       // Radiator rusak → overheating
       cooling.Efficiency *= (1.0 - engineDamage * 0.6)
       
   IF engineDamage > 0.7:
       // Engine misfire
       engine.TorqueOutput *= (0.7 + random() * 0.3)

4. TIRE/WHEEL DAMAGE:
   wheelDamage = AverageDamage(zones: Wheel_FL/FR/RL/RR)
   
   IF wheelDamage > 0.5:
       // Ban rusak → grip berkurang
       tire.GripMultiplier[i] *= (1.0 - wheelDamage * 0.7)
       
   IF wheelDamage > 0.8:
       // Ban bocor
       tire.IsPunctured[i] = true

5. WEIGHT REDISTRIBUTION:
   // Part yang lepas mengurangi berat
   FOR each detachedPart:
       vehicle.TotalMass -= detachedPart.Mass
       vehicle.CoG += detachedPart.CoGOffset * -1  // Adjust CoG
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 8.2 Algoritma Damage-Performance Integration

```
ALGORITMA: DamagePerformanceLink.Update
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  vehicleState, allZoneDamages
OUTPUT: modified vehicle parameters

1. Hitung damage per kategori:
   aeroDamage_front = GetZoneDamage("FrontBumper", "Hood", "Fender_FL", "Fender_FR")
   aeroDamage_rear = GetZoneDamage("RearBumper", "Trunk", "Spoiler")
   aeroDamage_side = GetZoneDamage("Door_L", "Door_R", "Fender_FL", "Fender_RR")
   suspDamage[4] = GetZoneDamage("SuspMount_FL/FR/RL/RR")
   engineDamage = GetZoneDamage("EngineBay", "Radiator")
   bodyDamage = AverageAllZones()

2. Update aerodinamika:
   vehicle.Aero.Cl_front *= (1.0 - aeroDamage_front * 0.35)
   vehicle.Aero.Cl_rear *= (1.0 - aeroDamage_rear * 0.35)
   vehicle.Aero.Cd *= (1.0 + bodyDamage * 0.2)
   
   // Crosswind sensitivity meningkat dengan damage
   vehicle.Aero.SideWindSensitivity *= (1.0 + aeroDamage_side * 0.5)

3. Update suspensi:
   FOR i in 0..3:
       IF suspDamage[i] > 0.3:
           vehicle.Suspension.SpringRate[i] *= (1.0 - suspDamage[i] * 0.4)
           vehicle.Suspension.Damping[i] *= (1.0 - suspDamage[i] * 0.3)
           vehicle.Suspension.Camber[i] += suspDamage[i] * 2.5
           vehicle.Suspension.Toe[i] += suspDamage[i] * 1.0

4. Update engine:
   IF engineDamage > 0.3:
       vehicle.Engine.MaxTorque *= (1.0 - engineDamage * 0.3)
       vehicle.Engine.CoolingEfficiency *= (1.0 - engineDamage * 0.5)
   
   IF engineDamage > 0.7:
       vehicle.Engine.MisfireChance = engineDamage * 0.3

5. Update weight:
   totalDetachedMass = Sum(detachedParts.Mass)
   vehicle.TotalMass = vehicle.BaseMass - totalDetachedMass

6. RETURN updated vehicle parameters
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 9. PERFORMANCE STRATEGY UNTUK GTX 7 SERIES

### 9.1 Budget Frame

```
Performance Budget (Target: 60 FPS di GTX 750 Ti):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Total frame time: 16.67 ms

Rendering (URP):           ~8.0 ms  (48%)
Vehicle Physics (Pacejka): ~1.5 ms  (9%)
Damage System:             ~1.0 ms  (6%)  ← BUDGET KITA
Audio:                     ~0.5 ms  (3%)
UI/HUD:                    ~0.5 ms  (3%)
Unity Overhead:            ~1.0 ms  (6%)
Headroom:                  ~4.17 ms (25%)

Damage System Budget: 1.0 ms per frame
  - Node-Beam update:     ~0.4 ms (hanya zona aktif)
  - Mesh vertex update:   ~0.3 ms (hanya zona dirty)
  - Normal recalc:        ~0.2 ms (incremental)
  - Fracture check:       ~0.1 ms (event-based)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 9.2 Optimasi Kunci

```
Optimization Strategies:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. LAZY EVALUATION:
   Node-beam HANYA diupdate saat:
   - Terjadi impact (event-driven)
   - 3 detik setelah impact (untuk settling)
   - Saat kamera dekat (< 10 meter)
   
   Di luar itu, zona deformable FROZEN (tidak update).
   
   Penghematan: ~90% CPU cost saat tidak ada crash.

2. ZONE ACTIVATION:
   Hanya zona yang terkena impact yang aktif.
   Zona lain tetap rigid.
   
   MAX 2 zona aktif bersamaan.
   Jika > 2 zona aktif, freeze zona paling lama.

3. ASYNC MESH UPDATE:
   - Frame N: Impact terdeteksi, node positions diupdate
   - Frame N+1: Mesh vertices diupdate
   - Frame N+2: Normals recalculated
   
   Spread cost over 3 frames.

4. JOB SYSTEM + BURST:
   Semua perhitungan node-beam menggunakan:
   - Unity.Jobs (IJobParallelFor)
   - Unity.Burst (SIMD compilation)
   - NativeArray (no GC pressure)
   
   Parallelism: setiap zona = 1 job.

5. LOD DAMAGE:
   Jarak kamera > 20m:
     - Node-beam resolution / 2
     - Mesh vertex update / 2
     - Normal recalc: skip
   
   Jarak kamera > 50m:
     - Node-beam: disabled
     - Mesh: pre-baked damage texture
     - Hanya rigid body
   
   Jarak kamera > 100m:
     - Semua damage: disabled
     - Gunakan billboard/impostor

6. POOLING:
   Pre-allocate semua debris, fragments, particles.
   Tidak ada runtime allocation.
   
   Fragment pool: 200 objects
   Debris pool: 500 objects
   Spark particles: 1000 particles

7. GC-FREE:
   Semua data structure menggunakan NativeArray/NativeList.
   Tidak ada boxing, tidak ada LINQ di hot path.
   Zero GC allocation per frame.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 9.3 Damage LOD System

```
ALGORITMA: DamageLOD.Update
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  cameraPosition, vehiclePosition, allZones
OUTPUT: LOD level per zone

1. dist = distance(cameraPosition, vehiclePosition)

2. IF dist < 10.0:
       lodLevel = 0  // Full detail
       nodeBeamEnabled = true
       meshUpdateEnabled = true
       normalRecalcEnabled = true
       fractureEnabled = true
       
   ELSE IF dist < 20.0:
       lodLevel = 1  // Medium
       nodeBeamEnabled = true
       meshUpdateEnabled = true
       normalRecalcEnabled = false  // Skip normals
       fractureEnabled = true
       
   ELSE IF dist < 50.0:
       lodLevel = 2  // Low
       nodeBeamEnabled = false
       meshUpdateEnabled = false
       normalRecalcEnabled = false
       fractureEnabled = true
       // Gunakan pre-baked damage texture
       ApplyDamageTexture(vehicle, totalDamage)
       
   ELSE:
       lodLevel = 3  // Minimal
       nodeBeamEnabled = false
       meshUpdateEnabled = false
       fractureEnabled = false
       // Hanya visual damage color tint
       ApplyDamageTint(vehicle, totalDamage)

3. UNTUK SETIAP zone:
       zone.LODLevel = lodLevel
       zone.IsActive = (lodLevel <= 1) AND zone.IsDirty

RETURN LOD settings
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 10. REPAIR SYSTEM

### 10.1 Repair Model

```
Repair System Design:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk game balap, repair biasanya terjadi di PIT STOP.

Repair Levels:
  Level 1: Quick Fix (5 detik)
    - Reset deformasi ringan (< 30% damage)
    - Kembalikan panel ke posisi mendekati original
    - Tidak memperbaiki kerusakan struktural
    
  Level 2: Full Repair (15-30 detik)
    - Reset semua deformasi
    - Ganti part yang rusak
    - Perbaiki kerusakan struktural
    - Reset aero ke original
    
  Level 3: Component Replacement (60+ detik)
    - Ganti seluruh zona
    - Reset semua ke kondisi baru
    - Termasuk engine, gearbox, suspension
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 10.2 Algoritma Repair

```
ALGORITMA: RepairSystem.Execute
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
INPUT:  vehicle, repairLevel, dt
OUTPUT: repaired vehicle

1. UNTUK SETIAP zona dengan damage > 0:

   IF repairLevel >= 1 (Quick Fix):
       IF zone.TotalDamage < 0.3:
           // Lerp kembali ke original
           repairSpeed = 2.0  // units per second
           
           FOR each node in zone:
               node.Position = lerp(
                   node.Position,
                   node.OriginalPosition,
                   repairSpeed * dt
               )
               node.Velocity = float3(0,0,0)
           
           zone.TotalDamage *= (1.0 - repairSpeed * dt)
           
           IF zone.TotalDamage < 0.01:
               zone.TotalDamage = 0
               ResetZoneMesh(zone)

   IF repairLevel >= 2 (Full Repair):
       // Reset semua node ke original
       FOR each node in zone:
           node.Position = node.OriginalPosition
           node.Velocity = float3(0,0,0)
           node.AccumulatedDamage = 0
           node.PlasticOffset = 0
       
       // Reset beams
       FOR each beam in zone:
           beam.IsBroken = false
           beam.CurrentStress = 0
       
       // Reset mesh
       ResetZoneMesh(zone)
       zone.TotalDamage = 0
       
       // Re-attach broken parts
       FOR each detachedPart in vehicle.DetachedParts:
           IF detachedPart.CanReattach:
               ReattachPart(detachedPart)

   IF repairLevel >= 3 (Component Replacement):
       // Ganti seluruh zona
       FOR each zone in vehicle.Zones:
           zone.ResetToFactory()
       
       // Reset vehicle performance
       vehicle.Aero = vehicle.Aero_Original
       vehicle.Suspension = vehicle.Suspension_Original
       vehicle.Engine.Health = 1.0
       vehicle.TotalMass = vehicle.BaseMass
       
       // Reset mesh semua zona
       FOR each zone:
           ResetZoneMesh(zone)

2. Update visual:
   FOR each zone:
       UpdateMeshFromNodes(zone)
       RecalculateNormals(zone)

3. RETURN repaired vehicle
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 11. VISUAL EFFECTS PIPELINE

### 11.1 Impact Effects

```
Visual Effects saat Impact:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. SPARKS (Percikan Api):
   Trigger: impactSpeed > 2 m/s AND material == metal
   Particle system: GPU particles
   Count: 10-50 tergantung impactSpeed
   Lifetime: 0.2-0.5 detik
   Color: orange-yellow gradient
   
2. DEBRIS (Serpihan):
   Trigger: impactSpeed > 3 m/s
   Pooled rigid body objects
   Count: 3-15 tergantung impactEnergy
   Shapes: small triangles, rectangles
   Lifetime: 5-10 detik (fade out)
   
3. GLASS SHATTER (Pecahan Kaca):
   Trigger: fracture zone == glass
   Pre-fractured glass fragments
   Count: 8-12 per panel
   Physics: rigid body dengan gravity
   Sound: glass break
   
4. SMOKE (Asap):
   Trigger: engineDamage > 0.5 OR fire
   Particle system: billboard smoke
   Color: gray → black (tergantung damage)
   Duration: continuous sampai repair
   
5. FIRE (Api):
   Trigger: engineDamage > 0.8 AND fuelLeak
   Particle system: fire + smoke
   Light: point light (orange, flickering)
   Damage over time ke zona terdekat
   
6. SCRATCH MARKS (Bekas Goresan):
   Trigger: sliding contact (scrape)
   Decal projection pada mesh
   Texture: scratch normal map + roughness map
   Persistent sampai repair
   
7. DIRT/DUST (Debu):
   Trigger: impact dengan ground/barrier
   Particle system: dust cloud
   Color: tergantung material (concrete, grass, gravel)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 11.2 Damage Textures (Procedural)

```
Procedural Damage Textures:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Untuk damage ringan (scratches, dents kecil), gunakan 
procedural texture modification:

1. SCRATCHES:
   - Project decal texture ke mesh di impact point
   - Modify normal map: tambahkan scratch normal
   - Modify roughness map: scratch lebih rough/shiny
   
2. DENTS (Ringan):
   - Modify normal map untuk fake dent shading
   - Tidak perlu vertex displacement
   - Hanya untuk dent < 5mm depth
   
3. PAINT DAMAGE:
   - Base color: ganti dengan warna primer/metal
   - Roughness: tingkatkan di area damage
   - Normal: tambahkan wrinkle/crackle pattern

Implementasi:
   - Render texture per zona (256x256 atau 512x512)
   - Modify texture saat impact (GPU compute shader)
   - Apply ke material zona
   - Jauh lebih murah daripada vertex deformation
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 12. DATA STRUCTURE & CLASS DESIGN LENGKAP

```
Class Hierarchy:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

HybridDamageEngine/
│
├── DamageZoneManager.cs          // Manager utama semua zona
│   ├── List<DamageZone> zones
│   ├── void RegisterZone(DamageZone)
│   ├── void UpdateAllZones(float dt)
│   ├── DamageZone GetZoneAtPoint(float3 point)
│   └── float GetTotalDamage()
│
├── DamageZone.cs                 // Base class zona
│   ├── DamageZoneConfig config
│   ├── float TotalDamage
│   ├── bool IsActive
│   ├── int LODLevel
│   ├── virtual void ApplyImpact(ImpactData)
│   ├── virtual void Update(float dt)
│   └── virtual void ResetToOriginal()
│
├── RigidZone.cs : DamageZone     // Level 0
│   └── (tidak ada deformasi, hanya tracking damage)
│
├── PanelZone.cs : DamageZone     // Level 1
│   ├── PanelControlPoint[] controlPoints
│   ├── PanelMeshBinding[] meshBindings
│   ├── void ApplyDent(float3 point, float depth, float radius)
│   ├── void UpdateControlPoints(float dt)
│   └── void UpdateMesh()
│
├── NodeBeamZone.cs : DamageZone  // Level 2
│   ├── NativeArray<DeformNode> nodes
│   ├── NativeArray<DeformBeam> beams
│   ├── void SolveBeams(float dt)
│   ├── void IntegrateNodes(float dt)
│   ├── void ApplyImpactForces(ImpactData)
│   └── void UpdateMesh()
│
├── FractureZone.cs : DamageZone  // Level 3
│   ├── GameObject[] fragments
│   ├── AttachmentPoint[] attachments
│   ├── bool IsFractured
│   ├── void CheckFracture(float force)
│   ├── void Fracture(float3 impactPoint)
│   └── void Detach()
│
├── CollisionHandler.cs           // Deteksi & resolusi impact
│   ├── void OnCollisionEnter(Collision)
│   ├── ImpactData CalculateImpact(Collision)
│   └── void DistributeImpact(ImpactData)
│
├── DamagePerformanceLink.cs      // Koneksi damage → performa
│   ├── void UpdateAeroDamage()
│   ├── void UpdateSuspensionDamage()
│   ├── void UpdateEngineDamage()
│   └── void UpdateWeightDistribution()
│
├── RepairSystem.cs               // Sistem perbaikan
│   ├── void QuickFix(float dt)
│   ├── void FullRepair(float dt)
│   └── void ComponentReplacement(float dt)
│
├── DamageEffects.cs              // Visual effects
│   ├── void SpawnSparks(float3 pos, float3 normal)
│   ├── void SpawnDebris(float3 pos, float3 normal)
│   ├── void SpawnSmoke(float3 pos)
│   └── void ApplyScratchDecal(float3 pos, float3 dir)
│
├── DamageLODManager.cs           // LOD untuk damage
│   ├── void UpdateLOD(float3 cameraPos)
│   └── int GetLODLevel(float distance)
│
└── DamageTelemetry.cs            // Logging damage
    ├── void LogImpact(ImpactData)
    ├── void LogZoneDamage(string zone, float damage)
    └── DamageReport GenerateReport()
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 13. ALGORITMA UTAMA: Main Damage Loop

```
ALGORITMA: HybridDamageEngine.MainUpdate
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Dipanggil setiap FixedUpdate (bersama vehicle physics)

1. Update LOD:
   damageLOD.Update(camera.position)

2. UNTUK SETIAP zona WHERE zone.IsActive:
   
   IF zone.LODLevel <= 1:
       // Full simulation
       zone.Update(dt)
       
       IF zone is NodeBeamZone:
           zone.SolveBeams(dt)
           zone.IntegrateNodes(dt)
           zone.UpdateMesh()
       
       ELSE IF zone is PanelZone:
           zone.UpdateControlPoints(dt)
           zone.UpdateMesh()
       
       ELSE IF zone is FractureZone:
           zone.CheckFracture()
   
   ELSE IF zone.LODLevel == 2:
       // Simplified: hanya update damage texture
       zone.UpdateDamageTexture()
   
   ELSE:
       // LOD 3: skip
       continue

3. Update damage → performance link:
   DamagePerformanceLink.Update(vehicle)

4. Cleanup:
   CleanupExpiredDebris()
   CleanupExpiredParticles()

5. Telemetry:
   DamageTelemetry.Update(vehicle)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 14. PANDUAN IMPLEMENTASI DENGAN QWEN CODE

### Urutan Prompt untuk Build Hybrid Damage Engine

```
PROMPT SEQUENCE:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

STEP 1 - Data Structures:
"Buat semua struct dan enum untuk hybrid damage engine di Unity C#:
 DeformNode, DeformBeam, BeamType, DamageZoneType, DamageZoneConfig,
 PanelControlPoint, PanelMeshBinding, ImpactData, AttachmentPoint.
 Gunakan Unity.Mathematics (float3, quaternion).
 Sertakan comments untuk setiap field."

STEP 2 - DamageZone Base:
"Buat abstract class DamageZone yang inherit dari MonoBehaviour.
 Properties: config, TotalDamage, IsActive, LODLevel, IsDirty.
 Virtual methods: ApplyImpact, Update, ResetToOriginal, UpdateMesh.
 Buat juga RigidZone : DamageZone (Level 0)."

STEP 3 - PanelZone (Level 1):
"Buat class PanelZone : DamageZone dengan:
 - Control point system (4-16 points per panel)
 - Vertex displacement dengan smoothstep falloff
 - Dent application dengan Perlin noise
 - Mesh binding (vertex-to-control-point weights)
 - Plastic deformation
 Implementasikan semua method dari DamageZone."

STEP 4 - NodeBeamZone (Level 2):
"Buat class NodeBeamZone : DamageZone dengan:
 - NativeArray<DeformNode> dan NativeArray<DeformBeam>
 - Spring-damper physics per beam
 - Semi-implicit Euler integration
 - Plastic deformation dan accumulated damage
 - Beam break threshold
 - Mesh skinning (vertex-to-node weights)
 Gunakan Unity.Burst dan IJobParallelFor untuk performa."

STEP 5 - FractureZone (Level 3):
"Buat class FractureZone : DamageZone dengan:
 - Attachment point system
 - Fracture detection (force vs threshold)
 - Fragment activation (pre-fractured meshes)
 - Part detachment dengan rigid body
 - Debris spawning dengan object pooling
 - Partial detach (hanging parts)"

STEP 6 - Collision Handler:
"Buat class CollisionHandler yang handle OnCollisionEnter:
 - Hitung impact energy, effective mass
 - Tentukan damage zone yang kena
 - Distribusikan impact ke zona
 - Spawn effects (sparks, debris)
 - Apply impulse ke vehicle rigid body
 - Log impact data untuk telemetry"

STEP 7 - Damage Performance Link:
"Buat class DamagePerformanceLink yang mapping damage ke
 vehicle performance: aero, suspension, engine, weight.
 Update setiap frame berdasarkan accumulated damage."

STEP 8 - Repair System:
"Buat class RepairSystem dengan 3 level repair:
 QuickFix, FullRepair, ComponentReplacement.
 Implementasikan lerp-back-to-original untuk node positions."

STEP 9 - LOD Manager:
"Buat DamageLODManager yang mengatur detail damage 
 berdasarkan jarak kamera. 4 LOD levels."

STEP 10 - Integration & Main Loop:
"Integrasikan semua sistem ke dalam HybridDamageEngine manager.
 Buat main update loop yang mengkoordinasikan semua zona,
 LOD, performance link, dan cleanup.
 Target: < 1ms per frame untuk damage system."
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 15. RINGKASAN ARSITEKTUR

```
HYBRID DEFORMATION ENGINE - RINGKASAN:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

INPUT:
  Collision events dari Unity Physics
  Vehicle state dari physics engine
  Camera position untuk LOD

PROCESSING:
  ┌─────────────────────────────────────────┐
  │  Collision Handler                       │
  │  → Impact detection & zone routing       │
  └──────────────┬──────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────┐
  │  Zone Router                             │
  │  → Rigid / Panel / NodeBeam / Fracture   │
  └──┬──────────┬──────────┬──────────┬─────┘
     │          │          │          │
  ┌──▼──┐   ┌──▼──┐   ┌──▼──┐   ┌──▼──┐
  │Rigid│   │Panel│   │Node │   │Frac │
  │ L0  │   │ L1  │   │Beam │   │ture │
  │     │   │     │   │ L2  │   │ L3  │
  └─────┘   └─────┘   └─────┘   └─────┘
     │          │          │          │
  ┌──▼──────────▼──────────▼──────────▼─────┐
  │  Mesh Updater (Skinning + Normals)       │
  └──────────────┬──────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────┐
  │  Damage → Performance Link               │
  │  (Aero, Susp, Engine, Weight)            │
  └──────────────┬──────────────────────────┘
                 │
  ┌──────────────▼──────────────────────────┐
  │  Visual Effects                          │
  │  (Sparks, Debris, Smoke, Fire)           │
  └─────────────────────────────────────────┘

OUTPUT:
  Deformed meshes (visual)
  Modified vehicle parameters (physics)
  Damage telemetry data
  Visual effects

PERFORMANCE:
  ~280 nodes, ~560 beams per vehicle
  Budget: < 1.0 ms per frame
  GTX 7 series: ✅ Feasible
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---
 Saya sarankan mulai dari **Step 1 (Data Structures)** dan **Step 2 (DamageZone Base + RigidZone)** sebagai fondasi, lalu lanjut ke **PanelZone** dan **NodeBeamZone**.