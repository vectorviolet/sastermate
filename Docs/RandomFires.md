# Random building and tree fires

`DemoScene` uses **BaseHouse instances and burnable tree prefabs**. The scene's **Random Fires** object owns `RandomFireController`; each eligible model has a `BurnableObject` component and mesh colliders for mouse picking and water hits.

## Environment prefabs

Use the configured prefabs under `Assets/_Project/Environment/Prefabs/Burnables` when placing more buildings or trees:

For random houses, drag **`BaseHouse.prefab`** into the scene. Its `HouseModelPicker` chooses from all 21 building FBXs in **`Assets/_Project/Environment/HouseModels.asset`**, using one shared set of gameplay components. The selected mesh and materials are assigned to the root, its mesh collider is updated, and its fire anchor and flame scale are fitted to the model. There are no extra model GameObjects or duplicate burnable components.

The scene has a saved random preview. **Randomize On Awake** is enabled, so each house chooses again when Play starts. To reroll a house in Edit mode, open its **House Model Picker** component's context menu and choose **Randomize House Model**. Turn off Randomize On Awake to retain that saved choice during gameplay. A burning house cannot change models until its fire is cleared.

The 24 previous building instances now use BaseHouse, retaining their placement, hierarchy, and original component references. The individual building prefabs below remain available for manually choosing a specific model:

| Folder | Models |
| --- | --- |
| `Buildings` | All 21 suburban buildings, `building-type-a` through `building-type-u` |
| `Trees/City` | `tree-large` and `tree-small` |
| `Trees/Nature` | The 11 tree variants used in the current scene: cone, cone dark, default, default dark, detailed, detailed fall, fat, fat fall, fat darkh, oak, and pineGroundA |

These 34 assets are prefab variants linked to their original FBXs. Each includes one `BurnableObject`, a non-trigger mesh collider on each mesh, and an assigned `FireAnchor`. Buildings use 3 water units and trees use 1.5, with a 20-second re-ignition cooldown. Flame scale is sized from the model bounds; anchors sit near the roof or within the tree canopy. Adjust these settings on the prefab to update its scene instances together.

Keep using these prefabs instead of dragging raw FBXs into the scene. Fire effects, warning markers, climate tracking, and CP rewards are managed globally; individual buildings and trees only need the prefab's components.

The controller's **Targets** list is empty so it discovers active `BurnableObject` components when Play starts. Newly placed prefab instances are included on the next Play session. A list containing only deleted references also falls back to discovery; a list with valid entries limits ignition to those entries.

## Playing

Press Play. One eligible object ignites after 2 seconds. Further ignition attempts occur every 8–14 seconds, with at most 3 simultaneous fires. Only Gameplay advances the ignition timer, cooldowns, or water application. Pausing also pauses fire particles and audio.

Aim at a burning building or tree's solid surface and hold LMB. With the player's current Water Per Second of 1:

| Target | Continuous water needed | Re-ignition cooldown |
| --- | --- | --- |
| Building | 3 seconds | 20 seconds |
| Tree | 1.5 seconds | 20 seconds |

Fire intensity and visual scale decrease as water is applied. A finished fire clears immediately and returns its particles to the shared VectorViolet component pool. Three effects are prewarmed for the current fire cap; normal spawn/extinguish cycles reuse them. Fire hits are routed through the existing `EventBus.Game.OnWaterHit`, with `OnFireIgnited` and `OnFireExtinguished` available for UI, rewards, and ambience.

Effects stay under the controller's **Fire VFX Pool** container and follow their targets in world space. They do not become children of buildings or trees. This permits a burning object or its parent hierarchy to be disabled while safely returning its effect to the pool.

## Nearby fire ambience

**Random Fires > Fire Ambience Controller** uses the three project SoundData assets: `FireSmall`, `FireMedium`, and `FireLarge`. Their clips are the matching `SFX_FireSmall_L`, `SFX_FireMedium_L`, and `SFX_FireBig_L` loops.

| Nearby burning objects | Sound |
| --- | --- |
| 0 | Silent |
| 1 | Small |
| 2 | Medium |
| 3 or more | Large |

