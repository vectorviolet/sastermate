# Ambient road traffic

Cars follow directed lane routes that you place on the road. The manager uses **one shared TrafficCar prefab** and randomly chooses a route, a cruising speed, and the interval before another spawn. Each TrafficVehicle chooses a visual model from a shared **TrafficModelCatalog** asset before its spawn is checked. They accelerate, brake, queue behind cars, and reuse pooled instances. Open routes return cars to the pool at the endpoint; Loop routes include the final segment back to the first waypoint. Traffic advances only during Gameplay.

During **Awake**, the manager places **Initial Cars** at random distances along the lanes, including positions between waypoints, up to **Maximum Cars**. `DemoScene` starts with eight. Each car faces the local lane direction and begins at its chosen cruising speed. Initial placements use the same fitted colliders, safety gaps, obstacle checks, and junction exclusions as normal spawns; occupied candidates are retried, and a full lane may result in fewer cars. First choices rotate across routes to spread the population when several lanes exist.

**Initial Spawn Attempts** limits the random positions tried per lane for each starting car (24 by default). Set Initial Cars to zero to retain gradual spawning from an empty road. Subsequent timed spawns still enter at the first waypoint rather than appearing midway through moving traffic.

Driving components and settings belong on the common prefab. Visual variety comes from FBX model references in the catalog, so each body model does not need another driving prefab. Chosen visual instances are cached per pooled car, and only one catalog model is active at a time. The collider is refitted to the current model and common attachments before the manager checks spawn clearance; inactive cached models cannot enlarge the footprint.

Vehicle separation uses the fitted BoxCollider's actual size and orientation. Each physics step checks the full swept box of the proposed movement, including rotation, against other cars and movements already granted during that step. This prevents endpoint-only checks from allowing two cars to cross through each other between frames. A blocked spawn is skipped. The manager never forces a car through an occupied area.

TrafficJunction zones reserve crossings **and sharp bends** for one car until its whole body clears them. These zones give turning cars room for their rear to swing without a follower closing that space. Opposing traffic needs separate lane routes; cars stop when blocked and do not overtake or reverse out of a head-on blockage.

## Manual editor setup: start with one straight lane

### 1. Create a model catalog and one car prefab

1. In `Assets/_Project/Cars`, right-click and choose **Create > Climate Guardians > Traffic > Model Catalog**. Name it **TrafficModels**.
2. Add the desired visual FBXs from `Assets/_Project/Cars/Models` to its **Models** array, for example sedan, taxi, and SUV. Use the original visual models rather than copies with driving components. Entries with no renderer, a Rigidbody, or a TrafficVehicle are skipped.
3. Use the catalog's **Model Scale** to fit the whole model family to one lane, and **Model Rotation** to make the imported models face the car root's **+Z**. The current curated car models already import at scale 0.15; start with Model Scale `(1, 1, 1)` and adjust visually in Play mode. **Center And Ground** is optional and off by default so authored model pivots are preserved.
4. Open the existing `Assets/_Project/Cars/Prefabs/TrafficCar.prefab`. Keep its root at scale `(1, 1, 1)` with its TrafficVehicle, Rigidbody, and BoxCollider. Remove the previous fixed **Model** child to avoid displaying it alongside a picked model.
5. Add an empty child named **Visuals**, reset its local transform, and assign it to **Traffic Vehicle > Visual Root**. Assign **TrafficModels** to **Model Catalog**. Visuals should contain only models controlled by the picker; common wheels or other fixed attachments can stay elsewhere under TrafficCar. Visual Root can also be left empty for an automatic runtime container.
6. Save the common prefab. A different model can be selected each time a pooled car respawns. Each selected model is automatically refitted even when **Fit Collider On Awake** is disabled; that checkbox only controls the older preplaced-model setup.

For a new common prefab, start with an empty TrafficCar and add **Traffic Vehicle** to get its Rigidbody and BoxCollider. The controller configures its kinematic body at runtime. Cars with no catalog can still use a preplaced visual model.

### 2. Draw a directed lane

1. Create an empty **Lane_A** and add **Traffic Route**.
2. Add direct empty children named **Point_00**, **Point_01**, etc. Place them along the **center of one driving lane**, in travel order. Their Y coordinate is the car root's driving height, just above the road surface.
3. Leave **Waypoints** empty; the route uses its direct children's Hierarchy order. With Scene view **Gizmos** enabled, cyan lines and arrows show the path, numbered markers show waypoint order, green marks START, and red marks an open route's END. Loop routes show their closing segment and START / LOOP label. Keep only waypoints as direct children of the route root.
4. Leave **Loop** off for the first straight-road test. Put the start and end beyond the visible road when possible so spawns/despawns are unobtrusive. Use at least two distinct points.
5. Add several points around curved road pieces so the polyline follows the bend inside the lane. A waypoint line crossing a sidewalk or a building is still the path the car will try to follow.

For opposing traffic, make **Lane_B** on the other side of the road with its points ordered in the opposite driving direction. Each route represents one lane. Enable Loop only for a complete, connected road loop; inspect the closing line from last to first to ensure it stays on the road.

Under **Traffic Route > Scene Gizmos**, use **Show Gizmos**, **Show Waypoint Labels**, **Draw On Top**, **Route Color**, **Gizmo Size**, and **Gizmo Height Offset** to adjust the preview. Selected routes use thicker lines. Marker sizes adapt to the Scene camera, and incomplete routes show a prompt to add waypoints. These previews also draw when the component is collapsed; their drawing code is editor-only.

### 3. Add the manager

