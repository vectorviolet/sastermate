# Flat toon shading and screen-space outlines

Target: this project's Unity **6000.3.18f1**, **URP 17.3.0**, Universal Renderer, Render Graph enabled. The live project uses `PC_RPAsset`. The desktop renderer is `Assets/Settings/RenderPipeline/Data/PC_Renderer.asset`. The editable graph and sample materials are included; the instructions below explain their construction and how to enable the outline feature.

The two effects work together: the material chooses flat face colors from the main light; the renderer feature adds black contours over opaque geometry. Neither requires changing mesh scale or the orthographic camera's follow behaviour.

## 1. Toon material in Shader Graph

### Ready-to-use assets

- `Assets/_Project/Shaders/SG_FlatToon.shadergraph`: the complete 24-node graph, including its four vertex/fragment blocks, with five exposed properties.
- `Assets/_Project/Materials/M_FlatToon.mat`: a white base material for general use. Duplicate it and set Base Map and Base Color to match the original material.
- `Assets/_Project/Materials/M_KenneyCity_FlatToon.mat`: uses the city kit's `colormap.png`, preserving its palette/UV colors. All 40 city FBX models map their `colormap` material to this shared material through their importer settings, so new instances inherit it. All 40 city mesh renderers currently in `DemoScene` use it, with shadow casting and receiving enabled.

Double-click the graph to edit its nodes. The graph is **Opaque / Unlit**, with **Float** precision, **Cast Shadows on**, and **Keep Lighting Variants on**. Its Custom Function references `ToonMainLight.hlsl` by asset GUID. Band Cutoff defaults to `0.35`.

For the player, duplicate `M_FlatToon` and assign `Assets/_Project/Player/blocky-characters/Models/Textures/texture-a.png` as Base Map. Each asset family needs its own original palette texture.

The graph and its DepthNormalsOnly pass were checked with the outline feature on the city meshes. See `Artifacts/CartoonRendering/CityToonApplied.png` for the current city setup, including real-time shadows.

### Graph settings

1. Open the included `SG_FlatToon.shadergraph`. To build a separate variant from scratch, use **Shader Graph > URP > Unlit Shader Graph** under `Assets/_Project/Shaders`.
2. Open it. In **Graph Inspector**, use the **Universal / Unlit** target, **Surface = Opaque**, **Render Face = Front**, **Alpha Clipping = Off**, **Cast Shadows = On**, and **Keep Lighting Variants = On**. The latter keeps the main-light shadow/cascade variants needed by the Custom Function.
3. Use **Float** graph precision. Leave vertex Position/Normal/Tangent unchanged.
4. Use one Directional Light as the main light. Its rotation controls band placement, and its color/intensity affect the final material color. Use **Shadows = Hard** for crisp shadows, or **Soft** for softer shadow-map edges. The light and active URP asset must support main-light shadows.

Use an **Unlit** graph deliberately. A Lit graph applies URP's lighting after your custom color calculation. Here, quantized NdotL controls face shading, while the Custom Function explicitly adds the main light's shadow attenuation and color.

### Blackboard properties

| Display name | Reference | Type | Starting value |
| --- | --- | --- | --- |
| Base Map | `_BaseMap` | Texture2D | White, or the original Kenney palette texture |
| Base Color | `_BaseColor` | Color, non-HDR | White |
| Shadow Tint | `_ShadowTint` | Color, non-HDR | RGB `(0.55, 0.60, 0.70)` |
| Lit Tint | `_LitTint` | Color, non-HDR | White |
| Band Cutoff | `_BandCutoff` | Float, Slider 0–1 | `0.35` |

Kenney assets often get their colors from a palette texture and UVs. Copy the original material's Base Map and tint to your new material. Do not replace that texture with one solid color unless that is the intended result. Keep any original texture tiling/offset in a **Tiling And Offset** node if the original material uses them. Avoid textures with baked soft shading if every visible surface must remain flat-colored.

### Main light Custom Function

Add a **Custom Function** node with:

