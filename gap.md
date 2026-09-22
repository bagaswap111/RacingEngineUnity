# Physics Simulation — What We Haven't Planned Yet

After auditing every system we've designed, here are the **physics simulation gaps** that remain unaddressed.

---

## Audit Summary

```
FULLY DESIGNED (15 systems):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Pacejka Tire Model (steady-state)
✅ Suspension Kinematics & Dynamics
✅ Load Transfer & Roll Dynamics
✅ Engine & Drivetrain (basic)
✅ Braking System (force + thermal)
✅ Tire Thermal Model (3-zone)
✅ Aerodynamics (adaptive 5-tier)
✅ Slipstream / Wake / Ground Effect / Crosswind / DRS
✅ Electronics (TC, ABS, ECU)
✅ Hybrid Soft-Body Deformation (4-level)
✅ Collision Detection & Impact
✅ Damage → Performance Coupling
✅ Repair System
✅ Telemetry & Lap Timing
✅ GPU Detection & Adaptive Quality

NOT YET DESIGNED (grouped below):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## CRITICAL GAPS
*These will cause incorrect or unstable behavior if missing.*

### 1. Chassis Rigid Body Integration

We defined forces (tire, aero, suspension, engine) but **never defined how they integrate into the chassis rigid body**.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

6-DOF Rigid Body State:
  Position (x, y, z)
  Orientation (quaternion)
  Linear Velocity (vx, vy, vz)
  Angular Velocity (ωx, ωy, ωz)

Integration Method:
  We never chose:
  - Semi-implicit Euler? (fast, stable enough)
  - RK4? (accurate, expensive)
  - Verlet? (good for constraints)
  
  For racing sim: Semi-implicit Euler at 240-480 Hz
  is standard. But we never specified.

Force Aggregation:
  How do these combine?
  
  ΣF = F_tire_FL + F_tire_FR + F_tire_RL + F_tire_RR
     + F_aero + F_gravity + F_collision + F_suspension_reaction
  
  Στ = τ_tire + τ_aero + τ_collision + τ_engine_reaction
  
  Where exactly is each force applied?
  In which coordinate space?
  What sign conventions?

Quaternion Integration:
  q̇ = 0.5 × q × ω_quaternion
  q_new = normalize(q + q̇ × dt)
  
  We never specified this. Without it, rotation
  integration will drift and become unstable.

Sub-stepping Strategy:
  Physics tick: 240 Hz? 480 Hz? 960 Hz?
  Render: 60 Hz
  How many physics steps per render frame?
  What happens if frame time spikes?
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 2. Coordinate Systems & Sign Conventions

We never explicitly defined the coordinate framework.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Unity Convention:
  X = right
  Y = up
  Z = forward
  
  But vehicle dynamics traditionally uses:
  X = forward (SAE) or X = longitudinal (ISO)
  Y = left/right
  Z = up/down

We need to define:
  - Vehicle local space: which axis is forward?
  - Tire coordinate system: SAE or ISO?
  - Sign convention for:
    - Slip angle (positive = ?)
    - Camber (positive = top out or top in?)
    - Toe (positive = toe-in or toe-out?)
    - Steering angle (positive = left or right?)
    - Yaw rate (positive = clockwise or CCW?)
    - Lateral force (positive = left or right?)

Without this, every module will have different
sign conventions and forces will cancel or
double incorrectly.

SAE Tire Coordinate System:
  X_tire = forward (rolling direction)
  Y_tire = left (driver's left)
  Z_tire = up
  
  Fx = longitudinal force (positive = forward)
  Fy = lateral force (positive = left)
  Fz = vertical force (positive = up)
  Mz = aligning torque (positive = CCW around Z)

ISO Tire Coordinate System:
  X_tire = forward
  Y_tire = right (opposite of SAE)
  Z_tire = up
  
  → Fy sign is INVERTED vs SAE
  
  We must pick ONE and stick with it.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 3. Tire Relaxation Length (Transient Tire Model)

Our Pacejka model is **steady-state only**. Real tires have transient response.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Steady-State (what we have):
  Slip angle changes → Force changes INSTANTLY
  
  This is WRONG. Real tires have a delay.

Transient Model (what we need):
  Tire behaves as a first-order system:
  
  dFy/dt = (Fy_steady - Fy_actual) / τ
  
  τ = relaxation length / velocity
  relaxation_length ≈ 0.2 - 0.4 m (for racing tires)
  
  At 100 km/h: τ ≈ 0.3/27.8 ≈ 0.011 s (11 ms)
  At 300 km/h: τ ≈ 0.3/83.3 ≈ 0.004 s (4 ms)

Why this matters:
  - Without relaxation: car feels "twitchy", 
    instant grip changes
  - With relaxation: car feels "planted",
    smooth grip build-up
  - Affects stability at high speed
  - Affects oscillation / shimmy behavior

Implementation:
  // Per wheel, per frame:
  Fy_steady = Pacejka(slipAngle, Fz, camber)
  
  sigma = relaxationLength / max(|Vx|, 0.1)
  alpha = dt / (sigma + dt)  // smoothing factor
  
  Fy_actual = lerp(Fy_actual, Fy_steady, alpha)
  
  // Same for Fx and Mz
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 4. Numerical Stability & Error Recovery

We never defined what happens when the simulation goes unstable.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Potential Instability Sources:
  - Suspension travel exceeding limits → force spike
  - Tire load = 0 → Pacejka division by zero
  - Very high slip angle → Pacejka extrapolation
  - Collision impulse too large → velocity explosion
  - Quaternion drift → orientation corruption
  - NaN propagation from any calculation

Recovery Strategy:
  1. NaN Detection:
     Every physics tick, check for NaN/Inf
     IF detected → reset vehicle to last valid state
     
  2. Force Clamping:
     F_tire_max = μ_max × Fz_max × safety_factor
     Clamp all forces to physical limits
     
  3. Velocity Clamping:
     V_max = 150 m/s (540 km/h)
     ω_max = 20 rad/s
     Clamp to prevent explosion
     
  4. Quaternion Renormalization:
     Every tick: q = normalize(q)
     Every 100 ticks: full orthogonalization
     
  5. Penetration Recovery:
     IF wheel below ground → push up
     IF chassis penetrating wall → push out
     Use position correction + velocity damping
     
  6. Fallback State:
     Store last N valid states (ring buffer)
     If corruption detected → rollback to last valid
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 5. Unsprung Mass Dynamics (Wheel Hop)

We treated wheels as attached to suspension but never modeled the wheel as a separate mass.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Current model (simplified):
  Chassis → Spring/Damper → Ground
  (wheel mass lumped into chassis)

Correct model:
  Chassis → Spring/Damper → Wheel Mass → Tire Spring → Ground
  
  Wheel mass (unsprung mass) is a SEPARATE body:
  - Wheel + tire + brake disc + hub ≈ 15-25 kg per corner
  - Has its own vertical DOF
  - Oscillates independently from chassis

Why this matters:
  - Wheel hop over kerbs
  - Tire contact consistency
  - Suspension response over bumps
  - Brake disc cooling (wheel rotation)

Equations:
  m_unsprung × ÿ_wheel = F_spring + F_damper - F_tire - m_unsprung × g
  
  F_tire = k_tire × (y_ground - y_wheel) + c_tire × (ẏ_ground - ẏ_wheel)
  
  k_tire ≈ 200-400 N/mm (tire vertical stiffness)
  c_tire ≈ 5-15 N·s/mm (tire damping)

This adds 4 more DOF (one per wheel vertical).
Total: 7 DOF (6 chassis + 4 wheel vertical - 3 constraints)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## IMPORTANT GAPS
*Needed for realism and correct feel.*

### 6. Steering System Physics

We mentioned steering geometry but never modeled the steering mechanism itself.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Steering Rack Model:
  - Rack travel (mm) vs steering wheel angle (deg)
  - Steering ratio (variable or fixed)
  - Rack compliance (flex under load)
  - Power steering assist curve
  
  steer_wheel_angle → rack_position → tie_rod_displacement
  → wheel_steer_angle

Steering Torque Feedback (for FFB):
  τ_steering = Σ(Fy × pneumatic_trail × cos(caster))
             + Σ(Fz × scrub_radius × sin(KPI))
             + friction_torque + inertia_torque

Variable Steering Ratio:
  ratio(steer_angle) = ratio_center 
                       + ratio_gain × |steer_angle|
  
  // Quick ratio at center (responsive)
  // Slower ratio at full lock (stable)

Steering Compliance:
  Under high lateral load, steering deflects:
  Δsteer = Fy × steeringCompliance
  
  steeringCompliance ≈ 0.001-0.005 deg/N
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 7. Vehicle-to-Vehicle Collision Response

We have deformation but not the **physics response** when two cars collide.

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Impulse-Based Collision Resolution:
  When two vehicles collide:
  
  1. Detect contact point & normal
  2. Calculate relative velocity at contact
  3. Calculate impulse:
  
     j = -(1 + e) × v_rel_n / (1/m_A + 1/m_B 
         + (r_A × n)²/I_A + (r_B × n)²/I_B)
     
     e = coefficient of restitution (0.1-0.3 for car-car)
     v_rel_n = relative velocity along normal
     r_A, r_B = contact point offset from CoG
     I_A, I_B = moment of inertia
     
  4. Apply impulse:
     Δv_A = j × n / m_A
     Δv_B = -j × n / m_B
     Δω_A = (r_A × (j × n)) / I_A
     Δω_B = (r_B × (-j × n)) / I_B

Friction at Contact:
  Tangential impulse:
  j_t = -v_rel_t × friction_coeff / (1/m_A + 1/m_B)
  j_t = clamp(j_t, -μ×j, +μ×j)  // Coulomb friction

Wheel-to-Wheel Contact:
  Special case: wheels interlocking
  → Prevent tunneling
  → Apply separate collision normal
  
Anti-Tunneling:
  At high speed (> 200 km/h), cars can pass through
  each other between frames.
  → CCD (Continuous Collision Detection) needed
  → Or: sub-step collision at 480+ Hz
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 8. Vehicle-to-Environment Collision Response

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Wall / Barrier Collision:
  - Concrete wall: rigid, high restitution
  - Tire barrier: deformable, low restitution
  - Guardrail: flexible, redirects vehicle
  - Tecpro barrier: deformable, absorbs energy
  
  Each needs different collision response:
  
  Concrete: e = 0.3, μ = 0.4
  Tire wall: e = 0.05, μ = 0.8, deformation
  Guardrail: e = 0.1, μ = 0.3, redirect along rail
  Gravel: deceleration zone, μ = 0.6, drag force

Kerb Interaction:
  - Kerb as raised surface (height profile)
  - Wheel hits kerb → vertical impulse
  - Kerb pattern → periodic excitation
  - Kerb can "launch" car if hit at angle
  
  kerb_height = f(distance_along_kerb)
  F_kerb_vertical = k_kerb × kerb_height × wheel_load
  
Gravel Trap:
  - High rolling resistance
  - Deceleration force: F = μ_gravel × Fz
  - μ_gravel ≈ 0.4-0.7
  - Also: gravel spray particles

Grass:
  - Low grip: μ_grass ≈ 0.25-0.35
  - Higher rolling resistance
  - Uneven surface → vibration
  - Wet grass: μ ≈ 0.15-0.20
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 9. Advanced Engine Physics

We have a basic torque lookup table. Missing:

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Turbo / Supercharger Dynamics:
  Turbo lag model:
  
  τ_turbine = exhaust_gas_force × turbine_efficiency
  τ_compressor = τ_turbine × shaft_efficiency
  
  boost_pressure += (τ_compressor / I_turbo_shaft 
                     - boost_pressure × wastegate_flow) × dt
  
  τ_shaft = I_turbo × dω_turbo/dt
  
  // Turbo lag: 0.5-2 seconds to full boost
  // Boost threshold: ~3000-4000 RPM
  
  Engine torque with turbo:
  τ_engine = τ_naturally_aspirated × (1 + boost_pressure × turbo_gain)

Engine Braking:
  When throttle = 0 and clutch engaged:
  
  τ_engine_brake = τ_compression + τ_friction + τ_pumping
  
  τ_compression = V_displacement × compression_ratio × f(RPM)
  τ_pumping = f(throttle_position, RPM)
  
  // Engine braking is significant in racing
  // Affects rear stability under braking

Flywheel & Rotating Inertia:
  I_total = I_flywheel + I_crankshaft + I_pistons 
          + I_clutch + I_gearbox_input
  
  I_flywheel ≈ 0.1-0.3 kg·m²
  // Affects how quickly RPM changes
  // Light flywheel = quick rev, but harder to launch

Rev Limiter:
  IF RPM > RPM_redline:
      fuel_cut = true
      τ_engine = 0
      // RPM drops → fuel_cut = false → oscillation
      // This creates the "limiter bounce" effect

Anti-Lag System (Rally/Turbo):
  IF throttle_lift AND turbo_equipped:
      // Fuel ignites in exhaust manifold
      // Keeps turbo spinning
      τ_turbo_maintain = anti_lag_force
      boost_pressure *= 0.95  // minimal decay

Engine Stall:
  IF RPM < RPM_stall (typically 500-800):
      engine_running = false
      // Requires clutch + throttle to restart
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 10. Advanced Transmission Physics

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Dog Ring Engagement:
  Racing gearboxes use dog rings, not synchromesh.
  
  Engagement condition:
    |ω_gear - ω_shaft| < engagement_threshold
    
  IF speed difference too high:
      // Gear clash / missed shift
      shift_failed = true
      // Grinding sound, potential damage
      
  Engagement time: 20-80 ms

Shift Sequence:
  1. Clutch disengage (or throttle lift for dog box)
  2. Current gear disengage
  3. Neutral phase (50-100 ms)
  4. Target gear engage
  5. Clutch re-engage
  
  During shift: τ_engine = 0 (torque interruption)
  
  Seamless shift (F1/DCT):
  → Two shafts, pre-select next gear
  → Torque interruption < 20 ms

Driveline Torsional Vibration:
  Engine → Flywheel → Clutch → Gearbox → Driveshaft → Differential → Wheels
  
  Each connection has torsional stiffness:
  
  τ_shaft = k_shaft × (θ_in - θ_out) + c_shaft × (ω_in - ω_out)
  
  This causes:
  - Driveline oscillation on throttle lift
  - "Clunk" on gear engagement
  - Wheel hop under hard acceleration

Transmission Stress:
  τ_shaft_stress = τ_transmitted / τ_shaft_max
  
  IF stress > 1.0:
      shaft_damage += (stress - 1.0) × damage_rate
      // Gearbox can break under extreme torque
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 11. Brake System Advanced

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Pad Friction Curve vs Temperature:
  μ_pad(T) is NOT constant:
  
  Temp (°C)  │ μ_pad
  ───────────┼──────
  0-100      │ 0.30  (cold, low grip)
  100-300    │ 0.42  (warming up)
  300-600    │ 0.48  (optimal)
  600-800    │ 0.45  (starting to fade)
  800+       │ 0.30  (severe fade)
  
  → Brakes need to be "warmed up"
  → Overheating → brake fade → longer braking distance

Brake Fade Model:
  fade_factor = 1.0 - max(0, (T_brake - T_fade_start)) 
                / (T_fade_end - T_fade_start)
  fade_factor = clamp(fade_factor, 0.2, 1.0)
  
  F_brake_actual = F_brake_ideal × fade_factor

Brake Disc Warping:
  IF T_brake > T_warp_threshold AND cooling_uneven:
      warp_amount += warp_rate × dt
      
  // Warped disc → vibration → brake pulse
  F_brake_pulsation = warp_amount × sin(wheel_rotation × disc_order)

Brake Bias Migration:
  Under braking, load transfers forward.
  Ideal brake bias should shift forward.
  
  bias_dynamic = bias_static + load_transfer × bias_migration_factor
  
  // Without migration: rear brakes lock too early
  // With migration: balanced braking

Brake Fluid Boiling:
  IF T_brake > T_fluid_boil (~230°C for DOT4, ~310°C for racing):
      fluid_vapor_lock = true
      // Brake pedal goes "soft" → reduced braking force
      F_brake *= 0.3  // Severe loss
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 12. Chassis Torsional Flex

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

We treat chassis as perfectly rigid.
Real chassis FLEXES under load.

Torsional Flex Model:
  Chassis twists when left and right wheels
  experience different loads (cornering, kerbs).
  
  φ_chassis = T_roll / K_torsional
  
  T_roll = roll torque from suspension
  K_torsional = chassis torsional stiffness (N·m/rad)
  
  Typical values:
    Road car:  10.000-20.000 N·m/rad
    GT3 car:   25.000-40.000 N·m/rad
    F1 car:    50.000+ N·m/rad

Effect:
  - Chassis flex CHANGES suspension geometry
  - Affects camber/toe under load
  - Reduces effective spring rate
  - Adds compliance → changes feel
  
Implementation (simplified):
  flex_offset = φ_chassis × suspension_arm_length
  
  // Adjust suspension geometry per frame:
  camber_actual += flex_offset × camberFlexFactor
  toe_actual += flex_offset × toeFlexFactor
  
  // This is a 2nd-order effect but noticeable
  // in sim racing (especially over kerbs)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## ADVANCED GAPS
*Polish and high-end simulation fidelity.*

### 13. Tire Pressure Dynamics

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Tire pressure changes with temperature:
  P × V = n × R × T  (Ideal Gas Law)
  
  P_actual = P_cold × (T_actual / T_cold)
  
  T_cold = 25°C (298 K)
  T_actual = tire operating temp (80-120°C)
  
  Pressure increase: ~15-25% from cold to hot
  
  Example:
    Cold: 200 kPa at 25°C
    Hot: 200 × (373/298) = 250 kPa at 100°C

Pressure effects:
  - Higher pressure → smaller contact patch → less grip
  - Lower pressure → larger contact patch → more grip
  - Too low → tire overheating, shoulder wear
  - Too high → center wear, reduced grip
  
  contact_patch_area ∝ Fz / P_tire
  
  Grip correction:
  μ_effective = μ_base × pressureGripFactor(P_actual)
  
Puncture:
  IF tire_punctured:
      P_tire → 0 over 1-3 seconds
      contact_patch → minimal
      grip → 10-20% of normal
      tire damage → rapid
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 14. Fuel System Physics

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Fuel Mass & Distribution:
  fuel_mass = fuel_volume × fuel_density (0.75 kg/L)
  
  Full tank (100L) = 75 kg
  Empty tank = 0 kg
  
  → Vehicle mass changes during race
  → CoG shifts slightly as fuel is consumed
  
  vehicle_mass = base_mass + fuel_mass + driver_mass
  CoG_adjustment = fuel_position × fuel_mass / vehicle_mass

Fuel Slosh:
  Fuel moves inside tank under acceleration.
  
  Simplified model:
  fuel_CoG_offset = -acceleration × slosh_factor
  
  slosh_factor ≈ 0.01-0.05 m per g
  
  // Affects CoG position → affects handling
  // More noticeable with low fuel (more slosh room)

Fuel Starvation:
  Under high lateral/longitudinal G:
  IF |lateral_G| > threshold OR |long_G| > threshold:
      IF fuel_level < starvation_threshold:
          fuel_starved = true
          engine_torque = 0  // Engine cuts
          // Until fuel returns to pickup
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 15. Track Surface Temperature & Grip Evolution

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Track Temperature Model:
  T_track changes with:
  - Ambient temperature
  - Sun exposure (shaded vs sunny sections)
  - Rubber buildup (darkens surface, absorbs heat)
  - Rain (cools surface)
  
  T_track += (T_ambient - T_track) × heat_transfer_rate × dt
  T_track += sun_intensity × absorption_rate × dt
  T_track -= rain_intensity × cooling_rate × dt

Grip Evolution (Rubbering In):
  As cars drive, rubber deposits on racing line.
  
  rubber_level += tire_wear × rubber_deposit_rate × dt
  
  grip_multiplier = 1.0 + rubber_level × grip_gain
  // grip_gain ≈ 0.05-0.15 (5-15% grip increase)
  
  // Racing line gets grippier over session
  // Off-line stays at base grip
  
  Track zones:
  - Racing line: high rubber, high grip
  - Off-line: low rubber, low grip
  - Wet patches: variable

Surface Contamination:
  - Oil/fluid from damaged cars → grip reduction
  - Gravel/debris on track → grip reduction
  - Tire marbles (off-line) → grip reduction
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 16. Driveline Oscillation & Wheel Hop

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Wheel Hop:
  Under hard acceleration, driveline torque
  excites suspension → wheel oscillates vertically.
  
  Cause: driveshaft angle + suspension geometry
  creates a feedback loop.
  
  Model:
  τ_driveshaft = k_ds × (θ_engine_out - θ_wheel)
                + c_ds × (ω_engine_out - ω_wheel)
  
  F_vertical_reaction = τ_driveshaft × tan(driveshaft_angle) 
                        / wheel_radius
  
  // This vertical force excites suspension
  // Can cause wheel hop if frequencies align

Driveline Oscillation:
  Engine → Clutch → Gearbox → Driveshaft → Diff → Wheels
  
  Each element has inertia and compliance:
  
  I_engine × ω̇_engine = τ_engine - τ_clutch
  I_clutch × ω̇_clutch = τ_clutch - τ_gearbox_in
  I_shaft × ω̇_shaft = τ_gearbox_out - τ_diff_in
  I_wheel × ω̇_wheel = τ_diff_out - τ_tire
  
  // This is a multi-mass torsional system
  // Simplification: 2-mass model (engine + wheel)
  
  2-mass model:
  I_1 × ω̇_1 = τ_engine - k × (θ_1 - θ_2) - c × (ω_1 - ω_2)
  I_2 × ω̇_2 = k × (θ_1 - θ_2) + c × (ω_1 - ω_2) - τ_load
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 17. Aero Transient Response

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

We model aero as instant (steady-state).
Real aero has response time.

Aero Response Model:
  When ride height changes suddenly (over kerb):
  → Downforce doesn't change instantly
  → Flow needs time to attach/detach
  
  F_down_actual += (F_down_steady - F_down_actual) 
                   × (dt / τ_aero)
  
  τ_aero ≈ 0.05-0.2 seconds
  
  // Fast: wing changes (small surfaces)
  // Slow: ground effect (large floor area)
  
  Different τ for:
    Front wing: τ ≈ 0.05 s
    Rear wing: τ ≈ 0.08 s
    Ground effect: τ ≈ 0.15 s
    Diffuser: τ ≈ 0.20 s

Pitch Sensitivity Transient:
  When car pitches (braking/acceleration):
  → Ride height changes
  → Ground effect changes with DELAY
  → Can cause "aero oscillation" at high speed
  
  This is why F1 cars are sensitive to
  pitch changes at high speed.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### 18. Environmental Physics

```
What's missing:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Air Density Variation:
  ρ = P_atm / (R_specific × T_absolute)
  
  P_atm = 101325 Pa (sea level)
  T_absolute = 273.15 + T_celsius
  
  Altitude effect:
  P_atm(h) = 101325 × (1 - 2.25577e-5 × h)^5.25588
  
  h = altitude (m)
  
  Example:
    Sea level, 15°C: ρ = 1.225 kg/m³
    1000m, 15°C:     ρ = 1.112 kg/m³ (-9.2%)
    2000m, 15°C:     ρ = 1.007 kg/m³ (-17.8%)
    
  → Less air density = less downforce, less drag, less engine power
  
