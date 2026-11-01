# Fire hose water

`PlayerWaterSpray` now launches moving water parcels instead of applying an instant straight-ray hit. The existing Attack action (hold LMB) and absolute mouse aim events are unchanged.

`PlayerAnimator3D` reads the hose's `IsSpraying` state and sets the Animator's `IsSpraying` Boolean. `Player_Controller` enters its looping **Fire** state from Idle or Walk while spraying, then blends back to Walk when moving or Idle when stationary on release. Transitions use a 0.08-second blend without exit time. Pausing, losing focus, or disabling the nozzle also clears the firing animation. This uses the existing full-body Fire clip; root motion stays disabled and movement remains controlled by the movement system.

`IsSpraying` now reflects held input, component readiness, and gameplay state immediately, so animation and movement handling share the same active-spray condition.

Play-mode animation checks confirmed that Fire loops at full weight while spraying, stationary release returns to Idle, moving release returns to Walk, and pause clears the spraying Boolean before resuming in Idle. The Console reported no errors.

The controller aims a low ballistic arc toward the surface under the cursor. Gravity bends each parcel's trajectory in FixedUpdate, and a swept sphere test checks every movement segment. The nearest solid collider receives that parcel's water amount only when the parcel arrives. The player's own colliders and triggers are ignored. Old water keeps its world-space motion when the player turns or releases the trigger.

`WaterJetMesh` draws a narrow eight-sided tube through the live parcel positions, using the URP water material from the imported pressure-water prefab. The prefab's original straight stream is hidden. Its splash and droplet particles are emitted at the collision point, with reflected/scattered velocities and gravity. The mesh, parcel storage, and particle effects are reused.

## Settings

On **WaterNozzle > OutPoint > Player Water Spray > Hose Physics**:

| Setting | Scene default | Effect |
| --- | --- | --- |
| Range | 3 world units | Maximum distance from a parcel's emission point |
| Jet Speed | 8 units/second | Pressure feel; higher speed flattens the arc |
| Gravity Scale | 1 | Multiplier on Physics.gravity |
| Jet Radius | 0.015 units | Physical sweep radius and visible tube radius |
| Water Parcels Per Second | 50 | Simulation/emission resolution |
| Water Per Second | 1 | Extinguishing rate, independent of parcel emission rate |

Low speeds may be unable to reach a high/distant cursor point. Moving closer or increasing Jet Speed gives more reach. Water in flight continues after release; pausing freezes its simulation and splash particles. Disabling the controller clears it.

This models a pressurized water jet and impacts. It is not a volume-fluid solver: puddles, surface flow, and interaction between water parcels are outside this implementation. Storage is capped at 128 live parcels; the default range/speed/rate remain well within that budget.

## Movement slowdown and hose recoil

Manual editor setup:

1. Let Unity compile the scripts, then select the scene's **Player** root (the object with the player's Rigidbody).
2. Add **Player Hose Movement**. Its **Body**, **Movement**, and **Water Spray** references resolve from the player hierarchy in Awake; they can be left empty. To assign them explicitly, use the player's Rigidbody, its existing EntityMovement component, and the PlayerWaterSpray component on OutPoint.
3. Start with the defaults below, save the scene, and test while holding LMB both stationary and while walking. Releasing LMB restores normal walking speed, with a short recoil recovery.

| Setting | Default | Effect |
| --- | --- | --- |
| Movement Multiplier | 0.5 | Half of normal movement speed while spraying |
| Recoil Acceleration | 2 | Horizontal acceleration opposite the actual launch direction, in units/second² |
| Maximum Recoil Speed | 0.4 | Cap on extra backward velocity, in units/second |
| Recoil Recovery | 4 | How quickly recoil velocity decays after release, in units/second² |

Reduce Movement Multiplier to slow the player further. Increase Maximum Recoil Speed for a stronger sustained push; increase Recoil Acceleration for a faster buildup. At the defaults, maximum recoil builds in about 0.2 seconds for a horizontal jet and settles in about 0.1 seconds after release. Nearly vertical shots produce little horizontal reaction and never add vertical lift.

The existing EntityMovement writes movement velocity each physics step. PlayerHoseMovement runs afterward (execution order 250) and combines that velocity with its bounded recoil, preserving gravity and the movement system's current speed stat. The Rigidbody still handles collisions. The shared VectorViolet package needs no changes. Pausing or disabling the component resets its accumulated recoil.

PlayerFacing3D faces the launch direction while spraying, so backward recoil cannot turn the Fire pose away from the stream. After release, facing ignores the remaining recoil drift and follows walking movement. Water already in flight continues independently.

The gameplay assembly compiled successfully against the project's Unity packages. Ten headless movement checks passed, covering half-speed movement, recoil direction and cap, vertical-velocity preservation, release recovery, reset, vertical aim, and physics-timestep consistency. Adding the component and checking the feel in Play mode remain manual.

## Fire integration and checks

Every impact raises the existing `EventBus.Game.OnWaterHit`. A parcel contains `Water Per Second / Water Parcels Per Second` water units, so increasing simulation resolution does not make fires extinguish faster. The building/tree thresholds remain 3 and 1.5 water units; travel adds a short delay before the first hit.

Play-mode checks confirmed a curved jet hitting terrain with splash particles, physical hose impacts extinguishing a building (3.020 water units), and a 0.05-unit wall blocking a 30-unit/second jet with zero behind-wall hits. C# compilation and Console error checks passed.
