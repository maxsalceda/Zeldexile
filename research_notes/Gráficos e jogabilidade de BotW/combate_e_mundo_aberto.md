# BotW: Combat, Cooking, Shrines and Open-World Level Design (for a Unity ARPG prototype)

Source confidence key: **[DEV]** = Nintendo developer statement or talk (GDC/CEDEC/interview); **[DATA]** = datamined game parameters (ZeldaMods, wiki templates built from game files); **[COMM]** = community wiki, guide or player observation; **[ANALYSIS]** = third-party design analysis.

Access note: zeldawiki.wiki, zelda.fandom.com, nintendolife and zeldauniverse returned 403/402 during this research, and breezewiki mirrors were bot-blocked. Several frame-exact numbers could not be verified, and the Gaps sections say which ones.

## 1. Combat: Flurry Rush, Perfect Guard, throwing, charged attacks, sneak strikes, headshots, aerial bow slow motion, enemy AI, durability

### Takeaway
BotW combat is built from a few readable, timing-based rewards: Perfect Dodge leads to Flurry Rush, Perfect Guard parries, sneakstrikes do 8x damage and headshots about 2x. These sit on top of a physics/chemistry sandbox, where enemies pick up weapons, use fire and avoid explosives. Developers say weapon durability exists to push players into encounters and make them vary their tactics. I found no reliable frame-exact timing windows, so the prototype will need to tune its own.