Only nearby fires determine the level. Distance is measured from the assigned **Player** transform in XZ, so the elevated orthographic camera's AudioListener does not reduce proximity volume. Fires enter the mix within **6 world units**; a 0.35-unit exit margin prevents rapid switching at the boundary. Volume is full within **1.5 units** of the nearest fire and smoothly falls to zero at 6 units. Adding or extinguishing fires, and walking between groups, selects the corresponding smaller or larger loop with a **0.5-second crossfade**.

The controller owns three reusable 2D AudioSources for this player-relative mix. Each uses its SoundData's clip, volume, and pitch settings, with **Master Volume = 0.65**, routed through the existing **SFX** mixer group. Adjust proximity, density thresholds, and crossfade settings on the controller; individual buildings and trees need no audio components. SoundData's 3D distance settings do not control this mix, since attenuation uses the player's position directly.

The pooled fire prefab's original AudioSource is disabled to avoid playing another loop on every object. Keep it disabled when using the ambience controller. Audio pauses outside Gameplay or at zero timescale, resumes with playback position preserved, and stops when the ambience controller is disabled or its scene unloads.

**Random Fires > Fire Extinguish Audio** plays `ExtinguishingFire` once after water fully puts out a fire. It uses the `399548__soundslikewillem__extinguishing-fire` clip with **Loop disabled**, through VectorViolet's shared SFX pool and mixer. Its volume, pitch, and spatial settings come from that SoundData asset. The clip is loaded when the scene starts so the first extinguishing can respond promptly.

This cue listens to `OnFireSuccessfullyExtinguished`, separately from the cleanup event used by the VFX and ambience. Partial watering, repeated water on an already extinguished object, disabling/destroying an object, and `ExtinguishAll` do not trigger it. It also plays when no CP task is assigned. It is emitted before task rewards, so reaching a reward goal does not prevent the success cue from starting.

## Tree fire visibility

Tree prefabs enable **Camera Facing Fire**. Their logical fire anchors sit within the canopy, but the visible particle origin is moved along the viewing ray to the camera-facing side of the tree's rendered bounds, with 0.05 world units of padding. This keeps the flames at the same screen position while bringing them out from behind the leaves. Placement updates after Cinemachine in LateUpdate as the camera moves. Normal depth testing, the tree collider, water-hit target, HUD tracking, and CP reward position remain unchanged. Buildings retain their roof anchors without this offset.

Off-screen fires now display warning icons and directional arrows along the HUD's screen edges. The HUD listens to the ignition/extinguishing events and reuses its markers. See [FireWarnings.md](FireWarnings.md) for the prefab and settings.

Successful water-based extinguishing also publishes a task completion when **Random Fire Controller > Extinguish Task** is assigned. A **Task Reward Manager** awards that task asset's carbon points once per fire incident. Object/level cleanup still clears fires without rewards. See [CarbonPoints.md](CarbonPoints.md) for the manual editor setup and CP label.

The player's current maximum spray range is **3 world units**. Aim at the building's wall/roof or the tree's trunk/foliage. Moving water parcels follow gravity and apply water at their first solid-object impact; the flame particles themselves are visual effects. See `Docs/HoseWater.md` for pressure, radius, and gravity settings.

## Configuration

- **Random Fires > Random Fire Controller**: Initial Delay, Ignition Interval, Max Simultaneous Fires, and Targets.
- **Random Fires > Fire Ambience Controller**: Player/Proximity Target, the three SoundData assets, SFX mixer group, hearing/full-volume distances, density thresholds, Master Volume, and Crossfade Duration.
- **Random Fires > Fire Extinguish Audio**: assign the non-looping `Assets/_Project/Audio/Data/ExtinguishingFire.asset`; adjust the success cue's volume and pitch on that asset.
- **Burnable Object** on a building/tree: Water To Extinguish, Reignite Cooldown, Fire Scale, Fire Height, and optional Fire Anchor. Fire Height 1 places the default origin at the top of the rendered bounds. A Fire Anchor overrides that position.
- **Burnable Object > Fire Visibility**: Camera Facing Fire is enabled on all 13 tree prefab variants. Camera Facing Fire Padding controls the gap between the visible flames and the front of the rendered bounds; the default is 0.05.
- **Assets/_Project/Prefabs/VFX/PooledFire.prefab**: a project-owned variant of the imported `VFX_Fire_01_Medium` prefab. `PooledFireEffect` handles scale, particle reset, pause, and material property overrides. Its flame emission/opacity keep the orange color readable; its heat-haze child and embedded AudioSource are disabled.

