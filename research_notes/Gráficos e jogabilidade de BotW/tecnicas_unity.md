# Unity 6 Built-in RP Techniques for a BotW-like Look and Feel (stylized low-poly)

> Scope: Built-in Render Pipeline (BiRP), Unity 6000.x, hand-written CG/HLSL shaders (surface or vertex/fragment), no HDRP.
> Convention: everything in "Cited Findings" has a source. Code and math in "Inferences" is standard technique written up by the researcher to make it implementable. It is not quoted from a source; test it before relying on it.

## Version and platform context (applies to everything below)

### Takeaway
BiRP still works and is supported in Unity 6, but it is officially deprecated. Unity says it gets maintenance through the Unity 6.7 LTS lifecycle. Post Processing Stack v2 (`com.unity.postprocessing`) is still maintained for Unity 6: 3.4.0 is listed for 6000.0, 3.5.x for 6000.3 and later, and the latest is 3.5.4 (March 2026).

### Cited Findings
- Unity 6.6 docs: "The Built-In Render Pipeline is deprecated and will be made obsolete in a future release. It remains supported, including bug fixes and maintenance, through the full Unity 6.7 LTS lifecycle." — [Unity 6.6 Manual, Surface Shader output](https://docs.unity3d.com/6000.6/Documentation/Manual/SL-SurfaceShaders-output.html)
- The Unity 6.0 (6000.0) manual lists `com.unity.postprocessing@3.4` as a released package (3.4.0). The Unity 6.3 LTS manual lists 3.5.x. — [Unity 6000.0 Manual: com.unity.postprocessing](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.postprocessing.html); [Unity 6.3 Manual](https://docs.unity3d.com/Manual/com.unity.postprocessing.html)
- PPv2 changelog: 3.4.0 (2023-12-11) added WebGPU and fixed compute effects on WebGL/Android GL, an obsolete FormatUsage error, and transparent objects with TAA off. 3.5.0 (2025-06-18) removed PVRTC support in Unity 6.1+, added Switch2, and fixed compute effects on GLES. 3.5.2 (2025-12-17) fixed obsolete ProjectWindow usage in Unity 6.4+ and moved to a CoreCLR-compatible API. 3.5.3 (2026-02-19) declared an explicit UI module dependency. 3.5.4 (2026-03-13) fixed serialization errors. — [PPv2 CHANGELOG](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.5/changelog/CHANGELOG.html)
- The PPv2 3.4 "requirements" page still says "Unity 2018.1+". It is out of date for Unity 6. — [PPv2 3.4 requirements](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.4/manual/requirements.html)
- URP and HDRP ship their own post-processing. PPv2 is the BiRP solution. — [Unity 6 Post-processing overview](https://docs.unity3d.com/6/Documentation/Manual/PostProcessingOverview.html)

### Inferences
- Pin PPv2 to whatever version the Package Manager offers for your exact editor: 3.4.0 on 6000.0 LTS, 3.5.x on 6000.1 and later.
- Because BiRP is deprecated, keep shaders in plain HLSL-ish CG with lighting in shared include files (`ToonLighting.cginc`). That makes a later URP port mostly a matter of swapping light-access functions.

### Gaps
- No official statement says whether PPv2 will be supported past 6.7 LTS.

---

## 1. Toon/cel shading BotW-style (banded diffuse, shadow tint, lit-side rim, specular bands, painted overlay, outlines)

### Takeaway
The standard BiRP base is Roystan's toon shader (Unlicense). It uses `smoothstep` on N·L multiplied by shadow attenuation for the band, a smoothstep Blinn-Phong specular, and a rim term multiplied by N·L so it only shows on the lit side. BotW's look adds a colored shadow tint instead of black/ambient, a wide soft terminator, and a strong lit-side rim ("lateral shine"). The URP Shader Graph recreations (ciro-unity, ccontinisio, Daniel Ilett) are good visual references, but they need porting to BiRP.

### Cited Findings
- Roystan toon shader math (BiRP, vertex/fragment, `ForwardBase`):
  - `NdotL = dot(_WorldSpaceLightPos0, normal)`
  - `lightIntensity = smoothstep(0, 0.01, NdotL * shadow)`
  - Specular: `H = normalize(lightDir + viewDir)`, `spec = pow(NdotH * lightIntensity, _Glossiness * _Glossiness)`, then `smoothstep(0.005, 0.01, spec)`
  - Rim: `rimDot = 1 - dot(viewDir, normal)`, multiplied by `pow(NdotL, _RimThreshold)`, then `smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, ...)`
  - Final: `_Color * tex * (_AmbientColor + light + specular + rim)`
  - Shadows come from `UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"`, `#pragma multi_compile_fwdbase`, `SHADOW_COORDS`, `TRANSFER_SHADOW` and `SHADOW_ATTENUATION`.
  - For multiple bands, sample a ramp texture with `u = NdotL*0.5+0.5`.
  — [Roystan, Toon Shader](https://roystan.net/articles/toon-shader/)
- Roystan's tutorial calls the code free and open source. The repo `IronWarrior/UnityToonShader` is under the **Unlicense** (Unity 2018.3 project) and covers specular, rim, and shadow casting/receiving. It has no outlines. — [GitHub IronWarrior/UnityToonShader](https://github.com/IronWarrior/UnityToonShader)
- `ciro-unity/BotW-ToonShader` is a Shader Graph, **URP**-only project (Unity 2021.2.5, URP 12.1.2) tied to Unite Copenhagen 2019 talks. Its README says it is free for commercial use with no credit required, and it lacks baking, point/spot lights and reflection probes. — [GitHub ciro-unity/BotW-ToonShader](https://github.com/ciro-unity/BotW-ToonShader)
- `ccontinisio/zelda-botw-toon-shader` is a URP fan-art shader that focuses on the rim-style edge light and strong lateral shine. `unitycoder/shaders-botw-cel-shading` is a Shader Graph recreation by Daniel Ilett with custom lighting code. — [ccontinisio repo](https://github.com/ccontinisio/zelda-botw-toon-shader); [unitycoder repo](https://github.com/unitycoder/shaders-botw-cel-shading) (licenses not verified for these two)
- Art direction background: BotW art director Satoru Takizawa (GDC 2017) said the team started from Wind Waker HD's look but moved toward a style that suggests real-world physics and chemistry, under the slogan "refreshing and full-flavoured". — [Nintendo Everything](https://nintendoeverything.com/zelda-breath-of-the-wild-art-director-on-how-the-wind-waker-hd-shaped-the-games-art-style/); [Zelda Universe](https://zeldauniverse.net/2017/03/14/takizawa-explains-how-the-wind-waker-hd-inspired-the-visual-style-for-breath-of-the-wild/)

### Inferences (implementation recipe, BiRP vertex/fragment)
- **Banded diffuse with a soft terminator.** Use `float band = smoothstep(_TermCenter - _TermSoft, _TermCenter + _TermSoft, NdotL * atten);` with `_TermSoft` around 0.05–0.15. BotW's terminator is softer than Roystan's 0.01. For 3 bands, use a 1D ramp texture (point-filtered, clamp).
- **Shadow color tint, not black.** Use `float3 lit = albedo * _LightColor0.rgb; float3 shd = albedo * _ShadowTint.rgb;` (a blue/purple tint, e.g. (0.55,0.6,0.8)), then `col = lerp(shd, lit, band) + ambientSH * albedo * _AmbientStrength;`. Get `ShadeSH9(float4(n,1))` from UnityCG.cginc so ambient follows the day/night skybox and ambient settings.
- **Rim only on the lit side.** Use `rim = smoothstep(_RimAmt-0.01,_RimAmt+0.01, (1-saturate(dot(V,N))) * pow(saturate(NdotL), _RimThresh)) * band;`, colored by `_LightColor0`. A BotW extra worth adding: weight the rim by `saturate(dot(-V.xz, L.xz))`, so it is strongest when the light is behind or to the side of the object, which matches the "lateral shine".
- **Specular bands.** Use `smoothstep` on Blinn-Phong as Roystan does. For 2 bands, sum two smoothsteps at different thresholds, or sample a ramp. Mask it with a `_SpecMask` texture channel so cloth stays matte and metal shines.
- **Painted/brush texture overlay.**
  - Option A: in world or triplanar space, multiply albedo by a tileable low-contrast brush-stroke texture: `lerp(1, brush, _BrushStrength)`.
  - Option B: perturb the terminator with the brush, `NdotL += (brush - 0.5) * _BrushWarp`, so band edges look painted. Option B is the more BotW-like one.
  - Screen-space hatching: sample the hatch texture with `i.screenPos.xy / i.screenPos.w * _ScreenParams.xy / _HatchScale` and apply it only in the shadow band. It swims under camera motion, so world-space or triplanar is usually better for 3D.
- **Additional lights.** Add a `ForwardAdd` pass (`Blend One One`, `#pragma multi_compile_fwdadd_fullshadows`) using the same band function, so point lights (torches, fire arrows) get toon bands too.
- **Outlines.**
  - *Inverted hull*: a second pass with `Cull Front`, vertices extruded along the normal in view or clip space. Clip-space extrusion gives constant pixel width: `o.pos = UnityObjectToClipPos(v); float3 n = mul((float3x3)UNITY_MATRIX_MVP, v.normal); o.pos.xy += normalize(n.xy) / _ScreenParams.xy * _Width * o.pos.w * 2;`. Low-poly hard edges split normals, so bake smoothed normals into a UV channel or vertex color for the outline pass. It's cheap and gives a per-object outline color. Recommended for characters.
  - *Post-process edge detection*: Sobel or Roberts on `_CameraDepthNormalsTexture` (BiRP: `cam.depthTextureMode |= DepthTextureMode.DepthNormals`) as a PPv2 custom effect. It gives one global outline including terrain creases. It costs a full-screen pass plus an extra depth-normals pass, so it is heavier on mobile.
  - BotW itself mostly shows thin dark outlines on characters only. That favors inverted hull for characters plus no outline (or very subtle depth edges) on terrain.

### Gaps
- No primary, Nintendo-authored technical description of BotW's shading model was found. The BotW-specific details above (soft terminator, lateral rim) are based on community recreations and visual analysis.
- Licenses of the ccontinisio and unitycoder repos were not checked.

---

## 2. Dense interactive grass (instancing vs geometry shader, wind, bending, fade, cut/burn, performance)

### Takeaway
There are two proven BiRP-compatible routes. (a) Geometry and tessellation shader grass (Roystan): simple, but needs SM4.6/DX11-class hardware, can't be a surface shader, and doesn't run on Metal. (b) Indirect instancing (`Graphics.RenderMeshIndirect`/`DrawMeshInstancedIndirect`) with compute-shader culling, which reaches millions of instances. Colin Leung's MIT demo shows 10M instances at 50–60 fps on a mid-range Android phone (URP, but the technique carries over to BiRP). Interactivity comes from a top-down "bend" render texture or a small array of player/object positions.

### Cited Findings
- Roystan geometry grass (BiRP, CGPROGRAM, shader model 4.6, D3D11 stages):
  - The geometry shader emits a triangle-strip blade per vertex in tangent space, with random facing, width/height and forward bend, and curvature from a power on the segment position.
  - Hull/domain tessellation sets density (`_TessellationUniform` ≈ 5 recommended).
  - Wind comes from a scrolling 2-channel distortion texture sampled at world position + time. Base vertices don't use the wind matrix, so roots stay planted.
  - A ShadowCaster pass with linear bias handles shadows.
  - Geometry shaders can't be surface shaders.
  - Performance levers: tessellation density, distance-based tessellation, and quads/billboards far away.
  — [Roystan, Grass Shader](https://roystan.net/articles/grass-shader/)
- `Graphics.RenderMeshIndirect(ref RenderParams, Mesh, GraphicsBuffer commandBuffer, int commandCount=1, int startCommand=0)`:
  - Works only on platforms with compute-shader support.
  - Use `GraphicsBuffer.IndirectDrawIndexedArgs` (Target `IndirectArguments`) for args.
  - Set `RenderParams.worldBounds`.
  - In the shader: `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs`, `#include "UnityIndirect.cginc"`, `InitIndirectDrawArgs(0)`, `GetIndirectInstanceID(svInstanceID)`.
  - The official example uses `UnityCG.cginc` and CGPROGRAM, which implies BiRP compatibility, but the doc page doesn't state it.
  — [Unity 6 ScriptReference: Graphics.RenderMeshIndirect](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html)
- Colin Leung (NiloCat) `UnityURP-MobileDrawMeshInstancedIndirectExample`, **MIT**, URP, Unity 2019.4:
  - 10 million instances at 50–60 fps on a Samsung Galaxy A70 (Adreno 612) with draw distance 125.
  - 30 fps on a Lenovo S5 (Adreno 506) with draw distance 75.
  - Cost scales with visible instances.
  - Culling is CPU cell frustum culling, then compute-shader GPU frustum culling, resulting in one `DrawMeshInstancedIndirect` call.
  - Bending uses an offscreen R8 top-down render texture drawn by TrailRenderers following moving objects.
  — [GitHub ColinLeung-NiloCat/UnityURP-MobileDrawMeshInstancedIndirectExample](https://github.com/ColinLeung-NiloCat/UnityURP-MobileDrawMeshInstancedIndirectExample)
- A limitation noted by a derived project: in that demo grass is generated at a fixed height, which breaks on uneven terrain. That project's author moved to geometry shaders. — [GitHub Velorexe/unity-geometry-grass-shader](https://github.com/Velorexe/unity-geometry-grass-shader)
- An infinite GPU-instanced grass field (URP) was built on Colin Leung's approach. — [80.lv](https://80.lv/articles/infinite-gpu-instanced-grass-field-in-unity-urp)

### Inferences (implementation recipe)
- **Recommended for a low-poly game**: indirect instancing of a 3–5-triangle blade or clump mesh.
  1. On load, generate positions by sampling your terrain mesh or heightmap (fixes the flat-height issue), storing `float4 posAndScale` plus `float rot` in a `StructuredBuffer`. Bucket them into chunks (e.g. 16×16 m).
  2. Each frame, CPU frustum-cull chunks. A compute shader then tests per-instance frustum and distance (and optionally a Hi-Z occlusion test) and appends visible indices to an `AppendStructuredBuffer`. `GraphicsBuffer.CopyCount` into `instanceCount` of the args buffer, then `RenderMeshIndirect`.
  3. Vertex shader: read the instance by index, rotate and scale, then apply wind and bend weighted by `uv.y` (or vertex color) so the root stays fixed.
- **Wind**: `float2 w = (tex2Dlod(_WindNoise, float4(worldPos.xz * _WindScale + _Time.y * _WindDir * _WindSpeed, 0,0)).rg * 2 - 1) * _WindStrength; pos.xz += w * h*h;` (h = normalized blade height; h² gives a curved bend). Add a high-frequency `sin(_Time.y*_Flutter + worldPos.x*3)` term at low amplitude. Use the same `_WindNoise` and globals in tree and foliage shaders for coherent gusts (BotW's waves of wind across fields).
- **Player bending**, two options:
  - Cheap: `Shader.SetGlobalVectorArray("_Benders", ...)` with up to ~8 `float4(pos, radius)`. In the vertex shader, `float3 d = worldPos - b.xyz; float f = saturate(1 - length(d.xz)/b.w); pos.xz += normalize(d.xz) * f * _BendStr * h; pos.y -= f * _Squash * h;`.
  - Trampling that persists: a top-down orthographic camera, or `CommandBuffer.Blit` into an R8/RG16 "trample" render texture centered on the player. Draw soft circles at feet each frame, and fade the texture back toward 0 over time with a blit. Grass samples it with world-to-RT UV. RG can store a bend direction, as in Colin Leung's R8 approach.
- **Distance fade**: in the compute cull, drop instances probabilistically with `hash(id) > densityAtDistance`. In the shader, shrink blade height to 0 near the cutoff (`scale *= saturate((_MaxDist - dist)/_FadeRange)`) instead of alpha blending. Alpha is expensive and unsortable. If you use alpha-to-coverage or alpha-test, enable dithering.
- **Cut/burn states**: keep a per-chunk "state" render texture (R = cut height, G = burnt amount) in world space. Cut means height `*= state.r` and spawn a particle burst. Burn means lerping albedo to charcoal, plus an emissive ember edge where `state.g` is in 0.4–0.6, with the fire-spread simulation done on CPU or compute writing to that texture. Regrowth slowly lerps the texture back.
- **Lighting**: give grass the same toon band as terrain. Use the terrain normal (passed per instance), not the blade normal, so fields shade like one surface (a key BotW trick visually). Fade albedo from a dark root to a light tip.
- **Shadows**: grass usually shouldn't cast shadows (big cost). Let it receive the terrain's shadow by sampling the screen-space shadow map (`SHADOW_ATTENUATION` in ForwardBase).
- **Geometry shader caveat**: geometry shaders aren't supported on Metal (macOS/iOS) and are slow on many mobile GPUs. Prefer instancing if any non-Windows target matters.

### Gaps
- No BiRP-specific benchmark of `RenderMeshIndirect` grass on desktop GPUs was found. The 10M figure is URP/mobile.
- The Minions Art interactive grass tutorial (Patreon) couldn't be fetched (HTTP 403), so its details aren't included.

---

## 3. Sky and atmosphere (gradient sky, sun disk, stylized clouds, height/distance fog, aerial perspective)

### Takeaway
In BiRP, a custom skybox shader (`Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}`, `Cull Off ZWrite Off`) assigned via `RenderSettings.skybox` reads `_WorldSpaceLightPos0` for the sun direction. Unity's built-in Procedural skybox already follows the directional light's rotation but has no clouds. Minions Art's "Stylized Skybox Shader" tutorial covers day/night colors, noise clouds, and a sun that follows the light (updated Nov 2025). Fog and aerial perspective are best done in your own shaders (shared include) or as one post-process pass using the depth texture.

### Cited Findings
- Unity's Procedural skybox (BiRP) needs no textures, has sun modes None/Simple/High Quality, Sky Tint, Ground and Atmosphere Thickness. Its sun direction comes from the active directional light's rotation. It has no clouds. — [Unity Manual: Procedural skybox](https://docs.unity3d.com/2020.2/Documentation/Manual/shader-skybox-procedural.html)
- Minions Art "Making a Stylized Skybox Shader" (Unity, Patreon post) covers customizable colors, separate night/day settings, noise-based clouds, and a sun from `_WorldSpaceLightPos0`. It was updated Nov 2025 with clouds fading in the distance and HDR colors. — [Patreon post 27402644](https://www.patreon.com/posts/27402644) (via search summary; the page wasn't fetched directly). A Godot port exists: [godotshaders Stylized SkyShader](https://godotshaders.com/shader/stylized-skyshader/)
- Paid Unity 6 options supporting BiRP: "Jupiter" stylized sky (3-color gradient, animated clouds, day/night). — [Fab listing](https://www.fab.com/listings/8002613c-dfc9-442e-bb7d-3a1afe4f6f1e)

### Inferences (shader math)
- **Gradient sky** (`float3 v = normalize(i.worldDir)`):
  `float t = v.y; float3 sky = lerp(_HorizonCol, _ZenithCol, smoothstep(0, _ZenithBlend, t)); sky = lerp(sky, _GroundCol, smoothstep(0, -0.05, t));`
- **Horizon glow toward the sun**: `float sunDot = dot(v, _WorldSpaceLightPos0.xyz); sky += _SunGlowCol * pow(saturate(sunDot), _GlowPow) * (1 - saturate(v.y*3));`. This gives the warm sunrise/sunset band.
- **Sun disk** (stylized, hard edge): `float sun = smoothstep(_SunSize, _SunSize + 0.002, sunDot - (1 - _SunSize*0.01))`. Simpler: `step(cos(radians(_SunAngle)), sunDot)` with a smoothstep for anti-aliasing. Do the moon the same way with `-_WorldSpaceLightPos0` or a separate `_MoonDir` global, plus an offset disk to cut a crescent.
- **Stylized clouds**: project onto a fake dome with `float2 uv = v.xz / (v.y + _CloudCurve)`, sample 2 octaves of scrolling noise, then threshold with `smoothstep(_Cover, _Cover + _Soft, n)` for BotW/Wind-Waker-like solid clouds. Color is `lerp(_CloudShadowCol, _CloudLitCol, saturate(n2 - n1 offset toward sun))`. Fade with `saturate(v.y*4)` near the horizon. Skip clouds below the horizon.
- **Stars at night**: threshold a high-frequency noise or hash of the cell grid, multiplied by `saturate(-_SunDir.y*4)`.
- **Fog / aerial perspective** (BotW: distant mountains go blue-ish, desaturated and bright):
  - In every opaque shader (shared include): `float d = distance(_WorldSpaceCameraPos, worldPos); float distF = 1 - exp(-d * _FogDensity); float heightF = exp(-max(0, worldPos.y - _FogBaseH) * _FogHeightFalloff); float fog = saturate(distF * heightF); col = lerp(col, _FogCol, fog);`
  - For aerial perspective, use a second, longer-range term: `float aerial = saturate((d - _AerialStart)/_AerialRange); col = lerp(col, lerp(col, _AerialTint, 0.7) + _AerialTint*0.2, aerial);`. Blue-shift and desaturate rather than just fade.
  - Make `_FogCol` blend toward the sky horizon color in the view direction, and toward the sun glow color when looking at the sun (`pow(saturate(dot(viewDir, sunDir)), 8)`). The fog then matches the sky seamlessly, the main trick for "sky and land feel like one".
  - Alternative: one PPv2 custom effect that reconstructs world position from `_CameraDepthTexture` and applies the same formula. It's cheaper to maintain, but transparents (water) need special handling.
  - Unity's built-in `RenderSettings.fog` (`UNITY_FOG_COORDS`/`UNITY_APPLY_FOG`) only does distance fog with one color. It's fine as a fallback but has no height or aerial term.

### Gaps
- No primary BotW technical source on its sky or fog model was found. The parameters are visual approximations.

---

## 4. Day-night cycle and weather (sun rotation, gradients, rain, wet darkening, lightning)

### Takeaway
Drive everything from one normalized `timeOfDay` (0–1). Rotate the directional light's X angle, and evaluate `Gradient`/`AnimationCurve` assets for light color, intensity, ambient (RenderSettings), fog color and sky colors. Push them as shader globals once per frame. Wetness is a global 0–1 that darkens albedo and raises smoothness/specular. Lightning is a brief spike of a directional light plus sky emission.

### Cited Findings
- The Procedural skybox, and any custom skybox reading `_WorldSpaceLightPos0`, follows the rotation of the directional light, which enables a simple day/night by rotating it. — [Unity Manual: Procedural skybox](https://docs.unity3d.com/2020.2/Documentation/Manual/shader-skybox-procedural.html)
- The Surface Shader `SurfaceOutputStandard` exposes `Albedo` and `Smoothness` (0=rough, 1=smooth). These are the fields to drive for wet surfaces. — [Unity 6.3 Manual: Surface Shader output](https://docs.unity3d.com/6000.3/Documentation/Manual/SL-SurfaceShaders-output.html)
- Asset-store examples with BiRP support exist (e.g. "PBR Wet Surface Shader"), but no free reference implementation was found. — [Asset Store: PBR Wet Surface Shader](https://assetstore-fallback.unity.com/packages/vfx/shaders/substances/pbr-wet-surface-shader-94412)

### Inferences (implementation)
- **Day/night controller** (C#):
  ```csharp
  public class DayNight : MonoBehaviour {
    [Range(0,1)] public float time; public float dayLengthSec = 1440; // 24 min, close to BotW's ~1 real s = 1 game min
    public Light sun, moon; public Gradient sunColor, ambientSky, ambientEquator, ambientGround, fogColor, skyZenith, skyHorizon;
    public AnimationCurve sunIntensity;
    static readonly int ZenithID = Shader.PropertyToID("_SkyZenith"), HorizonID = Shader.PropertyToID("_SkyHorizon"), FogID = Shader.PropertyToID("_FogCol");
    void Update(){
      time = (time + Time.deltaTime / dayLengthSec) % 1f;
      sun.transform.rotation = Quaternion.Euler(time*360f - 90f, 170f, 0); // 0.25 = sunrise
      moon.transform.rotation = sun.transform.rotation * Quaternion.Euler(180,0,0);
      sun.color = sunColor.Evaluate(time); sun.intensity = sunIntensity.Evaluate(time);
      moon.enabled = sun.intensity < 0.05f; sun.enabled = !moon.enabled; // swap so only one shadowing dir light
      RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
      RenderSettings.ambientSkyColor = ambientSky.Evaluate(time); RenderSettings.ambientEquatorColor = ambientEquator.Evaluate(time); RenderSettings.ambientGroundColor = ambientGround.Evaluate(time);
      Shader.SetGlobalColor(ZenithID, skyZenith.Evaluate(time)); Shader.SetGlobalColor(HorizonID, skyHorizon.Evaluate(time)); Shader.SetGlobalColor(FogID, fogColor.Evaluate(time));
      RenderSettings.sun = sun.enabled ? sun : moon;
    }
  }
  ```
  Trilight ambient avoids per-frame `DynamicGI.UpdateEnvironment()`, which is expensive if you rely on skybox ambient. If you do use skybox ambient, update it every N seconds only.
  - For BotW's "golden hour", the gradient keys should be warm orange at time 0.24–0.28 and 0.72–0.76, with a slightly blue-cyan noon. Shift the shader `_ShadowTint` toward deep blue at night.
- **Shadow stability**: rotating the sun every frame causes shadow-map shimmer. Quantize the rotation update (e.g. only rotate when the angle changes by more than 0.1°), or use Stable Fit shadow projection.
- **Rain**:
  - A ParticleSystem parented to the camera. Use stretched-billboard mode, ~2–5k particles in a box around the camera, world simulation space, and the collision module disabled.
  - For splashes, a second system with planar emission plus a depth-faded ring.
  - For occlusion under roofs, a cheap top-down depth render texture, or just a raycast grid. Optional.
- **Wet darkening** (global `_Wetness` 0–1, ramped over ~20 s): `albedo *= lerp(1, _WetDarken /*~0.6*/, wet); smoothness = lerp(smooth, 0.85, wet);`. For toon shaders, raise the specular band size and intensity instead. Mask by `saturate(worldNormal.y)` so up-facing surfaces get wetter, and add animated puddle noise in flat areas. Add a ripple normal on terrain with a flipbook or a procedural ring function.
- **Lightning**:
  - A coroutine that sets a secondary non-shadowing directional light (or ambient/sky boost) to intensity 3–5 for 0.05 s, off, then a second flicker 0.1 s later.
  - Set `Shader.SetGlobalFloat("_Flash", x)` so sky and clouds brighten.
  - Play thunder after `distance/343` seconds.
  - Optionally spawn a LineRenderer bolt with jittered points.
  - In BotW, lightning targets metal gear. A gameplay hook: raycast from the sky to the player when metal is equipped, after a warning spark VFX.
- **Weather blending**: a ScriptableObject per weather (fog density, sky/cloud cover, light multiplier, rain rate, wetness target), lerped over 30–60 s.

### Gaps
- No primary-source free BiRP wet-shader reference was found. The formulas above are standard practice, not cited.
- BotW's exact in-game day length wasn't verified with a source here (commonly cited as about 24 real minutes, unverified).

---

## 5. Post-processing in BiRP (PPv2 on Unity 6, bloom, LUT color grading, vignette, custom alternatives)

### Takeaway
PPv2 works in Unity 6 BiRP and is actively maintained (3.5.4, March 2026). Use a `PostProcessLayer` on the camera plus a `PostProcessVolume`. It provides Bloom, Color Grading (LDR LUT, HDR/ACES or Neutral tonemapping), Vignette, AO, SMAA/FXAA/TAA, and custom effects via `PostProcessEffectSettings`/`PostProcessEffectRenderer`, which is the right place for edge-detection outlines or depth fog. `OnRenderImage` still works in BiRP but is single-camera, less composable and slower.

### Cited Findings
- PPv2 3.4.0 is listed as released for Unity 6.0, and 3.5.x for Unity 6.3 LTS. — [Unity 6000.0 Manual: com.unity.postprocessing](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.postprocessing.html); [Unity 6.3 Manual](https://docs.unity3d.com/Manual/com.unity.postprocessing.html)
- 3.5.x fixes cover Unity 6.1+ (PVRTC removal) and 6.4+ (ProjectWindow API), showing it's kept compiling against new editors. — [PPv2 CHANGELOG](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.5/changelog/CHANGELOG.html)
- The post-processing overview says PPv2 is the BiRP solution, and URP/HDRP have their own. — [Unity 6 Post-processing overview](https://docs.unity3d.com/6/Documentation/Manual/PostProcessingOverview.html)

### Inferences
- **BotW-like stack**:
  - Bloom: threshold ~1.0 (HDR camera), intensity 1–3, soft knee 0.5, diffusion 7. Only sun disk, emissives, fire and specular bands should bloom, so keep albedo × light ≤ 1 in toon shaders.
  - Color Grading: Neutral tonemapper (ACES darkens and shifts saturated toon colors), slight saturation +10, lift shadows toward blue, warm highlights. Or bake a LUT in Photoshop/Resolve using the neutral 32×32×32 strip and use LDR LUT mode.
  - Vignette: intensity 0.2–0.3, smooth 0.4.
  - Optional depth of field only in menus or cutscenes.
  - AA: FXAA or SMAA (TAA ghosts on wind-swayed grass).
- **Weather and time-of-day**: blend multiple volumes by `weight` (e.g. a rain volume with desaturation, a night volume with a blue LUT) from the weather controller.
- **Custom effect template** (C# side):
  ```csharp
  [System.Serializable][PostProcess(typeof(OutlineRenderer), PostProcessEvent.BeforeTransparent, "Custom/Outline")]
  public sealed class Outline : PostProcessEffectSettings { public FloatParameter thickness = new FloatParameter{value=1}; }
  public sealed class OutlineRenderer : PostProcessEffectRenderer<Outline> {
    public override DepthTextureMode GetCameraFlags() => DepthTextureMode.DepthNormals;
    public override void Render(PostProcessRenderContext ctx){ var sh = ctx.propertySheets.Get(Shader.Find("Hidden/Custom/Outline")); sh.properties.SetFloat("_Thickness", settings.thickness); ctx.command.BlitFullscreenTriangle(ctx.source, ctx.destination, sh, 0); }
  }
  ```
  The shader includes `Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl` and uses `VertDefault`. Use `BeforeTransparent` so water isn't outlined. Use `BeforeStack` for fog applied before bloom.
- **Without PPv2**: `Camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, cb)` with `cb.Blit` chains, or `OnRenderImage(src,dst)`. These are fine for 1–2 passes. For a full bloom chain it's simpler to use PPv2.

### Gaps
- There's no official Unity statement of PPv2 support beyond the BiRP 6.7 LTS window. Some 6000.x patch releases may emit obsolete-API warnings. Check the console after updating.

---

## 6. Stylized water improvements (refraction, caustics, cheap reflections)

### Takeaway
Catlike Coding's "Looking Through Water" gives a BiRP-ready recipe: GrabPass plus `_CameraDepthTexture`, exponential underwater fog, and UV-offset refraction with artifact fixes. Caustics are cheap as a projected animated texture on underwater surfaces. For reflections, the cheapest option is a blurred sky/fresnel color or a baked reflection probe sampled with `UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflect(...))`. A planar reflection camera is the expensive option.

### Cited Findings
- Catlike Coding "Looking Through Water" (BiRP surface shader):
  - `GrabPass{"_WaterBackground"}` and `_CameraDepthTexture`.
  - `depthDiff = LinearEyeDepth(bg) - surfaceDepth`.
  - Fog `exp2(-_WaterFogDensity * depthDiff)`.
  - Refraction offsets the screen UV by tangent normal XY × strength (V scaled by texel aspect).
  - Fixes: revert to unoffset UV if `depthDiff < 0` (foreground bleed), snap UV to texel centers, and multiply the offset by `saturate(depthDiff)` for shallow water.
  - Output via `o.Emission = ColorBelowWater * (1-alpha)` and a `finalcolor` alpha=1 function.
  - Requires MSAA off and a depth texture. The fake refraction doesn't work with that series' vertex-displaced Waves shader (no tangent normals).
  - No explicit license is stated on the page.
  — [Catlike Coding, Looking Through Water](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/)

### Inferences
- **Faceted waves + refraction**: since your waves are faceted (flat-shaded, derivative normals), use the world-space facet normal (`normalize(cross(ddy(worldPos), ddx(worldPos)))`) projected into view space XY as the UV offset. That works around Catlike's "no tangent normals" limitation and gives chunky, stylized refraction that matches low-poly.
- **GrabPass cost**: each GrabPass with a name grabs once per frame (shared), which is acceptable. An alternative is `CommandBuffer` with `CameraEvent.AfterSkybox`/`BeforeForwardAlpha` copying `BuiltinRenderTextureType.CurrentActive` into a half-res RT, which is cheaper on mobile.
- **Caustics** (cheap):
  1. In terrain and rock shaders (shared include), if `worldPos.y < _WaterLevel`, add `caustic = min(tex2D(_Caustics, wp.xz*s + t*v1), tex2D(_Caustics, wp.xz*s*1.1 - t*v2))` × `_LightColor0` × depth falloff × `saturate(N.y)`. The min() of two scrolling samples gives the classic web pattern. Add an RGB split by sampling R, G and B with tiny UV offsets for a stylized prismatic look.
  2. Or a projector/decal from the water plane. Option 1 is cheaper and more robust.
- **Reflections, cheapest first**:
  1. Fresnel lerp to the current `_SkyHorizon`/`_SkyZenith` globals, which follow the day/night cycle and cost nothing.
  2. A reflection probe (realtime, "via scripting", updated every few seconds or on time-of-day change, low resolution 64–128) sampled at `reflect(-V, N)`.
  3. Screen-space reflection inside the water shader via GrabPass + depth ray-march (8–16 steps), falling back to the sky. Medium cost.
  4. A planar reflection camera (MirrorReflection-style script) at half res with a culling mask excluding grass and particles. Most expensive, but the best for calm lakes.
  Stylized: posterize reflections and fresnel into 2–3 bands to match the toon look.
- **Shoreline foam (you already have it)**: add a sinusoidal scrolling band `step(frac(depthDiff*_FoamFreq - _Time.y*_FoamSpeed), _FoamWidth * (1-depthDiff))` for animated shore lines, plus a sparkle term with a smoothstep specular on the facet normal with the sun.

### Gaps
- No licensed free BiRP repo of stylized water with caustics was verified in this pass.

---

## 7. Game feel (hit stop, camera shake, slow motion for perfect dodge, squash, coyote time, input buffering)

### Takeaway
Use `Time.timeScale` for slow motion and short hit stops, but scale `Time.fixedDeltaTime` with it to keep physics smooth, and run UI, camera and input on unscaled time. Camera shake should use the "trauma" model (trauma² × Perlin noise, decaying), from Squirrel Eiserloh's GDC talk, which has MIT-ish free Unity implementations. Coyote time and input buffering are simple timers. No authoritative Unity-specific tutorial was found for them, so the code below is standard practice.

### Cited Findings
- `Time.timeScale` changes take effect on following frames. The number of FixedUpdate calls per frame depends on timeScale, so scale `fixedDeltaTime` accordingly. FixedUpdate doesn't run at timeScale 0. Official example: `Time.timeScale = 0.7f; Time.fixedDeltaTime = this.fixedDeltaTime * Time.timeScale;`. — [Unity 6 ScriptReference: Time.timeScale](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-timeScale.html)
- Trauma shake model: one shared trauma float in [0,1], add on hit, decay per frame, offset = trauma² × max offset/angle × Perlin noise, which gives smooth fading. Don't stack coroutines. — [bugnet.io guide](https://bugnet.io/blog/how-to-fix-unity-camera-shake-stacking-and-never-decaying); [Bevy screen-shake example citing Eiserloh's GDC talk](https://bevy.org/examples/camera/2d-screen-shake/)
- Unity implementations: `baratgabor/Unity3D-PerlinCameraShake` (single MonoBehaviour, trauma-based, credits the GDC talk). `gasgiant/Camera-Shake` (Perlin, Bounce and Kick presets). — [baratgabor repo](https://github.com/baratgabor/Unity3D-PerlinCameraShake); [gasgiant repo](https://github.com/gasgiant/Camera-Shake) (licenses not verified)
- Cinemachine note: per-camera Basic Multi-Channel Perlin noise resets on blends, so use Impulse instead. — [bugnet.io Cinemachine](https://bugnet.io/blog/fix-unity-cinemachine-shake-amplitude-resets-on-blend)

### Inferences (code patterns)
- **Hit stop** (80–150 ms on heavy hits, 30–60 ms on light hits):
  ```csharp
  public static class HitStop {
    static float baseFixed = 0.02f; static Coroutine co;
    public static void Do(MonoBehaviour host, float dur, float scale = 0f){ if(co!=null) host.StopCoroutine(co); co = host.StartCoroutine(Run(dur, scale)); }
    static IEnumerator Run(float d, float s){ Time.timeScale = s; Time.fixedDeltaTime = baseFixed * Mathf.Max(s, 0.01f); yield return new WaitForSecondsRealtime(d); Time.timeScale = 1; Time.fixedDeltaTime = baseFixed; co = null; }
  }
  ```
  Alternative without a global timeScale: freeze only the attacker's and target's `Animator.speed = 0` for the duration, so the world keeps moving. This feels better in a 3D open world. Combine it with a camera shake impulse and a white flash on the target (a `_HitFlash` material property via `MaterialPropertyBlock`).
- **Perfect dodge slow-mo (BotW "Flurry Rush" feel)**: on a dodge input within the window (e.g. 0.2 s before an enemy attack's active frames), lerp timeScale to 0.2 over 0.1 s (unscaled), hold 2–4 s real time or until the player attacks, then lerp back. The player's Animator stays at normal speed with `animator.updateMode = AnimatorUpdateMode.UnscaledTime`, or player speed is multiplied by `1/timeScale`. Add PPv2 volume weight for desaturation and vignette, plus a chromatic aberration pulse, and pitch audio down (`AudioMixer` snapshot).
- **Squash & stretch**: scale the visual child only (never the collider) with volume preservation, `s = new Vector3(1/Mathf.Sqrt(k), k, 1/Mathf.Sqrt(k))`. Drive k with a spring (`k += vel*dt; vel += (-(k-1)*stiff - vel*damp)*dt`) and kick it on landing (k=0.8) or jump (k=1.15). For skinned characters, apply it on the root bone or a parent transform.
- **Coyote time and jump buffer** (typical 0.1–0.15 s each):
  ```csharp
  float lastGrounded=-1, lastJumpPress=-1; const float coyote=0.12f, buffer=0.15f;
  void Update(){ if(grounded) lastGrounded=Time.time; if(jumpPressed) lastJumpPress=Time.time;
    if(Time.time-lastJumpPress<=buffer && Time.time-lastGrounded<=coyote){ Jump(); lastJumpPress=-1; lastGrounded=-1; } }
  ```
- **Combat input buffer**: a queue of `(action, time)`. When the current attack reaches its "cancel window" animation event, consume the most recent buffered action newer than 0.2–0.3 s. Use `Time.unscaledTime` if inputs must work during hit stop.
- **Camera**: use Cinemachine Impulse Source on hits, or a custom trauma shake applied as an additive local rotation on a child of the camera rig, so it doesn't fight the follow logic. Keep shake rotational (pitch/yaw/roll, 1–3°) in 3D. Positional shake feels cheap.

### Gaps
- No Unity-specific authoritative tutorial was found for hit stop, coyote time or input buffering. The Celeste-based analyses referenced by aggregators weren't verified. The code above is standard practice, not cited.
- No published frame timings for BotW's Flurry Rush or perfect dodge windows were found.
