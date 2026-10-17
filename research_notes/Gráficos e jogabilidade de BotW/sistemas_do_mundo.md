# Sistemas do mundo de BotW: motor químico, clima, temperatura, ciclo dia-noite

Confidence key used throughout:
- **[DEV]**: developer-confirmed (GDC 2017 talk coverage, Nintendo statements).
- **[DATA]**: datamined, from the zeldaret/botw decompilation source or ZeldaMods wiki file-format docs.
- **[COMM]**: community observation, from fan wikis and guides.

Access note: zelda.fandom.com returned HTTP 402 and zeldawiki.wiki returned 403 to direct fetches. Claims from those wikis therefore come from search-result snippets, and the snippets were not checked against the full pages. The decomp files were read directly from raw.githubusercontent.com.

---

## Chemistry engine: rules, interactions, fire spread, physics coupling, multiplicative gameplay

### Takeaway
At GDC 2017 Takuhiro Dohta described two separate engines. Havok physics handles collision and movement. A custom "chemistry engine" handles state changes, following rules about **elements** (fire, water, ice, electricity, wind) and **materials** (trees, rocks, the player, weapons). The datamined chemistry config shows a semi-physical model: per-material heat capacity, thermal conductivity, ignition point, electrical resistivity and wetting, plus global heat-transfer, burn-speed and discharge-radius multipliers. That set is a direct blueprint for a Unity equivalent.

