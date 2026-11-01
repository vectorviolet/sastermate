# Beginner fire schedule

The saved scene's previous schedule started the first fire after 2 seconds, tried another ignition every 8-14 seconds, and allowed 3 simultaneous fires anywhere on the map. A larger map increases travel time without giving the player more time to respond, so the opening schedule now uses a beginner warmup.

Defaults on **RandomFires > Random Fire Controller > Beginner Warmup**:

| Gameplay time | Schedule |
| --- | --- |
| First 12 seconds | No initial fire |
| Through 60 seconds | At most 1 active fire; ignition attempts every 20-30 seconds |
| 60-90 seconds | Intervals interpolate toward the normal settings; the cap reaches 2 at 75 seconds and 3 at 90 |
| After 90 seconds | Existing normal settings: 8-14 seconds and at most 3 active fires |

During the first minute, random ignitions prefer eligible buildings/trees within **Warmup Spawn Radius = 12** world units of the player, measuring XZ distance. If none are eligible inside that radius, the nearest eligible target is chosen. After the first minute, the whole target list is eligible again. This checks distance rather than NavMesh reachability; retain sensible level placement and paths.

**Player Target** is optional and finds the player's PlayerWaterSpray transform at startup. Assign the player root manually if the scene has multiple water-spray actors. Set Warmup Spawn Radius to 0 to allow the whole map even during warmup. The normal fire prefab, task rewards, water handling, fire cooldowns, and maximum pool size retain their existing settings.

The schedule's clock advances only in Gameplay. Menus and pause do not consume the learning period. Replay reloads the scene and starts it again. Turning **Enable Warmup** off uses the original Initial Delay, Ignition Interval, and Max Simultaneous Fires immediately. All durations, initial delay, warmup interval, cap, radius, and ramp duration are Inspector settings.

Playtest the larger map after restarting Play mode: verify the first warning is close enough to find, one fire remains manageable during the first minute, and the post-warmup travel time still permits firefighting and CP purchases. The numerical schedule and temperature/win rules have headless coverage, while native target selection, navigation distances, and moment-to-moment balance still need a real playthrough.
