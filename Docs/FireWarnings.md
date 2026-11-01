# Screen-edge fire warnings

`DemoScene` includes a **FireWarningHUD** prefab instance. While playing, every burning object outside the camera view gets an amber warning triangle and an arrow pointing toward its fire position. The triangle stays upright; the arrow rotates. Warnings disappear when the target enters view, is extinguished, is disabled, or is destroyed. The HUD hides while gameplay is paused and restores its markers on resume.

The implementation adapts the world-to-screen and edge-clamping approach in Soulbonk's `UIFollowTransform` and `ScreenBoundaryUtility`. It supports this project's orthographic camera, accounts for the camera's pixel rectangle and `Screen.safeArea`, and leaves room for the entire warning and arrow. Nearby warnings on the same edge spread apart. Extremely crowded edges reduce their spacing to remain inside the bounds.

## Assets and settings

- `Assets/_Project/UI/Prefabs/FireWarningHUD.prefab`: a Screen Space Overlay Canvas, Canvas Scaler, and `FireWarningHud`. Drag it into another gameplay scene to reuse the HUD. Its scene instance references MainCamera; an unassigned World Camera falls back to `Camera.main`.
- `Assets/_Project/UI/Prefabs/FireWarningMarker.prefab`: the reusable warning and directional arrow. Change its Image sprites/colors or sizes here. If changing its full size, update Marker Radius on the HUD too.
- `Assets/_Project/Scripts/UI/HUD/FireWarningHud.cs`: subscribes to fire ignition/extinguishing events and positions markers after camera movement. Enabling the HUD mid-game also finds already-burning objects. Marker instances are reused and stay under the Canvas rather than becoming children of burning objects.
- `Assets/_Project/Scripts/UI/HUD/FireWarningMarker.cs`: positions each warning and rotates its separate arrow.

Defaults use a 1920 × 1080 reference resolution, 16-pixel edge padding, a 50-pixel marker radius, and 88-pixel minimum spacing. These are Canvas reference pixels and scale with resolution. Images have Raycast Target disabled, the marker CanvasGroup does not block raycasts, and the HUD has no GraphicRaycaster; it does not intercept movement, aiming, or spraying input.

The warning and arrow PNGs come from the local asset library's **Kenney Game Icons** pack (`PNG/White/2x/warning.png` and `right.png`). Their CC0 license is included at `Assets/_Project/UI/Sprites/FireWarnings/Kenney-Game-Icons-License.txt`.

## Verification

Play-mode checks covered three simultaneous off-screen fires at the left, right, and top edges; arrow alignment; on-screen hiding; extinguishing and disabling burning objects; re-enabling the HUD with existing fires; pause/resume; pooled-marker reuse; 88-pixel separation for nearby fires; and an offset, narrower camera viewport. No Console errors were reported. See `Artifacts/FireWarnings/Preview.png` for the in-game preview.