1. Under Managers, create an empty **Traffic** with **Traffic Manager**. Keep its scale at **(1, 1, 1)**.
2. Assign the common TrafficCar prefab to the single **Car Prefab** field. Older saved scenes retain a hidden fallback to the first prefab from their former list; explicitly assign the new field when updating the setup.
3. Leave **Routes** empty to discover enabled TrafficRoutes automatically, or assign the routes explicitly. Use one TrafficManager for the road network.
4. Start with **Maximum Cars = 3**, **Spawn Interval = (2, 4)**, **Cruise Speed = (0.45, 0.8)**, and **Safety Gap = 0.03**. Leave **Obstacle Mask = Nothing** for the initial car-following test; managed cars are always checked independently of that mask.
5. Save the scene and press Play. The first attempt to spawn occurs after about one second. Cars should follow the lane and stop behind a slower or blocked car. If the first point is occupied, the manager tries other routes and otherwise waits for its next spawn attempt.

## Crossings and tight bends

Before populating a route with crossings or sharp corners:

1. Create an empty at each crossing or sharp bend and add **Traffic Junction**. No collider is needed for this component.
2. Resize **Size** and, if necessary, **Center** so the amber box covers the whole crossing/turn and the turning car's full footprint. The default `(1, 2, 1)` is a starting point, not an automatic fit to a road tile. Its height must include the vehicle colliders.
3. Place one shared zone over an intersection used by different routes. Use the same kind of zone at a tight 90-degree bend to keep a follower outside while the leader turns. Leave room between successive zones for cars to clear the preceding zone.
4. Keep route **spawn points outside these boxes**, including room for the entire car. The manager deliberately refuses spawns inside a junction.
5. Add the remaining lanes, save, and test first with three cars before increasing the cap.

Smooth, generously spaced curves can use multiple lane waypoints; tight turns need the reservation zone. Incorrect lane direction, insufficient lane width, undersized turn zones, or routes that feed into each other head-on can cause queues. Cars remain stopped in these cases instead of overlapping to escape.

## Stopping for the player or scenery

The optional **Obstacle Mask** enables swept overlap checks against physics colliders. Use dedicated obstacle layers, and exclude road surfaces/ground that touch the vehicle boxes. Trigger colliders are ignored.

For player stopping, create a layer such as **TrafficObstacle** under **Project Settings > Tags and Layers**, put the player's Rigidbody/Collider object on that layer, and include it in the TrafficManager's Obstacle Mask. Additional solid obstacles can use this layer too. If an extinguishable object's collider is moved to a new layer, include that layer in PlayerWaterSpray's Aim Mask and Hit Mask so water can still reach it. Use the lane routes to keep normal traffic out of buildings and sidewalks.

## Tuning

| Component | Setting | Meaning |
| --- | --- | --- |
| Traffic Manager | Maximum Cars | Maximum active cars; blocked starts can keep the actual count lower |
| Traffic Manager | Initial Cars | Cars placed along lanes during Awake, capped by Maximum Cars |
| Traffic Manager | Initial Spawn Attempts | Random positions tested per lane per initial car |
| Traffic Manager | Spawn Interval | Random seconds between spawn attempts |
| Traffic Manager | Cruise Speed | Random speed range in world units/second |
| Traffic Manager | Safety Gap | Horizontal clearance between the fitted boxes |
| Traffic Vehicle | Acceleration / Braking | Speed changes in world units/second² |
| Traffic Vehicle | Turn Speed | Maximum yaw change in degrees/second |
| Traffic Vehicle | Model Catalog | Shared collection of visual models |
| Traffic Vehicle | Visual Root | Container for cached, randomly selected visual instances |
| Traffic Vehicle | Fit Collider On Awake | Fit a preplaced visual when the clone initializes; selected catalog models always refit |
| Traffic Model Catalog | Model Scale / Model Rotation | Shared visual size and orientation correction |
| Traffic Model Catalog | Center And Ground | Optional pivot alignment; preserves authored pivots when off |
| Traffic Junction | Size / Center | Exclusive crossing or turning area |

This is controlled ambient traffic on authored routes. It does not infer lane connections from road meshes, change lanes, simulate suspension, or choose new branches halfway through a route. Random route selection happens when a car spawns. Edit route layouts and model sizes outside Play mode.

## Validation

The gameplay assembly compiles with the project's Unity 6000.3.18f1 packages. Nineteen headless checks cover lane traversal, loop/end handling, clearance, narrow parallel lanes, bridge height separation, high-speed crossing paths, safe spawning, rotation sweeps, exclusive gates, and an eight-car, 20,000-step loop with corner reservations. The loop accumulated over 1,400 world units of travel with no physical footprint overlaps or global standstill.

Startup population was also checked in Play mode on DemoScene's 242.85-unit Lane_A loop: eight cars appeared immediately at different lane distances, spanning 146.45 units in the sampled run, with no fitted-footprint clearance overlaps. All eight advanced, totaling over 398 units of travel before GameOver stopped traffic, and remained non-overlapping. Additional Awake checks confirmed that Initial Cars is capped by Maximum Cars, zero disables startup filling, and a lane fully covered by a junction receives no initial cars. The 19 core regression checks still pass.

After catalog setup, check in Play mode that only one picked body is visible per car, that different models appear over several spawns, and that the root BoxCollider matches each selection. Check shared orientation and wheel/road height before increasing the car cap. Model changes occur only while the pooled car is inactive, before it is allowed onto the road.

Run the core regression checks from the project root:

```powershell
./Tests/Traffic/Run-TrafficChecks.ps1
```

Pass `-UnityEditorDirectory` if the matching Unity installation is outside the standard Unity Hub location. The test runner writes its compiler outputs under `Temp/Traffic` and does not open or modify a Unity scene.