- **Type = File**
- **Source = `Assets/_Project/Shaders/ToonMainLight.hlsl`**
- **Name = `ToonMainLight`**, without `_float` or `_half`
- One **Vector3** input named **`Position`**, connected to **Position / Absolute World**
- **Vector3** outputs **`Direction`** and **`LightColor`**, plus a **Float** output **`Shadow`**

The file supplies both precision variants and a preview fallback. `GetMainLight().direction` points **toward** the light, so do not negate it. `MainLightRealtimeShadow` samples its shadow map using world-to-shadow coordinates, with a separate screen-space-shadow path when that variant is active. The Direction-only overload remains available for older graphs.

### Exact two-band node connections

| Node | Input connections | Output destination |
| --- | --- | --- |
| UV, channel UV0 | — | Sample Texture 2D: UV |
| Sample Texture 2D | Texture = Base Map | RGB → first Multiply A |
| Multiply (albedo) | A = texture RGB; B = Base Color RGB | final Multiply A |
| Normal Vector | Space = World | Normalize (normal) In |
| Normalize (normal) | In = world normal | Dot Product A |
| Custom Function: ToonMainLight | As configured above | Direction → Normalize (light) In |
| Normalize (light) | In = Direction | Dot Product B |
| Dot Product | A = normal; B = light direction | Saturate In |
| Saturate | In = dot product | Step In |
| Step | **Edge = Band Cutoff; In = saturated NdotL** | shadow-band Multiply A |
| Position | Space = Absolute World | Custom Function Position |
| Multiply (shadow band) | A = Step; B = Custom Function Shadow | Lerp T |
| Lerp (band tint) | A = Shadow Tint RGB; B = Lit Tint RGB; T = shadow band | albedo/tint Multiply B |
| Multiply (albedo/tint) | A = albedo; B = band tint | main-light Multiply A |
| Multiply (main-light color) | A = albedo/tint; B = Custom Function LightColor | Fragment **Base Color** |
| Float constant 1 | — | Fragment **Alpha**, if that block is present |

Equivalent math:

```hlsl
float ndotl = saturate(dot(normalize(normalWS), normalize(mainLightDirection)));
float band = step(bandCutoff, ndotl); // exactly 0 or 1
float3 finalColor = baseMapRGB * baseColorRGB
    * lerp(shadowTint, litTint, band * realtimeShadow) * mainLightColor;
```

Face shading stays discrete because NdotL passes through Step. Shadow-map attenuation blends toward Shadow Tint; soft shadows can therefore soften the shadow boundary while face-lighting bands stay sharp. Do not multiply the result by the original smooth NdotL. Ambient, baked GI, specular, and reflections are not added by this graph.

Create a material from the graph and try it on one building first. For the Kenney models, preserve their imported hard normals. Recalculating smooth normals can make a single face's band boundary appear curved. Inspect the source mesh's normals if a flat face shows multiple bands.

### Optional third band

Add Mid Tint, Highlight Tint, and a second cutoff. For example, use cutoffs `0.2` and `0.75`:

```hlsl
float shadowToMid = step(0.2, ndotl);
float midToHighlight = step(0.75, ndotl);
float3 tint = lerp(shadowTint, midTint, shadowToMid);
tint = lerp(tint, highlightTint, midToHighlight);
tint = lerp(shadowTint, tint, realtimeShadow);
```

In nodes, feed the first Lerp into a second Lerp's A input; feed Highlight Tint into B and the second Step into T. Keep the first cutoff below the second.

### Remove the remaining soft shading

- Disable **ScreenSpaceAmbientOcclusion** on the active renderer. `PC_Renderer` currently has this feature enabled.
- Turn off fog, Bloom, depth of field, and any Volume effects that add gradients or alter the palette. A neutral/no-tonemapping setup makes the flat material colors easier to judge.
- Keep renderer **Cast Shadows** and **Receive Shadows** enabled for objects using the graph. It receives the main directional light's real-time shadows through the Custom Function; baked GI and additional-light shading remain outside this graph.
- Keep the orthographic camera. A rotation near X = 30°, Y = 45° gives the project's intended view; camera position and size control framing.

