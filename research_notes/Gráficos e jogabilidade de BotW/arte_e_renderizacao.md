# Art Direction and Rendering Techniques of The Legend of Zelda: Breath of the Wild (2017)

Sourcing legend used below:
- **[DEV]** = developer statement (GDC 2017 talk, CEDEC 2017 talks, *Creating a Champion* art book quotes).
- **[ANALYSIS]** = measurement by press analysts (Digital Foundry / Eurogamer).
- **[COMMUNITY]** = forum / fan / emulator / shader-recreation observation; not confirmed by Nintendo.

Fetch limits: zeldauniverse.net, gamedev.net, tweaktown.com and nintendowire.com returned HTTP 403, so the claims here from those sites come from search snippets only. Treat them as lower confidence.

## 1. Art direction: influences, goals, palette, developer statements

### Takeaway
Nintendo's own account is that BotW's look started from *The Wind Waker HD*. The team then made it less stylized in form and material so the physics/chemistry gameplay would read as believable. They called the result a "painterly" style that reconciles "reality" and "playability," under the internal slogan "Refreshing and full-flavored." I found no primary source for the often-repeated Ghibli, impressionist or gouache influences. The Jōmon influence is confirmed, but only for Sheikah technology.

### Cited Findings
- [DEV] At GDC 2017, art director Satoru Takizawa's team tested HD mockups in the styles of Skyward Sword, Twilight Princess and Wind Waker. Wind Waker "stood out," and Fujibayashi pointed at a Wind Waker screen as the reference. — [Zelda Universe (search snippet; page 403)](https://zeldauniverse.net/2017/03/14/takizawa-explains-how-the-wind-waker-hd-inspired-the-visual-style-for-breath-of-the-wild/); [GoNintendo](https://www.gonintendo.com/archives/256337-nintendo-says-wind-waker-hd-helped-inspired-the-visuals-for-zelda-breath-of-the); [Nintendo Everything](https://nintendoeverything.com/zelda-breath-of-the-wild-art-director-on-how-the-wind-waker-hd-shaped-the-games-art-style/)
- [DEV] Takizawa noted that Wind Waker's assets still looked fresh ten years later. He said its style allowed "a unique reality that was easy to lie within" while keeping playability. — [Thumbsticks GDC17 report](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/)
- [DEV] Why they moved away from pure Wind Waker: "The Wind Waker HD art style, in its depiction of form and feel of materials, is very stylised." Its "lies" were too big to suggest real-world physical/chemical interactions. The team also worried older players would read it as art for children. — [Thumbsticks](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/)
- [DEV] The design tension Takizawa described: moving away from realism improves tempo, visibility and pleasantness but loses the sense of a real world. Too much realism hurts the responsiveness of the game cycle. The art needed to be "information-dense," realistic enough to be believable, and still keep clarity and colour. "The artists had to continually research for an art style that makes it easy to lie." — [Thumbsticks](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/)
- [DEV] The art team's slogan was "Refreshing and full-flavored!" Takizawa summarised the style as "the reconciliation between the concepts of playability and reality," a style that "guarantees playability from all aspects, including visibility and action." — [Perfectly Nintendo GDC panel](https://www.perfectly-nintendo.com/zelda-breath-wild-details-pictures-gdc-2017-panel/); [Thumbsticks](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/)
- [DEV] Deliberate unrealism for readability: a chopped log instantly becomes tied firewood, and food bounces in the pot like a rubber ball with smoke puffs. Takizawa (*Creating a Champion*): "injecting humor into the visual shorthand helps players forgive" these departures. — [Thumbsticks](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/); [Nintendo Everything, Creating a Champion quotes](https://nintendoeverything.com/art-director-on-why-zeldas-art-style-constantly-changes-and-how-breath-of-the-wilds-look-was-made/)
- [DEV] *Creating a Champion* (Takizawa): "we created a painterly art style that combined the realism of the game world with its playability." The goal was a style that could "become the definitive version of The Legend of Zelda's art," and the results came from "trial and error." — [Nintendo Everything](https://nintendoeverything.com/art-director-on-why-zeldas-art-style-constantly-changes-and-how-breath-of-the-wilds-look-was-made/)
- [DEV] Takizawa said the general art design of Sheikah technology was inspired by the Jōmon period, "an era few are familiar with." No further detail was given. Claims that the shrines' curved lines and pottery-like patterns specifically come from Jōmon pottery appear only on low-quality sites. — [Game Informer](https://gameinformer.com/b/features/archive/2017/03/14/early-zelda-breath-of-the-wild-concepts-show-link-without-an-arm-the-presence-of-minish-and-more)
- [DEV] World scale was referenced to Kyoto. Fujibayashi tested sightlines such as Kyoto Gosho to Kyoto Tower. The world is said to be 12x Twilight Princess's Hyrule. — [Famitsu CEDEC 2017 level design](https://www.famitsu.com/news/201708/31140870.html); [Game Informer](https://gameinformer.com/b/features/archive/2017/03/14/early-zelda-breath-of-the-wild-concepts-show-link-without-an-arm-the-presence-of-minish-and-more)
- [DEV] Terrain lead artist Makoto Yonezu (CEDEC 2017) used a "triangle rule" for terrain shape language:
  - Large triangles act as landmarks; medium ones block views and shape routes; small ones prompt input.
  - Triangle tips guide the eye.
  - Squares hide things; trapezoids block views and invite climbing.
  - Shrines were made more visible in daylight, and glowing objects become the "attraction" (引力) at night.
  — [Famitsu](https://www.famitsu.com/news/201708/31140870.html)
- [DEV] Production: roughly 4 years and about 300 people. The world was built "skeleton"-first (a rough complete world, then assets replaced) in three loops: prototype about 1.5 years, mass production and polish about 1 year each. The in-house "Zelda Editor" auto-validated model and texture sizes. — [Famitsu CEDEC 2017, Okamura & Oyama](https://www.famitsu.com/news/201709/03141030.html)
- [DEV/award] BotW won the Visual Arts and Game Design categories at CEDEC Awards 2017. — [CEDEC press release PDF](https://2018.cedec.cesa.or.jp:443/2017/documents/press/cedec17_release_0831.pdf)

### Inferences
- For a Unity game, the actionable principle is "stylize the shading, not the readability of materials." Keep shapes simple and low-poly, but make sure wood, metal, stone, grass and water differ clearly in hue, value and specular response. That is what lets physics and chemistry interactions read.
- The triangle-based silhouettes are an art-direction tool as much as level design. Distant mountain silhouettes plus haze (section 4) make landmarks readable.

### Gaps
- No primary source found for Ghibli, impressionist or gouache painting as a stated influence. These are common press and fan descriptions; flag them as unconfirmed.
- No specific palette values or colour scripts were found. *Creating a Champion* likely contains concept art but no colour specs.
- No Nintendo Dream or Edge interview text was found within this research budget.
- Nintendo held 8 BotW sessions at CEDEC 2017, including "3D graphics," per [GameBusiness.jp](https://www.gamebusiness.jp/article/2017/07/04/13213.html). I could not locate a write-up of a dedicated rendering/graphics session, and Famitsu's index lists only level design, engineer/artist pipeline and UI.

## 2. Shading: toon/cel model, rim light, specular, deferred pipeline

### Takeaway
Nintendo has published nothing on BotW's shading internals. Community consensus, from dumps and observation, is a deferred (possibly clustered-deferred) renderer with PBR-style materials and Fresnel. On top of that sits a stylized light response: a two-band diffuse with a soft terminator, a strong rim/"contour" light when facing the sun, and simple specular. Every Unity recreation reproduces it as banded diffuse + rim + specular.

### Cited Findings
- [COMMUNITY] A ResetEra technical analysis (author's findings based on Switch footage and a text/symbol dump of the game) says the engine "combines the concepts of both clustered and deferred rendering" based on dump symbols. The author later states it "uses deferred rendering." — [ResetEra thread p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [COMMUNITY] Same source: "BOTW uses PBR, every material has Fresnel," applied "with a bit of artistic flair." Screen-space reflections are visible mainly on glossy shrine and Divine Beast walls. — [ResetEra p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2); [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Same source: real-time GI via light probes (guess: spherical harmonics), with probes streamed alongside LOD. SSAO is screen-space with a visible noise pattern in crevices. Subsurface scattering uses baked SSS masks (ice, lava, tents). Only global lights (sun, moon, lightning) cast shadows; emissive particles act as point lights. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Reflections are said to use three methods: specular for sun, sky and point lights; a low-rate (about 4–5 fps) captured reflection/environment view from a camera above Link; and SSR only on glossy shrine materials. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Erik Roystan Ross's toon shader tutorial uses BotW as its reference. It describes the look as two bands of lightness (lit and shadow) plus rim and specular, and suggests a ramp texture if more bands are needed. — [GameDev.net (search snippet; page 403)](https://gamedev.net/tutorials/programming/graphics/toon-shader-using-unity-r5095)
- [COMMUNITY] Ciro Continisio's CC0 URP Shader Graph recreation uses "2/3 bands of light and shadows, specular, and additional artistic light effects." It imitates BotW's contour lighting, where an object seen against the main light gets lit on its contours (backlit rim). The author calls it fan art that doesn't fully replicate the game. — [ccontinisio/zelda-botw-toon-shader](https://github.com/ccontinisio/zelda-botw-toon-shader); [ciro-unity/BotW-ToonShader](https://github.com/ciro-unity/BotW-ToonShader) (Unity 2021.2.5 / URP 12.1.2; no point/spot light or reflection probe support; Unite Copenhagen 2019 talk breaks it down)
- [COMMUNITY] Daniel Ilett's Shader Graph recreation (Unity 2020.3.2f1, URP 10.4.0) uses custom lighting with multi-light support, plus a YouTube tutorial. — [daniel-ilett/shaders-botw-cel-shading (mirror)](https://github.com/unitycoder/shaders-botw-cel-shading); [80.lv summary](https://80.lv/articles/tutorial-making-a-zelda-style-cel-shading-art-style-in-unity)
- [COMMUNITY] Asset-store BotW-style shaders expose Main Color, Ambient Color, Specular Color, Glossiness, Rim Color, Rim Amount and Rim Threshold. That is the de facto parameter set for this look. — [Unity Asset Store listing](https://assetstore.unity.com/packages/slug/187336)
- [COMMUNITY] The Cemu "No Cel-shade" graphic pack (Serfrost & Altros) removes the toon response. Users describe the result as "drab, depressing, and lifeless." That the stylization can be removed by patching shaders suggests it is a lighting-stage modification, not baked into textures. — [Nintendo Wire (search snippet; page 403)](https://nintendowire.com/news/2019/03/18/cemu-community-transforms-zelda-breath-of-the-wild-with-a-no-cel-shade-graphicpack/); [TweakTown (snippet)](https://www.tweaktown.com/news/65156/breath-wild-looks-dead-without-cel-shading/index.html)

### Inferences
- Unity recipe consistent with the above:
  - Diffuse: `NdotL` → `smoothstep(t - w, t + w)` with a small width `w` for the soft terminator, or a 2–3 step ramp texture.
  - Shadow band colour: tinted (cool or ambient-coloured), not black.
  - Ambient term: from SH or light probes.
  - Rim: `(1 - NdotV)` thresholded, masked by `NdotL` or boosted when backlit (view facing the sun) for the "contour light."
  - Specular: a small thresholded Blinn-Phong highlight.
  - Shadow receive: shadow maps multiplied into the band before thresholding.
- Because the community analysis says the base is PBR + Fresnel, keep per-material smoothness/metal maps feeding the banded model. That preserves the "material feel" Takizawa emphasised.
- Brush or hatch textures on terrain: BotW terrain reads painterly mainly through hand-painted, low-frequency colour variation in albedo, not screen-space hatching. This is an inference; no source documents a hatching pass.

### Gaps
- Character vs. environment shading differences are undocumented. Characters appear to have stronger rim light and smoother ramps, but I found no source for this.
- No published G-buffer layout or RenderDoc/Cemu frame-by-frame breakdown was found.
- No confirmed technique for the painterly or brush look on terrain.

## 3. Grass: geometry, wind, interaction, LOD

### Takeaway
No developer documentation exists. Community observation says near-field grass is individual-blade geometry at about one triangle per blade. Wind bends the tip vertex, and vertex colours likely drive a root-to-tip gradient. Grass is a mix of geometry and masked cards depending on LOD. A global wind simulation also drives clouds and physics.

### Cited Findings
- [COMMUNITY] Near the player each blade is "exactly 1 tri." Wind is done by pushing the top vertex in the wind direction, and vertex colours likely create the shading gradient. This is based on Switch footage and explicitly unconfirmed. — [ResetEra p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [COMMUNITY] The analysis author suspects a mix of alpha-masked textures and real polygons depending on LOD, based on the dump. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Wind is "completely random"/procedural per the dump. The wind simulation affects grass, clouds and physics objects by weight. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/), [p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [COMMUNITY] Industry article: BotW, Ghost of Tsushima and Genshin are cited as using per-blade instanced mesh grass. The common player-interaction method renders the player's feet from below into a render target that flattens grass. This is not confirmed as Nintendo's method. — [PixelAnt Games](https://pixelantgames.com/blog/rendering-millions-of-grass-blades-using-ue-and-niagara/)
- [COMMUNITY] A BotW-style grass recreation in Three.js uses:
  - A 5-vertex blade, with vertex colour black (base) → grey → white (tip) used as the wind weight.
  - Sine wind offset by UV position to mimic gusts.
  - Per-blade colour sampled from a splattered green texture via world-space UV, giving field-scale gradients.
  - A scrolling cloud-noise texture for moving cloud shadows on the field.
  — [Smyth Design](https://smythdesign.com/blog/stylized-grass-webgl/)
- [COMMUNITY] Fan shaders reproduce radial push-away from the player plus noise wind. — [Godot Shaders](https://godotshaders.com/shader/stylized-grass-with-wind-and-deformation/); [GameIdea](https://gameidea.org/2023/12/03/3d-grass-shader-with-distortion-wind/)

### Inferences
- Unity: GPU-instanced or compute-generated blades (3–5 verts) within a near radius, fading to a terrain colour/texture beyond it. Colour each blade from the terrain albedo at its root so grass blends into the ground at distance. That is likely why BotW's grass fields read as painted colour masses.
- Cutting and burning can be done with a per-chunk "grass state" mask texture (cut/burnt/alive) sampled in the grass shader. This is a design suggestion, not documented.

### Gaps
- No numbers found for grass density, draw radius or LOD distances.
- No primary source on how cutting, burning or regrowth is implemented.

## 4. Sky, atmosphere, distance, clouds, light shafts

### Takeaway
Per community dump analysis, the sky is not a skybox. It is computed from approximate Rayleigh/Mie scattering, with per-pixel sky irradiance driving sunset colour and star visibility. Clouds are procedural, wind-driven and shadow-casting, but not volumetric. Light shafts come from shadow volumes with noise-patterned inscatter fog. Fog colour is therefore tied to the scattering model and the time of day, which produces the blue mountain haze.

### Cited Findings
- [COMMUNITY] Rayleigh and Mie scattering are approximated mathematically; the analysis author says the text dump confirms this. Per-pixel sky irradiance drives sunset transitions and star visibility. "No skybox." — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Clouds are procedural, cast real-time shadows, are influenced by wind, and are not volumetric. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Every shadow caster has shadow volumes, which produce volumetric-looking light shafts. Fog inscatter uses a noise pattern in those volumes. A reply clarifies the fog is not truly volumetric. Sky occlusion follows Mie coefficients based on fog and cloud density. A "moisture" variable in the dump may modulate Mie scattering (speculation). — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/), [p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [COMMUNITY] Aperture-based lens flares appear even when the sun is just off-screen and scale with zoom. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Cemu graphic packs include "Remove Distant Fog" and "Volumetric dust and rays" options. Removing fog makes distant NPCs visible in heavy rain, which implies distance fog also masks LOD and pop-in. Shrines can be heavy on blue/bloom, and the same tweak affects overworld sky and water. — [Nexus Mods guide](https://nexusmods.com/legendofzeldabreathofthewild/mods/19)
- [DEV] Landmarks are designed to be revealed gradually by terrain occlusion. The CEDEC talk does not discuss fog. — [Famitsu](https://www.famitsu.com/news/201708/31140870.html)

### Inferences
- Unity: distance/height fog whose colour is sampled from the sky gradient in the view direction (a cheap aerial perspective). Blend toward a desaturated sky-blue with distance, and toward a warm sun-tinted colour near the sun direction (Mie lobe). Drive both from a time-of-day gradient. Also push distant terrain toward low contrast: lift the blacks toward the fog colour. That is the main source of the "painted" depth.
- A scrolling cloud-shadow projection on terrain and grass is cheap and a big part of the look.

### Gaps
- No numeric fog distances or colour curves found.
- No source on sun/moon rendering specifics.

## 5. Post-processing

### Takeaway
The community analysis lists SSAO (noisy), a screen-space DOF with bokeh/CoC emulation, lens flares and bloom. Digital Foundry noted anti-aliasing is weak to nonexistent. No Nintendo statement on colour grading exists.

### Cited Findings
- [COMMUNITY] DOF emulates bokeh and circle of confusion, likely as a screen-space post-process ("my guess"). SSAO shows visible noise. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Possible order-independent transparency rather than dithered transparency, with a visible cost in Korok Forest (unconfirmed). — [ResetEra p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [ANALYSIS] On the Switch 2 Edition, Digital Foundry says anti-aliasing "ranges from ineffective to virtually non-existent." Evidence suggests FSR1-style upscaling is still used. Nearby shadows have higher resolution but flicker. — [Nintendo Everything summary of DF](https://nintendoeverything.com/zelda-breath-of-the-wild-and-tears-of-the-kingdom-nintendo-switch-2-frame-rate-resolution/)
- [COMMUNITY] Bloom is strong in shrines (blue); see the Nexus guide above. — [Nexus Mods](https://nexusmods.com/legendofzeldabreathofthewild/mods/19)

### Inferences
- Unity post stack for the look: soft bloom with a moderate threshold, mild distance DOF, gentle saturation boost in mids, slight warm or cool split-toning by time of day, and SSAO at low intensity.

### Gaps
- No colour-grading/LUT information.
- No primary frame-capture (RenderDoc) breakdown found.

## 6. Water

### Takeaway
Only community observations exist. Reflections combine a low-rate captured reflection with specular. Foam is depth-based at the shoreline, and rain puddles are dynamic.

### Cited Findings
- [COMMUNITY] Shoreline foam depends on distance to ground, "probably" a z-buffer depth comparison. Rain puddles come from a water plane under the terrain that rises to fill basins and later evaporates. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] The water foam where grass meets water was praised. Shrine and tower glow reflects on water. — [ResetEra p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)

### Inferences
- Unity: depth-difference foam band plus a toon-stepped Fresnel to sky colour, a scrolling normal map, and planar or probe reflections. A puddle mask can rise with a "wetness" parameter.

### Gaps
- No details on river flow rendering, refraction or caustics.

## 7. LOD, pop-in, resolution and performance

### Takeaway
BotW targets 30 fps with dips, and drops directly to a 20 fps cadence under stress. Docked Switch renders 900p and Wii U/handheld 720p, with dynamic resolution scaling to about 90% per axis. LOD and probe data stream with the world partitioning.

### Cited Findings
- [ANALYSIS] Digital Foundry: Switch docked 900p native; Wii U and Switch handheld 720p. Visual differences are minor; shaders and lighting are nearly identical. — [Nintendo Wire](https://nintendowire.com/news/2017/03/04/breath-wild-wii-u-vs-switch-performance-comparison/); [Kotaku](https://kotaku.com/zelda-breath-of-the-wilds-switch-and-wii-u-versions-c-1792968383)
- [ANALYSIS] Dynamic resolution: drops to 90% on both axes under load. Docked goes 1600x900 → 1440x810; handheld goes to 1152x648 (81% of 720p pixels). — [Nintendo Everything / Eurogamer](https://nintendoeverything.com/zelda-breath-of-the-wild-uses-dynamic-resolution-scaling/); [Nintenderos](https://www.nintenderos.com/2017/03/the-legend-of-zelda-breath-of-the-wild-usa-el-escalado-de-resolucion-dinamica-para-mantener-el-rendimiento/)
- [ANALYSIS] When it misses 30 fps, the game locks to 20 fps. DF suggested a "hidden bottleneck" tied to world partitioning. — [Nintendo Wire](https://nintendowire.com/news/2017/03/04/breath-wild-wii-u-vs-switch-performance-comparison/)
- [ANALYSIS] Switch 2 Edition: 1440p (docked), likely FSR1. — [Nintendo Everything](https://nintendoeverything.com/zelda-breath-of-the-wild-and-tears-of-the-kingdom-nintendo-switch-2-frame-rate-resolution/)
- [COMMUNITY] Light-probe data appears to stream with LOD. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Hardware context: the game was built for Wii U (roughly SM4-level GPU) and ported. — [ResetEra p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)

### Inferences
- The look is achievable on modest hardware. A stylized low-poly Unity project can borrow the same tricks: heavy distance fog to hide LOD, cheap per-blade grass in a short radius, and low-frequency far-terrain textures.

### Gaps
- No numeric draw distances or LOD ring sizes found.

## 8. Time of day and weather

### Takeaway
Time of day is driven by the scattering sky and per-pixel sky irradiance. Lightning is a localized light source with its own shadows. Rain fills dynamic puddles. Developer statements tie the time of day to gameplay visibility: glowing objects attract players at night.

### Cited Findings
- [COMMUNITY] Per-pixel sky irradiance drives sunset transitions and star visibility. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Lightning: localized, radius-based strikes anywhere on the map, with shadows matching the nearest strike. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/)
- [COMMUNITY] Rain creates puddles via a rising sub-terrain water plane. A "moisture" variable may affect Mie scattering, i.e. haze in rain. — [ResetEra p.1](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/), [p.2](https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2)
- [DEV] "Attraction" changes with time of day because glowing objects (shrines, towers) stand out at night. Shrines were tuned to be more visible by day. — [Famitsu](https://www.famitsu.com/news/201708/31140870.html)
- [COMMUNITY] In heavy rain, fog strongly reduces visibility (see the Cemu defog note). — [Nexus Mods](https://nexusmods.com/legendofzeldabreathofthewild/mods/19)

### Inferences
- Unity: one time-of-day controller with gradients for sun colour, ambient (shadow band) colour, fog colour/density and sky top/horizon colour. A weather layer multiplies in wetness (darken albedo, raise smoothness), fog density and cloud cover, plus lightning flashes as a directional light pulse.

### Gaps
- No source on wet-surface shading specifics (albedo darkening amount, specular change).
- No source on snow accumulation or other weather visuals.