To add another target, drag one of the configured environment prefabs into the scene. For a custom mesh, create a prefab with a `BurnableObject` and a solid collider on a layer included by the player's Aim Mask and Hit Mask, then check Fire Height/Fire Scale or assign a Fire Anchor. Leave Targets empty for automatic discovery, or add the target to an explicitly maintained list.

The imported fire Erosion and Flames shaders have orthographic-aware depth conversion, matching the water effect fix. The pooled variant uses a short 0.025-unit soft-particle depth fade for the miniature city.

The current system ignites targets independently and awards CP for successful water-based extinguishing. It does not yet spread fire between objects or destroy buildings.

## Validation

Checked in Play mode: automatic building/tree ignition, concurrent-fire cap, partial water intensity, extinguishing, cooldown rejection, reuse of the same pooled instance, and pause/resume. C# compilation and Unity Console error checks passed. `Artifacts/RandomFires/Preview.png` shows the configured effects in the game camera.

Lifecycle regression check: disabled a burning building, disabled the level with burning objects, disabled the fire controller, and exited Play mode without the parenting exception. Continuous `PlayerWaterSpray` ray hits also extinguished a building after 3.012 water units and a tree after 1.507 water units; those checks used the running spray rather than manually raising water-hit events.

After the environment conversion, all 34 prefab variants passed component, anchor, collider, material-reference, water-amount, and cooldown checks. Play mode discovered all 103 targets. Physics ray hits on a converted house and Nature Kit tree were routed through the water-hit event: half the required water left half intensity, full water extinguished each fire, and the two completions awarded 200 CP total. Climate and warning-HUD counts rose and cleared correctly. Disabling another burning target cleared it without a reward. The Unity Console contained no errors after leaving Play mode.

BaseHouse validation: all 24 houses selected new models at startup (13 distinct models in the test run), and the fire controller discovered all 116 scene targets. Switching through all 21 building models kept the same burnable target and produced valid mesh-collider ray hits. A physical water hit routed through the water event reduced a randomized house to half intensity after 1.5 water units, extinguished it after 3, and awarded 100 CP. Climate/HUD counts and cooldown behaved correctly; model changes were rejected while burning. Disabling another burning house cleaned up without a reward. No Console errors or warnings remained after leaving Play mode.

Tree visibility validation: 104 placement checks covered all 13 tree variants, four camera directions, orthographic/perspective projection, rotation, and nonuniform object scale. All kept the logical fire anchor's screen position within 0.002 pixels while placing the effect at least 0.04999 units in front of the rendered bounds. The pooled effect followed four camera rotations in LateUpdate. A real camera ray still hit the original tree collider; partial water shrank the flames, full water extinguished them and awarded 100 CP with floating text, and disabling another burning tree returned its effect safely. Buildings retained their original fire positions. `Artifacts/TreeFireVisibility/Before.png` and `After.png` show identical particle simulation with the canopy offset disabled/enabled.

Fire audio validation: Play-mode checks covered Small/Medium/Large for 1/2/3 nearby fires, crossfades in both directions, silence with three fires still burning outside the hearing radius, and unchanged proximity when the target's height moved by 50 units. At the midpoint of the distance fade, gain was 0.5 and source volume was 0.325. Range entry/exit hysteresis passed at 5.9/6.1/6.2/6.4 units. Pause preserved playback samples; resume kept the clip position. Disabling/re-enabling reused the same three sources and rediscovered existing fires. Extinguishing the last nearby fire faded to silence while two distant fires kept burning. Embedded fire audio stayed disabled, and the SFX-routed medium loop produced nonzero audio output. Cleanup stopped all voices; the Console had no errors after leaving Play mode.

Extinguish-cue validation: a physical water ray completing a fire started exactly one non-looping SFX voice, even without a reward task. Partial watering, extra water after completion, disabling another burning target, and `ExtinguishAll` added no voices. The clip was loaded before use, produced nonzero audio output through the SFX mixer, and ended naturally after approximately 10.1 seconds. The shared pool did not grow. No Console errors or warnings were reported during this check.