Humidity effect:
  Humid air is LESS dense than dry air
  ρ_humid = ρ_dry × (1 - 0.378 × e / P_atm)
  e = water vapor pressure

Wind Field (Spatial Variation):
  Wind is NOT uniform across the track.
  
  wind(x, y, z) = V_mean + V_gust(t) + V_local(x,y,z)
  
  V_local: buildings, hills, trees create
  local wind acceleration/sheltering
  
  Simplified: define wind zones per track section
  Section 1: headwind 5 m/s
  Section 2: crosswind 8 m/s (exposed)
  Section 3: sheltered, 2 m/s
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## COMPLETE GAP SUMMARY

```
CRITICAL (Must design before implementation):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 # │ System                          │ Status    │ Effort
───┼─────────────────────────────────┼───────────┼────────
 1 │ Chassis Rigid Body Integration  │ ❌ Missing │ 3-4 days
 2 │ Coordinate System & Conventions │ ❌ Missing │ 1 day
 3 │ Tire Relaxation (Transient)     │ ❌ Missing │ 1-2 days
 4 │ Numerical Stability & Recovery  │ ❌ Missing │ 2-3 days
 5 │ Unsprung Mass / Wheel Hop       │ ❌ Missing │ 2-3 days

IMPORTANT (Needed for correct feel):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 6 │ Steering System Physics         │ ❌ Missing │ 2-3 days
 7 │ Vehicle-to-Vehicle Collision    │ ❌ Missing │ 3-4 days
 8 │ Vehicle-to-Environment Collision│ ❌ Missing │ 2-3 days
 9 │ Advanced Engine (Turbo, Stall)  │ ⚠️ Partial │ 3-4 days
10 │ Advanced Transmission           │ ⚠️ Partial │ 2-3 days
11 │ Advanced Brakes (Fade, Warp)    │ ⚠️ Partial │ 2 days
12 │ Chassis Torsional Flex          │ ❌ Missing │ 1-2 days

ADVANCED (Polish / high-fidelity):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
13 │ Tire Pressure Dynamics          │ ❌ Missing │ 1 day
14 │ Fuel System Physics             │ ❌ Missing │ 1 day
15 │ Track Temp & Grip Evolution     │ ❌ Missing │ 2 days
16 │ Driveline Oscillation           │ ❌ Missing │ 2-3 days
17 │ Aero Transient Response         │ ❌ Missing │ 1 day
18 │ Environmental Physics           │ ❌ Missing │ 1-2 days

TOTAL REMAINING: ~30-40 days of design work
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## Recommended Design Order

```
PHASE A — Foundation (do FIRST, everything depends on it):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  1. Coordinate System & Conventions  ← Do this BEFORE anything else
  2. Chassis Rigid Body Integration
  3. Numerical Stability & Recovery
  4. Tire Relaxation Length

PHASE B — Vehicle Feel:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  5. Unsprung Mass / Wheel Hop
  6. Steering System Physics
  7. Advanced Engine (turbo, stall, braking)
  8. Advanced Transmission (shift dynamics)

PHASE C — Interaction:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  9. Vehicle-to-Vehicle Collision
  10. Vehicle-to-Environment Collision
  11. Advanced Brakes (fade, warp)

PHASE D — Fidelity:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  12. Chassis Torsional Flex
  13. Tire Pressure Dynamics
  14. Fuel Physics
  15. Track Temperature & Grip Evolution
  16. Driveline Oscillation
  17. Aero Transient
  18. Environmental Physics
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**#1 and #2 are blockers.** Without coordinate conventions and rigid body integration defined, every other system we've designed will have inconsistent signs, wrong application points, and integration errors. I recommend we design those two first.