## 2. Screen-space outline Renderer Feature

Files:

- `Assets/_Project/Scripts/Rendering/ScreenSpaceOutlineFeature.cs`
- `Assets/_Project/Shaders/ScreenSpaceOutline.shader`

### Enable it

1. Let Unity import and compile both files.
2. Select the URP asset used by the current quality level. Open its **Renderer List** entry used by the camera. For the desktop setup, this is `Assets/Settings/RenderPipeline/Data/PC_Renderer.asset`; the mobile quality configuration can use a different renderer.
3. Choose **Add Renderer Feature > Screen Space Outline Feature**.
4. Assign the **ScreenSpaceOutline.shader asset** to **Outline Shader**. Keep this serialized reference; relying only on `Shader.Find` can allow the shader to be stripped from a build. A separate material is unnecessary: the feature owns and disposes its runtime material.
5. Start with the following settings, then save the renderer asset.

| Setting | Starting value | Meaning |
| --- | --- | --- |
| Outline Color | Black, alpha 1 | Opaque black lines |
| Thickness | `3` | Nominal full stroke width in output pixels; try 4 for a heavier style |
| Depth Threshold | `0.02` | Minimum depth curvature in world units; appropriate to the small city scale |
| Normal Threshold | `0.2` | A crease near 37° or sharper can produce a line |
| Show In Scene View | On | Preview while editing |

The feature requests **Depth + Normal** with `ConfigureInput`, so you do not need to force a depth prepass manually. It also requests an intermediate color texture. It targets **URP 17.3 Render Graph**, which is already active in this project; it does not implement the older Compatibility Mode `Execute` API.

### How it works

1. URP supplies the scene depth and world-space normal textures for opaque objects.
2. A fullscreen pass samples the center and eight neighbors. Sample offsets come from camera pixel dimensions and the width setting, independent of object scale and camera distance.
3. Orthographic depth uses `LinearDepthToEyeDepth`; perspective depth uses `LinearEyeDepth`. This avoids the perspective-only depth conversion that made the water VFX hard to see earlier.
4. Normal edges use `1 - dot(centerNormal, neighborNormal)`. The shader uses URP's normal sampling helper, including the octahedral-normal variant for deferred rendering. Zero/missing decoded normals are excluded from the normal comparison; they cannot make a whole material black. Depth-based outlines remain available without normals.
5. Depth curvature is measured in **raw device depth**, which is affine on a projected flat surface for both orthographic and perspective cameras. Symmetric, whole-texel probes cancel the slope. The world-unit Depth Threshold is converted into the equivalent raw-depth difference at the center pixel, with a D24/float quantization tolerance. The requested pixel radius limits the final stroke's footprint separately, retaining thin outlines without treating half-texel sampling bias as an edge.
6. A surface-to-background change produces a silhouette edge. Background-to-background regions remain clear.
7. A Step threshold produces a hard mask, and the shader composites black onto the original camera color, preserving its alpha.

The pass declares its color/depth/normal dependencies to Render Graph. It writes to a separate temporary color texture, then copies back. It never samples and writes the same render target in one pass. The temporary texture is managed by Render Graph, and the owned material is destroyed when the feature is disposed.

The pass runs **Before Rendering Transparents**: opaque buildings and characters receive contours, then transparent water and VFX draw over them. It skips preview/reflection cameras and overlay cameras; attach it to the base camera's renderer. Screen Space Overlay UI stays unaffected.

### Normals support

Materials must supply a **DepthNormals/DepthNormalsOnly** pass for internal creases. The generated **URP Unlit Shader Graph in this project's URP 17.3** includes a DepthNormalsOnly pass. A different handwritten Unlit shader may not. If silhouettes work but face creases do not, inspect the material's passes and normal texture first.

### Tuning and practical limits