### Cited Findings
**Flurry Rush and Perfect Dodge [COMM]**
- A Flurry Rush is triggered by a successful Perfect Dodge, where Link jumps out of an enemy attack at the last moment. Time slows, and if Link does not connect a hit the rush cancels and time returns to normal. — [Zelda Wiki: Flurry Rush](https://zeldawiki.wiki/wiki/Flurry_Rush); [Zelda Wiki: Perfect Dodge](https://zeldawiki.wiki/wiki/Perfect_Dodge); [Zelda Archive (Fandom)](https://zelda-archive.fandom.com/wiki/Flurry_Rush)
- Input is lock-on (ZL) plus jump (X) plus a direction. The dodge direction depends on the attack: dodge horizontally (side-hop) against vertical attacks and vertically (backflip) against horizontal attacks. Perfect Dodge "will only activate on melee attacks", so ranged enemies cannot trigger it. — [Game8](https://game8.co/games/LoZ-BotW/archives/292529)
- Guides describe the invincibility window as "incredibly tight". Link is not invincible during the Flurry Rush, and taking damage ends it. — [GamerTagMythras combat tips](https://gamertagmythras.com/blog/zelda-breath-of-the-wild/botw-combat-tips)
- No source I could reach gives the dodge window in frames or the slow-motion length in seconds.

**Perfect Guard (parry) [COMM]**
- Press A with any shield "the moment before" an attack lands. This staggers the enemy, can disarm it, and deflects certain projectiles such as Guardian lasers back at the source. — [Zelda Wiki: Perfect Guard](https://zeldawiki.wiki/wiki/Perfect_Guard)
- Almost every attack type can be parried. — [Breezewiki/Zelda Fandom: Parry](https://breezewiki.discard.no/zelda/wiki/Parry) (search snippet only; the page was bot-blocked)
- Frame-window search results (for example "9.3 frames") came from *Lies of P* and *Stellar Blade* mods, not BotW. Do not use them.

**Damage multipliers [COMM]/[DATA]**
- A sneakstrike (hitting an unaware enemy from behind) multiplies damage by **8**. — [Zelda Wiki: Weapon Strength (revision)](https://zeldawiki.wiki/wiki/Special:MobileDiff/1140917)
- Arrow headshots increase damage. Datamined enemy parameters set `WeakPointBone: Head` with `WeakPointRatio: 2.0`, which implies about **2x** on the head. — [Zelda Wiki: Weapon Strength](https://zeldawiki.wiki/wiki/Special:MobileDiff/1140917); [Template: BotW Enemy Damage Parameters](https://www.zeldawiki.wiki/wiki/Template:BotW_Enemy_Damage_Parameters) [DATA]
- Multipliers of different types stack. — [Zelda Wiki: Weapon Strength](https://zeldawiki.wiki/wiki/Special:MobileDiff/1140917)

**Aerial bow slow motion ("bullet time") [COMM]**
- Aiming a bow while airborne (from a jump, a glide, or a leap off a horse) slows time automatically. The effect lasts until the stamina wheel runs out. — [Twinfinite guide](https://twinfinite.net/guides/zelda-breath-of-the-wild-slow-motion-shoot-bow-how-to/); [GamerGuides basics](https://www.gamerguides.com/the-legend-of-zelda-breath-of-the-wild/guide/the-basics)
- One unofficial blog estimates the time scale at about **1/20**. Speedrunners exploit slowed object physics in a "Bullet Time Bounce" glitch. — [Mechanics of Magic](https://mechanicsofmagic.com/?p=5165) (single source, treat as approximate)

**Enemy AI [DATA]/[COMM]**
- The datamined AI definitions include separate modules for avoiding explosives (with variants for shield-carrying Bokoblins), unarmed punching, noticing threats (`EnemyNoticeTerror`), choosing gear to pick up (`EnemyTargetGearSelect`), returning to post (`EnemyReturnSelect`), and mounted range-keeping. — [ZeldaMods AIDef: BattleCloseExplosivesAvoidRun](https://zeldamods.org/wiki/AIDef:Action/BattleCloseExplosivesAvoidRun); [EnemyTargetGearSelect](https://zeldamods.org/wiki/AIDef:AI/EnemyTargetGearSelect); [EnemyNoticeTerror](https://zeldamods.org/wiki/AIDef:AI/EnemyNoticeTerror); [EnemyReturnSelect](https://zeldamods.org/wiki/AIDef:AI/EnemyReturnSelect); [HorseRideRangeKeepMove](https://zeldamods.org/wiki/AIDef:AI/HorseRideRangeKeepMove)
- Players have seen a disarmed Bokoblin pick up rocks to attack, and a Bokoblin dip its spear into a campfire before attacking with a flaming spear. — [Zelda Dungeon: Dynamic AI in BotW](https://www.zeldadungeon.net/?p=14702) [COMM]
- From the CEDEC talk: aggressive players head for enemy camps to get better gear, so camps work as gear-reward points of interest. — [Famicast CEDEC translation, part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html) [DEV, via translation]
- From the GDC talk: the engine was built on primitive action-game components, namely collision, movement and object states. Elemental and physical reactions come out of this "chemistry/physics" layer instead of scripted one-offs. — [GDC Vault: Change and Constant](https://gdcvault.com/play/1024562/Change-and-Constant-Breaking-Conventions); [Thumbsticks GDC17 report](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies) [DEV]

**Weapon durability rationale [DEV]**
- Fujibayashi (Nintendo Power Podcast, Dec 2017): without durability, players would skip fights they don't need. Durability motivates them to seek out enemies that drop specific weapons. — [Inverse](https://inverse.com/gaming/breath-of-the-wild-2-weapon-durability)
- Aonuma: durability stops players from always using their strongest attack, so they must think about when and against whom to use each weapon. — [Inverse](https://inverse.com/gaming/breath-of-the-wild-2-weapon-durability)
- Critics argue it frustrates because there is no repair or upgrade to offset it, and that breakage comes with too little warning. — [Unwinnable](https://unwinnable.com/2017/04/27/weapon-durability-in-breath-of-the-wild/); [Siliconera](https://siliconera.com/?p=962645) [ANALYSIS]
- An early prototype let Link stab a weapon into a wall to rest while climbing and recover stamina. It was cut, reportedly at Miyamoto's suggestion. — [Zelda Universe](https://zeldauniverse.net/2017/04/16/breath-of-the-wild-originally-involved-using-your-sword-to-climb-cliffs/); [Destructoid](https://www.destructoid.com/you-could-originally-regain-stamina-while-climbing-in-zelda-breath-of-the-wild-but-miyamoto-nixed-it/)

### Inferences
- For a PoE-style build game, BotW's multipliers (8x sneak, 2x head) are "situational multipliers". They map cleanly onto build modifiers such as "+X% sneak damage" or "headshot multiplier", which gives build-crafting hooks that reward play skill.
- Bullet time limited by stamina turns stamina into a shared resource between traversal and offense. That is a useful pattern if the ARPG has a stamina/mana-like pool.
- Durability's stated goal is to force engagement and tactical variety. A PoE-style game, where gear is the build, will likely conflict with harsh durability. A gentler version may keep the goal without the frustration: consumable "charges" on special weapons, or degrading affixes instead of breaking items.
- The AI modules in the datamine (notice, gear-select, explosive-avoid, return-to-post) are a practical checklist for a Unity behavior tree for camp enemies.

### Gaps
- Exact frame windows for Perfect Dodge and Perfect Guard, and the length of the Flurry Rush slow motion. No reliable source was reachable (zeldawiki and fandom were blocked). Unverified community recollection puts Flurry Rush at about 5 hits with fewer for heavy weapons; this needs verification.
- Per-second stamina drain during aerial bow slow motion: not found.
- Thrown-weapon damage bonus (commonly said to be 2x) and the "last hit before breaking" critical (commonly said to be 2x): searched, but no source found.
- Charged/spin attack stamina cost and damage per weapon class (one-handed spin, two-handed charged spin, spear flurry): not researched within budget.
- Stagger/knockback rules (poise thresholds, shield bash, two-handed weapon knockback): not found.
- Bokoblin camp horn and alert propagation, and kicking bombs back: claimed in community lore, but no source found.

## 2. Cooking: combination rules, effects, durations, elixirs, link to exploration

### Takeaway
Cooking is data-driven. Each ingredient carries an effect type and an effect level. Levels are summed, scaled by a per-effect rate, and compared against thresholds to give tiers 1–3. Only one effect survives per dish. A random "critical" can add duration, potency or hearts, and it is guaranteed during the Blood Moon window (about 23:30–00:15). Because ingredients come from specific biomes, exploration feeds back into combat and survival prep.

### Cited Findings
- Effect level: each ingredient's `EffectLevel` is summed and multiplied by the effect type's Material Rate (MR). The result is compared to the first three values of the `NMMR` array, giving level 1, level 2 (if the max Ma is 2 or more) or level 3 (if Ma is 3 or more). — [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking) [DATA]
- A dish has only one effect. If two effect types both clear the first threshold, the game discards both (conflicting effects cancel). — [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking) [DATA]
- Caps: stamina is measured in 1/5 of a wheel and hearts in 1/4 of a heart. All other effect types are hard-capped at level 3 by the executable. Each effect has a Base Time (BT) as its minimum duration before ingredient additions or multipliers. — [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking) [DATA]
- Effect types in the data: LifeRecover, LifeMaxUp (hearty), ResistHot, ResistCold, ResistElectric, AllSpeed, AttackUp, DefenseUp, Quietness (stealth), Fireproof, plus GutsRecover and GutsMaxUp (stamina and enduring). — [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking) [DATA]
- Critical success: about 5% base chance per unique material, raised by some ingredients, capped at 100%, at most one bonus per recipe. A bonus adds hearts, raises effect level, or extends duration (one source lists +5 minutes). — [Zelda Wiki: Cooking in BotW](https://zeldawiki.wiki/wiki/Cooking_in_Breath_of_the_Wild) (search snippet) [COMM]
- Blood Moon: bonuses are guaranteed on a "Bloody Day" while blood-moon progress is above 0.1%. Progress rises from 0% at 23:30 to 100% at 00:00 and falls back to 0% at 00:15. — [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking) [DATA]
- Example: a level-3 attack buff (the maximum) needs a total potency of about 7. Dragon horn shards raise effect level and duration (one guide lists a 30-minute Mighty Lv3 dish, and Dinraal's horn guarantees 30 minutes). — [GameWith](https://gamewith.net/zelda-botw/article/show/36033); [Samurai Gamers recipes](https://samurai-gamers.com/legend-zelda-breath-wild/cooking-recipes-list/) [COMM]
- Sources disagree on whether the duration crit is a flat +5 minutes or a change to the base timer. — [Zelda Wiki](https://zeldawiki.wiki/wiki/Cooking_in_Breath_of_the_Wild) vs. [GameWith](https://gamewith.net/zelda-botw/article/show/36033) [COMM]
- From the GDC talk: the "multiplicative gameplay" toolbox means the same chemistry rules (fire, cold, wind) apply everywhere. Cooking and temperature resistances belong to that system, since cold and hot regions gate exploration. — [Tom's Guide GDC report](https://www.tomsguide.com/uk/us/zelda-breath-wild-design,news-24586.html); [GDC Vault](https://gdcvault.com/play/1024562/Change-and-Constant-Breaking-Conventions) [DEV]

### Inferences
- The tier system (sum of levels × rate compared to thresholds, single effect, conflicts cancel) maps directly onto a PoE-style flask/consumable crafting system. Biome-specific ingredients become loot tables that reward exploring particular regions, and the effect types (resist cold/heat/shock, attack, defense, stealth, speed) line up with ARPG elemental-resistance stats.
- The "only one effect" rule keeps cooking readable. Elixirs (critters plus monster parts) offer a second crafting path that ties combat drops back into consumables.

### Gaps
- Exact NMMR thresholds, MR values and per-ingredient duration additions (commonly said to be about +30s per ingredient) were not available on the reachable ZeldaMods stub.
- Rules for elixirs (critter + monster part), dubious food, rock-hard food and fairy tonic, and the 5-ingredient maximum, could not be fetched because the wiki pages were blocked. These are well known in the community but not cited here.

## 3. Shrines, Spirit Orbs, Divine Beasts, Korok seeds

### Takeaway
There are 120 base-game shrines. Each is a short, self-contained puzzle or combat challenge that rewards one Spirit Orb, and 4 orbs buy 1 Heart Container or 1 Stamina upgrade. There are 900 Korok seeds, used as micro-rewards that expand inventory. Shrines are the game's "bite-size dungeons", and the four Divine Beasts are the larger dungeons.

### Cited Findings
- 120 shrines in the base game, each giving 1 Spirit Orb. Four orbs buy one Heart Container or one stamina upgrade at a Goddess Statue. Maxing both would take 132 orbs, more than are available, so players must choose. — [GameWith: Heart Containers & Stamina Vessels](https://gamewith.net/zelda-botw/article/show/38663); [Prima Games](https://primagames.com/tips/legend-zelda-breath-wild-how-get-more-heart-containers-and-increase-stamina) [COMM]
- DLC shrine count: one source says +16 shrines (136 total), another says +4. The figures conflict. — [Zelda collectibles checklist](https://consolepulse.com/nintendo/zelda/guides/zelda-botw-collectibles-master-checklist) [COMM]
- 900 Korok seeds. 441 buy every inventory slot upgrade, and all 900 earn "Hestu's Gift". — [ConsolePulse checklist](https://consolepulse.com/nintendo/zelda/guides/zelda-botw-collectibles-master-checklist) [COMM]
- CEDEC: Korok seeds are usually placed in "triangle" terrain setups, at the small scale meant to set tempo. — [Famicast CEDEC part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html); [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/) [DEV, via translation]
- Triangle peaks draw the eye, so key items such as Koroks or shrines can be placed at their tips. — [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret) [DEV, via translation]
- Divine Beasts: the four sit at Zora's Domain, Rito Village, Goron City and Gerudo Town, all lower than the terrain around them. — [Game Developer: "Gravity to go Forward"](https://gamedeveloper.com/design/breath-of-the-wild-open-world-analysis-gravity-to-go-forward) [ANALYSIS]

### Inferences
- The 4-orb exchange plus the choice between hearts and stamina is a ready-made template for a "passive skill point" currency. For a PoE hybrid, shrine-equivalents could award passive-tree points.
- Korok density (900) shows that frequent, very small rewards fill gaps between larger POIs.

### Gaps
- Test of Strength (minor/modest/major), "Blessing" shrines (no puzzle, just a reward), shrine quests, and average shrine completion length: no source found within budget.
- Shrine reward chests (weapons and armor) and the puzzle-vs-combat split: not verified.

## 4. Open-world level design: triangle rule, gravity, towers, POI density, guiding without markers (GDC 2017 and CEDEC 2017)

### Takeaway
At CEDEC 2017, director **Hidemaro Fujibayashi** and senior lead artist (terrain) **Makoto Yonezu** presented two tools.
- **Triangle rule:** triangular terrain at three scales gives the player a choice to go over or around, and hides then gradually reveals what lies behind it.
- **"Gravity" (引力):** landmarks with different levels of visibility and importance pull players in different directions.

After towers alone made playtests feel linear, adding gravity points between towers spread tester heat maps across the whole map. At GDC 2017, the "Change and Constant" talk (Fujibayashi, Takizawa, Dohta) covered "multiplicative gameplay" built on a chemistry/physics engine.

### Cited Findings
**Speakers and talks [DEV]**
- CEDEC 2017 field level design talk: director Fujibayashi Hidemaro (藤林秀麿) and senior lead artist Makoto Yonezu (米津真). — [Gamer.ne.jp CEDEC report](https://www.gamer.ne.jp/news/201709010048/) (search snippet); [Den Fami Nico Gamer](https://news.denfaminicogamer.jp/kikakuthetower/171016); [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/)
- The name spellings in your brief need correcting. "Hiroki Hirano" did not present this talk. Some English outlets misspell Fujibayashi's given name as "Hideyuro". Den Fami calls Yonezu the terrain lead artist. — [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/); [Den Fami Nico Gamer](https://news.denfaminicogamer.jp/kikakuthetower/171016)
- Nintendo gave 8 BotW sessions at CEDEC 2017, its first time presenting there, covering field level design, 3D graphics, sound, UI and more. — [Game*Spark](https://www.gamespark.jp/article/2017/07/04/74432.html)
- English coverage comes mainly from Capcom's Matt Walker live-tweeting and translating. No recording exists, and Game Watch published a written Japanese summary. — [Famicast](https://www.thefamicast.com/2017/12/cedec-talks-translated-making-of-breath.html); [Hyrule Interviews (Notion)](https://hyruleinterviews.notion.site/The-Open-Air-Sound-Playing-a-Massive-Breathing-World-a77922aec8864fb5b0142bf350890f37)
- GDC 2017, "Change and Constant: Breaking Conventions with The Legend of Zelda: Breath of the Wild": Hidemaro Fujibayashi, Satoru Takizawa and Takuhiro Dohta. — [GDC Vault](https://gdcvault.com/play/1024562/Change-and-Constant-Breaking-Conventions); [GDC news](https://www.gdconf.com/news/legend-zelda-breath-wild-devs-speaking-gdc-2017)

**Triangle rule [DEV, via translation]**
- Triangles do two jobs: they let players choose to go straight over or around, and they block the view so designers can surprise players. — [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/); [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html)
- There are three scales. **Large** triangles are landmarks and visual markers. **Medium** ones shield landmarks and routes. **Small** ones set the tempo and change which buttons the player presses. — [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret); [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html)
- A triangle reveals what lies behind it gradually as you crest it, while a **rectangle** (a wall, or trees hiding mushrooms and bee nests) hides things completely and reveals them all at once. — [Den Fami Nico Gamer](https://news.denfaminicogamer.jp/kikakuthetower/171016); [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret)
- The team says it applied the rule explicitly and walked the field repeatedly to tune it. — [Den Fami Nico Gamer](https://news.denfaminicogamer.jp/kikakuthetower/171016)

**Towers, gravity and heat maps [DEV, via translation]**
- The original plan was to spread towers across the map with game events between them. Playtesters felt they were being guided, called the game "too linear", and diverged from each other "in a bad way". — [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html); [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret)
- The fix was **"gravity"**: structures with different levels of visibility and importance, placed to pull players in different directions and let them get sidetracked. Fujibayashi says story-critical, distinctive landmarks have the strongest pull. This produced "infinite play", with players moving back and forth between attractions. — [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html); [Den Fami Nico Gamer](https://news.denfaminicogamer.jp/kikakuthetower/171016)
- Before/after heat maps: before, testers followed similar routes and few areas got visits. After, routes spread out much more evenly, reaching "every corner of the field". No numbers were published. — [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret); [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html)
- Attractions are ranked by visibility and by objective, and the order changes with play style (aggressive players go to enemy camps for gear). — [Famicast part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html)
- Chained reveal example: hill, then bridge structure, then tower, where one structure hides the next. — [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret)

**Scale, map and tools [DEV]**
- The map was designed to be about the size of **Kyoto** (Nintendo Dream interview). Early CEDEC footage showed Link skydiving over a Kyoto test map and exploring Himeji Castle. — [GoNintendo](https://gonintendo.com/contents/8220-check-out-this-never-before-translated-interview-with-the-developers-of-breath-of-the); [Zelda Dungeon](https://www.zeldadungeon.net/?p=166446); [Destructoid](https://destructoid.com/?p=259425)
- Development tooling: tasks were placed as in-world signs that link to specs and meetings, with a "field task view". The in-game map has 120 dynamically loaded areas and 4 zoom levels, built from 2,344 procedurally generated screens. — [GIGAZINE](https://gigazine.net/gsc_news/en/20171004-breath-of-the-wild-design-secret); [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/)

**Multiplicative gameplay (GDC) [DEV]**
- Fujibayashi pitched the game with a 2D prototype where wind shakes trees, logs roll into rivers, and arrows passed through flame become fire arrows. The goal was a toolbox players apply anywhere instead of fixed puzzles, which the team called "multiplicative gameplay". — [Tom's Guide](https://www.tomsguide.com/uk/us/zelda-breath-wild-design,news-24586.html); [Thumbsticks](https://www.thumbsticks.com/gdc-17-breaking-conventions-breath-of-the-wild); [AOL/Engadget](https://www.aol.com/news/2017-03-12-breath-of-the-wild-gdc-talk.html)

**Topography as guidance [ANALYSIS]**
- "Gravity to go Forward" (Nic Phan, 2019) argues that BotW's traversal (walk, climb, glide, shield surf) makes going down easier than going up. Key hubs sit in depressions, and roads follow valleys, so players "roll" toward destinations, which reduces the need for HUD and map UI. This is a third-party reading, not the CEDEC "gravity" concept. — [Game Developer](https://gamedeveloper.com/design/breath-of-the-wild-open-world-analysis-gravity-to-go-forward)

### Inferences
- Unity workflow: author terrain with three tiers of "occluders".
  - Large peaks act as landmarks, visible from 1 km or more.
  - Medium ridges hide the next POI.
  - Small mounds and rocks set micro-tempo, with Korok-style micro-rewards on top.

  Then tag each POI with a visibility score and an importance score (gravity), and check coverage with player heat maps in playtests.
- Towers alone create linear "connect the dots" play, so the space between towers needs gravity objects of mixed size and type (camps, ruins, shrines, odd trees).
- Enemy camps doubling as gear-reward attractors fits a PoE-style loot economy: camps can be the "aggressive player" gravity, and shrine-equivalents the "puzzle player" gravity.

### Gaps
- No official numeric POI density was found (for example, distance in meters or seconds between discoveries). Community analyses circulate figures, but none were verified here.
- The CEDEC slide PDF linked by 80.lv was not fetched. It may contain more diagrams.
- I found no source that ties the GDC talk directly to the CEDEC triangle/gravity material. GDC focused on conventions, multiplicative gameplay and the physics/chemistry engine, while CEDEC covered field design.
- Tower-to-tower spacing, tower count (15 commonly cited) and how map reveal works were not verified in this pass.

## 5. Enemy camps and encounter density

### Takeaway
Officially, enemy camps are framed as an attraction type with high value for aggressive or gear-seeking players (CEDEC). Their AI is modular: notice, gear selection, explosive avoidance, return to post. I found no numeric data on camp density or spacing.

### Cited Findings
- Camps are ranked as an objective in the gravity system, and aggressive players prioritize them for better gear. — [Famicast CEDEC part 1](https://www.thefamicast.com/2017/12/part-1-managing-tasks-and-world-design.html) [DEV, via translation]
- Fujibayashi says durability motivates players to engage enemies instead of avoiding them, which links camp encounters to weapon supply. — [Inverse](https://inverse.com/gaming/breath-of-the-wild-2-weapon-durability) [DEV]
- The datamined AI modules for camp enemies include threat notice, gear pickup, explosive avoidance and return to post. — [ZeldaMods AIDef index examples](https://zeldamods.org/wiki/AIDef:AI/EnemyReturnSelect) [DATA]

### Inferences
- Pair camps with weapon economy (durability or consumable affixes) so camps feel necessary, not optional. Camps also make natural "gravity" points between larger landmarks.

### Gaps
- No sourced data on camp count, spacing, respawn rules (Blood Moon resets enemies; commonly known but not cited here) or scaling of enemy tiers (the hidden level-scaling "point" system is community knowledge, not cited).