### Cited Findings
**Developer statements (GDC 2017, "Change and Constant: Breaking Conventions with The Legend of Zelda: Breath of the Wild")**
- [DEV] The speaker was technical director Takuhiro Dohta, in a 90-minute session shared with director Hidemaro Fujibayashi. Most action games have three components: collision, movement, and object states. "Physics" usually means collision plus movement — [Thumbsticks, GDC 17 part 2](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)
- [DEV] Dohta called game physics "false physics or liars physics", tuned for control and responsiveness rather than realism. Havok was chosen for stability, robustness and track record. It powers Stasis, Magnesis and the many physical objects, and a rock can be rolled from the game's start to the final boss — [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)
- [DEV] A physics engine calculates movement by rules, and the chemistry engine calculates state changes by rules. **Elements** have no fixed state (fire, water, ice, electricity, wind). **Materials** are solid objects (trees, rocks, the player) — [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)
- [DEV] The three rules — [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies); [GamesBeat](https://gamesbeat.com/the-legend-of-zelda-breath-of-the-wild-makes-chemistry-just-as-important-as-physics/):
  1. Elements can change the state of materials. Example: fire burns the leaves off a tree.
  2. Elements can change each other's states. Example: water extinguishes fire.
  3. Materials cannot change each other's states.
  - Caveat: per the GamesBeat search snippet, its example for rule 3 is "a burning tree can't ignite a second tree" directly. In practice, fire spreads because a burning material emits the fire *element*, and that element ignites the next material. Rule 3 means material-to-material contact alone never changes state. GamesBeat's wording also says "physics engine" for these rules, which is likely an editing slip.
- [DEV] Elements can also generate energy that affects the physical world. Example: fanning a leaf makes wind that pushes a raft. Dohta: "Aside from rigid body dynamics we consider all phenomena that occur in nature to be chemistry." — [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)
- [DEV] On consistent rules, Dohta said "if you unify your game world with a set of consistent physics rules, the player will come up with ideas all sorts of things they want to try"; puzzles that reference real-world behavior can be solved "instinctively" — [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)
- [DEV] Coverage also notes the two engines working together, for example a thrown flaming torch (physics) igniting grass (chemistry). Another summary says the engine tracks every object's state against five elements: fire, water, ice, wind and electricity — [GamesBeat](https://gamesbeat.com/the-legend-of-zelda-breath-of-the-wild-makes-chemistry-just-as-important-as-physics/) (via search summary; direct fetch returned 403)

**Multiplicative gameplay [DEV]**
- Fujibayashi proposed mechanics that "scale and multiply" consistently across the world, so puzzle-like situations are mass-produced rather than hand-built, with objects reacting to the player and to each other (an "active game"). Quote: "I wondered if I could use this idea of multiplication and apply it to the mass production of these puzzles." — [Thumbsticks, GDC 17 part 1](https://www.thumbsticks.com/gdc-17-breaking-conventions-breath-of-the-wild)
- The prototype was a 2D NES-Zelda look-alike running on a 3D engine. It had only a river and trees, no puzzles, and one rule: reach the goal. Example: fell a tree and use the trunk to cross the river. Making walls climbable removed impassable barriers, and the glider was added to solve descent, which Fujibayashi called the key to expanding freedom — [Thumbsticks part 1](https://www.thumbsticks.com/gdc-17-breaking-conventions-breath-of-the-wild)
- Other summaries of the talk cite examples such as firing an arrow through fire to ignite a forest, and describe the approach as "a toolbox to apply to the whole world" — [Tom's Guide](https://www.tomsguide.com/uk/us/zelda-breath-wild-design,news-24586.html); [Game Developer](https://gamedeveloper.com/design/forging-i-zelda-i-s-future-by-revisiting-its-past) (via search summary)

**Datamined chemistry config (`chmres` / `ksys::chm::SystemConfig`) [DATA]**
Source: [zeldaret/botw chmSystemConfigObjs.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfigObjs.cpp) and [chmSystemConfig.h](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfig.h). The values are **constructor defaults in code**. Shipped values in the game's resource file may differ, and I did not read that file. Japanese labels are translated.
- **Element** params: `life_frames` (lifetime, range 0–300 frames), `temperature`, `type`, `wetting_rate`, `wind_speed`, `electric_charge`, `shock_wave_speed` (explosions), `fire_level` (default 2), `moisture`, `effect_type`, an attribute bitflag.
- **Material** params, each with a real SI unit label:
  - `heat_capacity` [J/gK]: default 1, range 0–10.
  - `thermal_conductivity` [W/m·K]: default 1, range 0–100.
  - `electrical_resistivity` [Ω·m]: default 1, range −10 to 10. Likely log-scale given the negative range, which is an inference.
  - `electrical_capacitance` [F]: range 0–1.
  - `electromotive_force` [V]: range 0–10.
  - `fire_proof_level`: Lv 0–5.
  - `ignition_point`: default **100°**, range 0–1000.
  - `burn_speed`: default 1.0, range 0–1.
  - `wetting_amount` [kg/m²]: range 0–100.
  - `wind_force_remain_rate`: range 0–1.
  - Per-material response-element slots for flame, warm air, explosion (two slots), electrification, ice magic and water. These define which element each material emits or turns into when hit.
- **World (global) params**, with defaults:
  - `evaporation_speed_rate`: 24.0 (evaporation coefficient vs temperature).
  - `heat_transfer_speed_rate`: 24.0 overall, and `fire_heat_transfer_speed_rate`: 2.0 for fire.
  - `conduction_speed_rate_log`: −5.0 (electric conduction speed).
  - `burn_speed_rate`: 0.1.
  - `fire_extra_radius`: 0.3 m (fire hits things up to this far away).
  - `player_fire_margin`: 0.3 m (safe distance from direct flame).
  - `electricity_extra_radius_rate`: 50.0 (discharge distance multiplier), and `electric_water_extra_radius_rate`: **500.0** for leakage through water.
  - `min_ice_size`: 0.15 m.
  - Ice melt rates: vs ambient temperature 0.05, vs warm air 0.5, vs fire 1.0, vs flame 10.0. There is an "ice melts faster" cheat above a 10,000 kg threshold at rate 0.5.
  - `wind_lift_rate`: 0.1 (lift/drag ratio, relevant to updrafts and gliding).
  - `lightning_distance`: **25 m** (lightning range).
  - `character_ignition_count` and `object_ignition_count`: **90 frames** (ignition time, 3 s at 30 fps).
  - `offensive_rigid_radius`: 1.0 (chemical size of an attacking state).
- [DATA] The decomp also contains many chemistry-aware AI classes, such as `aiChemicalWeaponRoot`, `aiChemicalExplode`, `aiChemicalStayObjectRoot`, `aiEnemyChemicalSelect` and `aiCapturedActorReactionChemical`. These show that actors subscribe to chemistry state changes through AI nodes — [zeldaret/botw repo tree](https://github.com/zeldaret/botw/tree/master/src/Game/Actor/AI)

**Interaction list (mostly [COMM])**
- Fire on grass produces an updraft the paraglider can ride. The in-game tip says spreading fire creates an updraft — [Zelda Wiki: Gust Geyser (snippet)](https://zeldawiki.wiki/wiki/Gust_Geyser); [Twinfinite updraft guide](https://twinfinite.net/guides/zelda-breath-of-the-wild-updrafts-how-create/); [GameWith](https://gamewith.net/zelda-botw/article/show/38837)
- Metal conducts electricity. Metal shields and weapons make Link drop them when he is shocked, and Shock Arrows are "incredibly effective against metal or anything wet." Shocks cause equipped items to drop. Metal weapons sink in water but can be retrieved with Magnesis — [Zelda Wiki: Shields in BotW](https://zeldawiki.wiki/wiki/Shields_in_Breath_of_the_Wild); [Zelda Wiki: Shock Arrow](https://zeldawiki.wiki/wiki/Shock_Arrow); [Zelda Wiki: Great Thunderblade](https://www.zeldawiki.wiki/wiki/Great_Thunderblade) (snippets)
- An NPC (Daz) says a shock arrow in the rain does extra damage, and Zora are vulnerable to electricity — [Zelda Wiki: Shock Arrow (snippet)](https://zeldawiki.wiki/wiki/Shock_Arrow)
- Wooden items burn when Link catches fire in volcanic areas, even with a Fireproof Elixir — [Shacknews](https://www.shacknews.com/article/99235/how-to-cool-down-breath-of-the-wild)

### Inferences
- A Unity design implied by the datamined params:
  - Each `ChemObject` carries a temperature and a wetness value and runs a heat-exchange step with nearby element emitters. The step is scaled by `thermal_conductivity / heat_capacity` and the global rates.
  - When temperature crosses `ignition_point`, the object starts a ~90-frame ignition timer, then becomes a fire emitter. Burnable materials then burn down at `burn_speed × burn_speed_rate`.
  - Wetness and water suppress fire. Ice melts at per-source ratios.
  - Electricity travels through low-resistivity materials, with its radius multiplied by about 10× more through water (500 vs 50).
  - This keeps rule 3, because materials never act on each other directly, only through emitted elements.
- Wind-direction fire spread is not stated by developers in what I found. With a `wind_speed` field on elements and `wind_force_remain_rate` on materials, a plausible model biases fire-element emission downwind.
- Use Unity physics (PhysX) for rigid bodies, and run a separate tick-based chemistry layer, for example on a coarse spatial grid or with trigger volumes. That mirrors the Havok/chemistry split.

### Gaps
- I found no developer-sourced numbers for fire spread rate, grass burn duration, updraft strength or height, or the wind effect on fire direction. Mark Brown's Game Maker's Toolkit and the GDC Vault video weren't transcribed here.
- The shipped `Chemical/system.chmres` values, and the per-material table (wood, metal, grass, ice and so on), were not retrieved. Only code defaults are listed.
- Explosion interactions (bomb shockwave on wood and ore, fire plus explosive barrel) are not specifically sourced.

---

## Weather: rain, thunderstorms, snow, wind, forecast, change frequency, regional weather

### Takeaway
Weather is rolled per **climate**, and each map area is assigned one of 20 climates. The roll is a percentage pick among bluesky, cloudy, rain, heavy rain and thunderstorm, using per-climate rates. Community sources say an area's weather re-rolls every 4 in-game hours. Snow is rain in cold climates. Metal gear attracts lightning within about 25 m.

### Cited Findings
- [DATA] `WeatherMgr::rollNewWeather(Climate)` draws a random number 0–99. It then walks cumulative rates (BlueSky−1, Cloudy, Rain, HeavyRain), and the remainder becomes ThunderRain. Two overrides follow. If the Water Relic (Divine Beast Vah Ruta) rain-stop condition is on for that climate, the result is forced to BlueSky. Before the plateau or paraglider flag is set, only BlueSky or Cloudy are allowed, and anything else becomes BlueSky — [worldWeatherMgr.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldWeatherMgr.cpp)
- [DATA] The header defines `NumWeatherCycles = 3` per climate, a stored array of upcoming weathers per climate, `mTimeBlock` and `mWeekDay`, plus `rerollClimateWindPowers()` — [worldWeatherMgr.h](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldWeatherMgr.h)
- [DATA] Climate data lives in `normal.bwinfo` (AAMP). Version 1.5.0 has 20 `ClimateDefines`, and AreaData.byml maps areas to climates. Example rates are blue sky 60, cloudy 20, rain 10, heavy rain 5, storm 5. `DayLockBlueSky` and `NightLockBlueSky` force clear skies. `WindPower` (example 7.5) affects paragliding, arrows, remote bombs and thrown objects. `WeatherType` is 0 sunny, 1 cloudy, 2 rain, 3 snow — [ZeldaMods: Bwinfo](https://zeldamods.org/wiki/Bwinfo)
- [COMM] Rain becomes snow in cold areas. Storm rate controls thunderstorms. Weather in an area re-rolls every **four in-game hours** — [Zelda Wiki: Weather (snippet)](https://zeldawiki.wiki/wiki/Weather)
- [COMM] Thunderstorms occur in Akkala, Central Hyrule, Faron, Lanayru and Necluda — [Zelda Wiki: Thunderstorm (snippet)](https://zeldawiki.wiki/wiki/Thunderstorm)
- [COMM] In a thunderstorm, equipped metal weapons, bows and shields begin to **spark** as a warning, and Link can then be struck. Metal blocks also draw lightning, and a horse being ridden can be struck. Thunder Helm or Rubber gear nullifies the danger. The NPC Endai warns against metal gear in rain, and throwing a metal weapon at enemies redirects lightning to them — [Zelda Wiki: Thunderstorm](https://zeldawiki.wiki/wiki/Thunderstorm); [Zelda Wiki: Endai](https://zeldawiki.wiki/wiki/Endai); [GameWith](https://gamewith.net/zelda-botw/article/show/38818) (snippets)
- [DATA] Lightning range is a global chemistry param, `lightning_distance` = 25 m (code default) — [chmSystemConfigObjs.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfigObjs.cpp)
- [COMM] Link slips while climbing in rain, wasting stamina — [Zelda Wiki: Weather/Rain (snippet)](https://zelda.fandom.com/wiki/Weather)

### Inferences
- `NumWeatherCycles = 3` plus a time block suggests the game pre-rolls the current and next weather slots per climate. That would let the Sheikah Slate show a forecast without randomness, which is my inference. A forecast in Unity can likewise pre-roll N slots of 4 hours each per region.
- Fire being extinguished and campfires failing in rain follows from the chemistry model (`wetting_rate` on the rain element, `moisture`). In Unity, apply a global wetness element to exposed objects during rain.

### Gaps
- Rain masking footstep sound (stealth) is widely reported by players. I found no source in these searches, so it is unverified here.
- Per-region rate tables, the full set of 20 climates, and wind direction change timing (only `rerollClimateWindPowers` exists) were not retrieved.
- Lightning strike frequency and warning time before a strike were not found. The forecast UI (Sheikah Sensor/HUD weather icons, unlocked after Vah Naboris? unverified) was not sourced.

---

## Temperature: zones, gauge, damage, mitigation, burning zones

### Takeaway
Ambient temperature depends on the climate's height-keyed table, with separate day and night values. Damage starts below 0°C and above 40°C, and worsens below −10°C and above 50°C. Separately, an `IgnitedLevel` climate flag makes Link and flammable items burst into flame on Death Mountain unless fireproof.

### Cited Findings
- [DATA] Each climate defines temperature by height (Y). Example (Day/Night °C):
  - Y ≥ 900: −28/−28
  - 800–899: −20/−23
  - 700–799: −10/−14
  - 600–699: −5/−5
  - 500–599: 3/−2
  - 400–499: 9/6
  - 300–399: 15/13
  - 200–299: 18/16
  - 100–199: 20/18
  - 0–99: 25/23
  - Source: [ZeldaMods: Bwinfo](https://zeldamods.org/wiki/Bwinfo)
- [DATA] `IgnitedLevel` is a per-climate setting. Level 1 causes "burst into flame" as on Death Mountain. Level 2 kills monsters and destroys wooden and food items, including weapons — [ZeldaMods: Bwinfo](https://zeldamods.org/wiki/Bwinfo)
- [DATA] Materials have `fire_proof_level` 0–5, and the world has `player_fire_margin` 0.3 m — [chmSystemConfigObjs.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfigObjs.cpp)
- [DATA] A `TemperatureRandSelect` AI node exists, documented on ZeldaMods — [ZeldaMods: AIDef TemperatureRandSelect](https://zeldamods.org/wiki/AIDef:AI/TemperatureRandSelect) (not fetched)
- [COMM] Cold damage is slow below 32°F (0°C) and faster below 14°F (−10°C). Heat damage starts above 104°F (40°C) and worsens above 122°F (50°C). Certain freezing waters damage Link with no protection possible — [Zelda Wiki: Temperature (snippets)](https://zeldawiki.wiki/wiki/Temperature); [Zelda Wiki: Heat](https://zeldawiki.wiki/wiki/Heat)
- [COMM] Mitigation includes:
  - moving to a milder area
  - shade, or entering water in hot areas
  - elemental weapons (flame weapons warm, frost weapons cool)
  - armor (Snowquill, Warm Doublet)
  - Spicy and Chilly food or elixirs
  - Gerudo Town and Kara Kara Bazaar giving constant heat protection
  - Source: [Zelda Wiki: Temperature](https://zeldawiki.wiki/wiki/Temperature); [Freezing Climate](https://zeldawiki.wiki/wiki/Freezing_Climate)
- [COMM] Lava areas push temperatures so high that the Sheikah Slate gauge shows "ERROR" — [Zelda Wiki: Extreme Heat (snippet)](https://zeldawiki.wiki/wiki/Extreme_Heat)
- [COMM] Flame Guard from Flamebreaker armor prevents spontaneous combustion in volcanic areas. It does not protect against direct fire or lava. Per one wiki, actual "Fireproof" requires the set upgraded to Lv 2+, which is unconfirmed elsewhere. Unequip wooden items, because they burn even with a Fireproof Elixir. Death Mountain signage uses star levels — [Zelda Wiki: Flamebreaker Set](https://zeldawiki.wiki/wiki/Flamebreaker_Set); [Fireproof Armor](https://zeldawiki.wiki/wiki/Fireproof_Armor); [Shacknews](https://www.shacknews.com/article/99235/how-to-cool-down-breath-of-the-wild)

### Inferences
- A Unity approach:
  - Ambient = climate curve(height, dayOrNight) + local modifiers (shade, water, warm-air emitters such as campfires and torches, which the chemistry config's "warm air" element supports).
  - Resistance levels shift the safe band; for example, cold resistance Lv1 lowers the threshold by one band.
  - Damage ticks per band.
  - Height-keyed tables are cheap and readable to designers.

### Gaps
- Exact damage per tick for cold and heat, how resistance levels shift thresholds, and food or elixir durations were not found in fetchable sources.
- The amount of warmth a torch gives was also not sourced.

---

## Day-night cycle: length, time divisions, Blood Moon, night gameplay

### Takeaway
Datamined code puts 1 in-game minute at 1 real second, so a full day is about 24 real minutes. The game splits the day into 8 named divisions and treats 06:00–18:00 as daytime. Blood Moons are scheduled when a timer exceeds 7 in-game days and occur at midnight.

### Cited Findings
- [DATA] Time is a float 0–360, 15 units per hour (`timeToFloat(h,m) = h*15 + m*0.25`). The new-game start is 05:15 (78.75). `DefaultTimeStep = durationToFloat(0,1)/30`, meaning one in-game minute per 30 frames — [worldTimeMgr.h](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldTimeMgr.h); [ZeldaMods: Time](https://zeldamods.org/wiki/Time)
- [DATA] 1 in-game minute = 1 real second (ZeldaMods), so 1,440 in-game minutes is about 24 real minutes (derived). Time flows normally until 11:00 on the Plateau, and activating the Great Plateau Tower sets it to 11:00. Sleeping or a campfire jumps instantly to the target time, and the game never speeds up or slows down time — [ZeldaMods: Time](https://zeldamods.org/wiki/Time)
- [DATA] Daytime is 06:00 < t < 18:00, setting the `WM_DaytimeFlag` and `WM_NighttimeFlag` game flags. Time divisions:
  - Morning_A 04–07 (A1 04–05, A2 05–07)
  - Morning_B 07–10
  - Noon_A 10–13
  - Noon_B 13–17
  - Evening_A 17–19
  - Evening_B 19–21
  - Night_A 21–24
  - Night_B 00–04
  - Each division has a game flag that scripts and spawns can query.
  - Source: [worldTimeMgr.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldTimeMgr.cpp)
- [DATA] The Blood Moon timer runs at clock rate. When it exceeds 2520.0 (7 days), the next midnight sets `WM_BloodyDay`, and that midnight is a Blood Moon. The timer resets to 0. If the Blood Moon is prohibited, for example by an event, the timer is set to 2880 (8 days). The flag clears 150 frames (5 in-game minutes) later. This applies only after leaving the Plateau. Sleeping does not hasten it, since the timer does not advance in cutscenes — [ZeldaMods: Time](https://zeldamods.org/wiki/Time); [worldTimeMgr.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldTimeMgr.cpp)
- [DATA] Moon phase = (days + x + 1) % 8 (x=1 after noon), giving an 8-phase cycle where 0 is the full or blood moon — [ZeldaMods: Time](https://zeldamods.org/wiki/Time)
- [DATA] TimeMgr also has a random hourly appearance roll (`getU32(23)`) gated by area checks. This is likely a spawn of a time-of-day special actor, but the exact actor isn't confirmed — [worldTimeMgr.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/World/worldTimeMgr.cpp)
- [COMM] The Blood Moon revives defeated enemies (described on the Blood Moon wiki page) — [Zelda Wiki: Blood Moon](https://zeldawiki.wiki/wiki/Blood_Moon) (snippet; detail not verified)

### Inferences
- In Unity, store time as a float with 1 unit = 1 in-game minute, advancing at 1 per real second by default. Expose division enums and events (OnDivisionChanged, OnMidnight) so spawners (Stalfos at night), NPC schedules and temperature (day/night curve) can subscribe. Implement the Blood Moon as a 7-day counter that ignores skipped time.

### Gaps
- Night-only enemy spawning (Stal-monsters), NPC schedules and night-specific items were not covered by fetched sources.
- Whether the Blood Moon also has a "too many objects loaded" emergency trigger (a known community claim) was not verified. The code shows multiple force modes (`BloodMoonForceMode`) whose exact triggers I didn't read.

---

## Electricity: conductivity, water, shock effects

### Takeaway
Electricity is a chemistry element. Its spread depends on material resistivity, and it reaches about 10× further through water (radius rates 50 vs 500). Gameplay-wise, metal conducts, wet targets take extra effect, and being shocked makes Link drop what he holds.

### Cited Findings
- [DATA] Materials have `electrical_resistivity`, `electrical_capacitance` and `electromotive_force`. The global `conduction_speed_rate_log` is −5, `electricity_extra_radius_rate` is 50, and `electric_water_extra_radius_rate` is 500 for leakage via water — [chmSystemConfigObjs.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfigObjs.cpp)
- [COMM] Shock drops equipped items. Metal shields and weapons conduct and attract lightning. Shock arrows work well on metal and wet targets, with extra damage in rain — [Zelda Wiki: Shields in BotW](https://zeldawiki.wiki/wiki/Shields_in_Breath_of_the_Wild); [Zelda Wiki: Shock Arrow](https://zeldawiki.wiki/wiki/Shock_Arrow) (snippets)
- [COMM] Shock resistance comes from the Rubber set and Thunder Helm — [GameWith](https://gamewith.net/zelda-botw/article/show/38818) (snippet)

### Inferences
- In Unity, electricity can propagate as a flood-fill over a contact graph of conductive colliders (metal, water volumes), with a radius bonus in water. On hitting a character, apply stun plus a forced weapon or shield drop.

### Gaps
- Shock damage numbers, stun duration and whether water conducts across a whole water body were not found.

---

## Specific numbers summary (quick reference)

### Takeaway
The most reliable hard numbers come from the decomp and the ZeldaMods docs. Damage and duration values remain unsourced.

### Cited Findings
- Time: 1 in-game min = 1 real s, so a day is about 24 real min. Daytime 06–18. Blood Moon every more than 7 in-game days (2520 units), or 8 if prohibited — [ZeldaMods: Time](https://zeldamods.org/wiki/Time)
- Weather: per-climate % rates, for example 60/20/10/5/5. Rolled every 4 in-game hours ([COMM]). 20 climates — [ZeldaMods: Bwinfo](https://zeldamods.org/wiki/Bwinfo); [Zelda Wiki: Weather](https://zeldawiki.wiki/wiki/Weather)
- Temperature: height-keyed, roughly 25°C at sea level to −28°C at high altitude. Damage thresholds at 0/−10°C and 40/50°C — [ZeldaMods: Bwinfo](https://zeldamods.org/wiki/Bwinfo); [Zelda Wiki: Temperature](https://zeldawiki.wiki/wiki/Temperature)
- Chemistry code defaults: ignition time 90 frames, ignition point 100, lightning range 25 m, fire reach 0.3 m, electric radius rate 50 (500 in water), min ice size 0.15 m — [chmSystemConfigObjs.cpp](https://github.com/zeldaret/botw/blob/master/src/KingSystem/Chemical/chmSystemConfigObjs.cpp)

### Inferences
- The engine runs at 30 fps, implied by "1 min / 30 frames" equalling 1 real second, so frame-count params convert at 30 frames = 1 s.

### Gaps
- Not found: fire spread rate, grass burn duration, updraft force, temperature damage per tick, shock damage, and food or elixir durations.