- Increase Thickness to make the line heavier. Width does not multiply by mesh scale, orthographic size, or distance. Pixel sampling introduces small orientation/rounding differences; it is a raster contour with a nominal pixel width.
- At Render Scale = 1, one output pixel corresponds to one rendered texel. Lower render scales/dynamic resolution quantize the contour before upscaling; use Render Scale = 1 when evaluating exact line thickness. The offsets still use output pixel dimensions.
- Lower Depth Threshold to catch small separations; raise it if curved surfaces develop unwanted depth lines. The threshold is in world units, even though line width is in pixels. Flat perspective surfaces no longer require an increased threshold to suppress their ordinary depth slope.
- Lower Normal Threshold for more creases; raise it for cleaner silhouettes. On low-poly meshes, a 0.2–0.5 starting range is useful.
- Depth and normals cannot detect a color boundary on one coplanar surface. Two touching surfaces with identical depth and normals can merge visually. Material/object-ID edges would require another buffer.
- Transparent particles do not normally contribute to the opaque normal/depth textures. They will not receive their own contours from this pass.
- The nine-position stencil samples depth and normals, with eight additional depth-only probes for symmetric slope rejection, plus scene color. It also adds an intermediate color target and a copy. Profile before shipping to WebGL/mobile.
- Avoid strong temporal AA or aggressive upscaling while judging ink lines. Those filters can soften or shimmer thin screen-space details.

Together, these effects produce clean cel shading and ink-like contours. An intentionally hand-drawn look can additionally use authored texture strokes or restrained line irregularity; neither is necessary for the flat lighting and fixed-width outlines implemented here.

### Fix for distant objects turning black

The original outline mask could fill entire surfaces with black because its point-filtered fractional-pixel samples were asymmetric, its linear-eye-depth curvature was not valid for a flat perspective surface, and a zero normal was treated as a strong crease. Fixed eye-depth thresholds also amplified distant depth-buffer quantization noise. The shader now rejects those cases using the sampling and validity rules above. Existing renderer settings and shader references need no reassignment; refresh/reimport the shader if the editor still shows an old result.

GPU regression draws tested seven scenarios at both 1- and 3-pixel widths. Flat orthographic/perspective slopes and missing normals previously produced 3,136 black pixels out of 3,136 tested pixels; the corrected shader produced zero. A quantized distant perspective plane also became clean. Real depth steps, normal creases, and silhouettes retained the same 1- and 3-pixel stroke coverage. Shader compilation and Console error checks passed. Results are recorded in `Artifacts/CartoonRendering/OutlineRegression.json`.

## Imported VFX and mirror compatibility

The fire erosion shader clamps the bases of its noise and mask powers to `0.0001`. Both pressure-water shader variants use the same lower bound for their Fresnel power. This avoids undefined negative-base powers and zero raised to a negative exponent while preserving positive HDR values. These are edits to generated Amplify shader source; regenerating the imported graphs can overwrite them. Retain equivalent Maximum nodes before Power if you edit those graphs later.

The imported kMirrors component uses `ProfilingScope` and `UniversalRenderPipeline.SingleCameraRequest`. Reflection requests run in LateUpdate after camera movement, outside SRP rendering callbacks; submitting from beginCameraRendering causes recursive-render errors. It follows Camera.main during play and the last active Scene view camera while editing. Reflection textures recreate after disable/enable, and the previous GL culling state is restored after each request. Texture creation, recreation, and rendering were checked without warnings or errors.

See Unity's [render request documentation](https://docs.unity.com/en-us/engine/6000.3/manual/cameras/urp/multiple/user-render-requests) for the supported request API.

## References

- [Shader Graph 17.3 Custom Function node](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Custom-Function-Node.html)
- [Unity custom-lighting examples](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Shader-Graph-Sample-Custom-Lighting.html)
- [URP Render Graph](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph.html)
- Implementation was also checked against the installed URP 17.3 `BlitAndSwapColorRendererFeature`, `FullScreenPassRendererFeature`, `DeclareDepthTexture.hlsl`, and `DeclareNormalsTexture.hlsl` sources.
