# BotW (2017): Traversal, Movement, Camera and Game Feel, for re-implementation in a Unity third-person prototype

> **Source legend.** **[DM]** = datamined. The values are read directly from game files (leoetlino's `botw` dump of the 1.5/1.6 ROM data, converted to YAML). **[OBS]** = observed or community-reported. **[INF]** = my own inference or interpretation, not stated by any source.
>
> Primary datamine files (raw YAML, publicly readable):
> - Player general params: https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml
> - Player AI program (per-state params for climb, glide, swim, jump and so on): https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml
> - Camera general params: https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/GameRomCamera.gparamlist.yml
> - Key descriptions (partial): https://zeldamods.org/wiki/Bgparamlist
>
> **Unit warning.** The files never state units. Distances appear to be metres, because Link's physics center of mass is about 0.85 up, which fits a roughly 1.7 m character ([physics](https://github.com/leoetlino/botw/blob/master/Actor/Physics/Player_Link.physics.yml)). Speeds such as `MaxSpeedF` appear to be metres per frame at 30 fps. Times named `*Time`/`*Frame` appear to be in frames at 30 fps. These are all [INF] and need validating in-engine.
>
> Several Zelda wikis (zeldawiki.wiki, zeldadungeon.net, zelda.fandom.com) returned 403/402 to my fetches. Their content appears here only where search snippets surfaced it.

## 1. Stamina wheel: base, max, regen, exhaustion, per-action costs

### Takeaway
Stamina is a float pool. One wheel is 1000 units and the maximum is 3000 units (3 wheels). Each Stamina Vessel adds 200 units (one fifth of a wheel). The datamine gives exact cost and regen constants: sprint 300, climb 36.5 (scaled by direction and slope), climb jump 310, glide 28, swim 30, swim dash 125, dash-jump 300, charge-attack start 250 plus 500 per charge level, and regen 300 after a 10-frame delay (30 frames when tired). The time unit of the continuous rates is not stated. "Units per second" fits observed play.

### Cited Findings
**Pool size**
- [DM, save data] The max-stamina save value is 0x447A00(00) at game start. This is IEEE float 1000.0. The next step is 0x449600(00), which is 1200.0, meaning +200 per vessel. Any value between 0x447A00 and 0x453B80 (3000.0) draws a partial wheel. — [marcrobledo savegame-editors issue #30](https://github.com/marcrobledo/savegame-editors/issues/30)
- [OBS] A forum user reports that stamina is stored as a float with a default maximum of 3000. — [GBAtemp: BotW savefile hex edit](https://gbatemp.net/threads/botw-savefile-hex-edit.504699/)
- [OBS] Each Stamina Vessel adds one fifth (20%) of a wheel. The cap is 3 full wheels. — summarised from search results for [Zelda Dungeon Stamina Wheel](https://www.zeldadungeon.net/wiki/Stamina_Wheel) and [Screenrant](https://screenrant.com/zelda-botw-raise-link-stamina-max-how-high/)
- [OBS] Enduring food and elixirs add temporary yellow stamina that does not regenerate once spent. Energizing food restores stamina. — search snippets of [Zelda Fandom Stamina Wheel](https://zelda.fandom.com/wiki/Stamina_Wheel)

**Regen and exhaustion constants** ([DM] [Player_Link.gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml); descriptions from [ZeldaMods bgparamlist](https://zeldamods.org/wiki/Bgparamlist))
- `EnergyAutoRecover: 300.0`. ZeldaMods calls this the "stamina recovery rate (per frame)". A literal per-frame reading would refill a wheel in about 3 frames, so the real unit is probably larger (see Inferences).
- `EnergyAutoRecoverInAir: 0.0`. No recovery while in freefall or in the air.
- `EnergyAutoRecoverInvalidTime1: 10.0` is the initial regen delay in frames. `EnergyAutoRecoverInvalidTime2: 30.0` is the regen delay when tired, in frames.
- `EnergyTiredValue: 99.0`. Undocumented. Likely the threshold or recovery amount tied to the exhausted ("tired") state.
- `VelDiamTired: 0.85` and `StickDiamTired: 0.85`. While exhausted, movement speed and stick input are multiplied by 0.85.
- `NoEnergyDashInterval: 60.0`. Undocumented. Likely the frames before sprint is allowed again after running dry.
- `PlayerFall` and `PlayerTwiceJump` have `NoClimbTime: 8` and `NoClimbTimeTired: 16–20`. After falling off a wall, Link cannot re-grab for 8 frames, or 16–20 frames if exhausted. `ClimbRestartHeight: 20.0` and `ClimbRestartTime: 30.0` are related but undocumented. — [Player_Link.aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml)
- `PlayerSitWait: AutoRecoverRate 2.0`. Undocumented. Likely a 2x recovery multiplier while sitting. — [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml)

**Per-action costs** ([DM] [Player_Link.aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml) unless noted)

| Action | Key (state) | Value | Notes |
|---|---|---|---|
| Sprint | `EnergyDash` (PlayerMove) | 300.0 | `EnergyDashTrig: 20.0` is probably a one-off cost when sprint is triggered |
| Sprint jump | `EnergyDashJump` (PlayerJump) | 300.0 | Normal jump `EnergyJump: 0.0`, so a standing or walking jump is free. `EnergyUseDiam1/2/3: 1.0/1.5/2.0` are likely multipliers |
| Climb (continuous) | `StaminaDownAlways` (PlayerActionClimb) | 36.5 | Multiplied by direction: up ×1.0, sideways ×0.5, down ×0.5 (`StaminaRateMovingUp/Side/Down`) |
| Climb slope scaling | `StaminaRateSlopeMin/Center/Max` | 0.5 / 1.0 / 2.0 | Steeper or overhanging walls cost up to 2x |
| Climb idle | `StaminaDownWait` | 0.0 | Hanging still on a wall costs no stamina |
| Climb in rain | `StaminaDownScaleInRain` | 1.2 | 20% more drain in rain, on top of the slipping |
| Climb jump | `StaminaDownTriggerJump` | 310.0 | Charge-level scaling `StaminaScaleTriggerJumpLv1/2/3: 0.5/0.8/1.0` |
| Climb Jump Stamina Up set bonus | `ArmorCompClimbJumpEnergyRate` (gparamlist) | 0.5 | Climb jump cost halved |
| Rest on weapon peg | `EnergyClimb` (PlayerClimbRest) | 36.5 | |
| Glide (paraglider) | `EnergyGlide` (PlayerParashawlGlide) | 28.0 | `NoEnergyTime: 30.0` |
| Swim (move) | `EnergyMove` (PlayerSwimMove) | 30.0 | `EnergyWait: 0.0`, so floating still costs nothing |
| Swim dash | `EnergyDash` (PlayerSwimDash) | 125.0 | Swim jump `EnergyJump: 100.0` |
| Swim Dash Stamina Up (Zora set) | `ArmorCompSwimEnergyRate` (gparamlist) | 0.25 | |
| Naked swim | `ArmorCompNakedSwimEnergyRate`, `SpeedRate`, `AnimeRate` (gparamlist) | 0.8 / 1.2 / 1.2 | Hidden bonus: swimming with no armour is cheaper and faster |
| Charged attack start | `EnergyChargeStart` | 250.0 | `EnergyPerChargeLv: 500.0`; gparamlist `EnergyCharge: 250.0` is the per-time drain while charging or doing a spin |
| Large-sword spin | `EnergyMove` (charge spin state) | 300.0 | `MaxTime: 150`, `AccSpeed 0.003`, `DecSpeed 0.005`. Rumble `RumbleType 4`, power 1–2 |
| Bow slow-mo (air aim) | `EnergyBowSlow` (gparamlist) | 260.0 | `BowSlowMaxTime: 135`, `DiveBowSlowMaxTime: 90`, `BowSlowRateDiam: 20.0` (likely time slowed 20x), `BowSlowInvalidHeight: 1.5` m (1.5 m minimum height), 2.1 when jumping off a shield |
| Push heavy object | `EnergyPush` (gparamlist) | 60.0 | |
| Calm a horse | `EnergyDecreasePerSec` (PlayerCalmHorseDown) | 400.0 | This is the **only** key whose name states "per second" |
| Urbosa's Fury threshold | `SupportElectricEnergy` | 500.0 | Half a wheel |

- [OBS] Bounding while climbing or swimming drains the wheel faster but covers the distance in less time. — snippet of [Zelda Dungeon Stamina Wheel](https://www.zeldadungeon.net/wiki/Stamina_Wheel)
- [OBS] One guide claims that one base wheel gives about 15 s of sprinting. This is unverified and conflicts with the datamine ratios; treat as unreliable. — [eathealthy365 guide](https://eathealthy365.com/zelda-botw-stamina-your-ultimate-guide-to-maxing-it-out/)
- [OBS] Sprinting drains at the same rate whatever armour is worn. — [GameFAQs](https://gamefaqs.gamespot.com/boards/189707-the-legend-of-zelda-breath-of-the-wild/75693540)
- [OBS] The speedrun community lists these stamina consumers: running, climbing, gliding, swimming, charging attacks, bullet time mid-air, taming horses, and pushing heavy objects. — [speedrun.com forum](https://www.speedrun.com/botw/forums/y2gok)
- [OBS] "Stamina-free sprint" by mashing the sprint button is a known technique that exploits the per-trigger and continuous cost split. — [speedrun.com forum](https://www.speedrun.com/botw/forums/gmh4u)

### Inferences
- [INF] The continuous rates are most likely **units per second, with 1000 units per wheel**. `EnergyDecreasePerSec: 400` uses the same scale. On that reading, one wheel lasts about 3.3 s of sprint, about 27 s of straight-up climbing on a flat wall (about 55 s sideways), about 36 s of gliding, and about 33 s of swimming. Regen refills a wheel in about 3.3 s. These durations fit typical play, but measure them in-game before you lock them in.
- [INF] Exhaustion design for Unity:
  - At 0 stamina, enter a Tired state. Speed ×0.85, no sprint, glide or climb.
  - Wait 30 frames (1 s) before regen starts.
  - Stay Tired until the wheel is full again. This is the well-known BotW rule, but I did not find a citable source for the full-refill condition.
- [INF] Implement costs as `rate * dt * directionMul * slopeMul * weatherMul * armorMul`. Implement one-shots (climb jump, dash trigger, charge start) as immediate deductions.

### Gaps
- No primary source states the time unit of the energy constants. The full-refill-before-exit rule for exhaustion lacks a fetched citation; the wikis that likely have it returned 403.
- Precise frame-timed measurements of wheel durations were not found.

## 2. Climbing

### Takeaway
Almost any surface steeper than about 50° is climbable. Climb drain depends on movement direction (up costs 1x, side and down cost 0.5x) and on wall slope (0.5x–2x). Hanging still is free. Rain causes random slips every 60–120 frames and drains 1.2x faster. The climb jump is a chargeable one-shot that costs 310.

### Cited Findings
All [DM] from [Player_Link.aiprog, PlayerActionClimb state](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml) and [Player_Link.gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml), unless noted.

**Climbable surfaces**
- `ClimbEnableAngle: 50.0` (gparamlist). This is likely the minimum surface angle that triggers climbing; below it you walk.
- `ClimbEnableSpeedMinAngle: 0.05` and `ClimbEnableSpeedMaxAngle: 0.02`, plus `DashUpEnableAngle: 69.5` and `DashUpEnableSpeed: 0.18`. These are likely sprint-into-wall "dash up" thresholds. `PlayerWallDashUp: JumpHeight 1.3`, `MinSpeedF 0.05`, `MaxSpeedF 0.15`.
- `EndGroundAngle: 45.0`. Climbing ends and becomes walking when the surface falls below 45°.
- `FallLimitWallAngle: 160.0`. Ceilings or overhangs beyond 160° cannot be held.

**Probing and contact geometry**
- `RayCastOffsetY: 1.8` and `RayCastLength: 1.5`. The wall probe ray starts 1.8 m up and is 1.5 m long.
- `BodyFixedOffset: 0.35` is the body distance from the wall.
- `BodyDiffLengthMaxNormal: 0.7` and `BodyDiffLengthMaxJump: 1.2` are likely the maximum surface discontinuity tolerated.
- `OcclusionCheckLength: 3.0`.

**Speed and animation**
- `DashAnmSpdRate: 1.5`.
- `StaminaDownAlwaysMaxSpeed: 0.7`. Likely the drain normalises to climb speed (full drain at speed 0.7).
- `EndWaitRate: 0.35`.

**Climb jump (charged)**
- `ChargeJumpLevel1Frame: 12` and `ChageJumpLevel2Frame: 24`. Hold for 12 or 24 frames to reach charge levels.
- `ChargeJumpScaleMin/Max: 0.6/1.4`. Jump distance scales from 0.6x to 1.4x.
- Cost multipliers per level are 0.5, 0.8 and 1.0 of 310.
- `LockingAfterJumpCnt: 20` frames of lock after a jump.
- `JumpScaleWhenLitteleStamina: 1.0`.
- Flick detection for the input: `FlickThresholdMin/Max 0.1/0.9`, `FlickWaitToOverMaxTime 0.1`, `FlickOverMaxToReturnTime 0.15`.

**Rain slip**
- `BaseCountSlipInRain: 60` plus `RandomCountSlipInRain: 60`. A slip happens every 60–120 frames, about 2–4 s at 30 fps.
- `BaseSpeedSlipInRain: 12` plus `RandomSpeedSlipInRain: 3` set the slip speed.
- `DownStaminaSlipInRain: 150.0` is the stamina lost per slip.
- gparamlist `SlipEnableSpeed: -0.05`, `SlipSpeedAddMin/Max: 0.05/0.15`, `SlipSpeedAddDiamByRain: 2.5`.

**Ledges, ladders, trees, walls**
- Ledge, step and mantle are separate states: `PlayerHangWallCatch` (small ledge grab), `PlayerStepMove` (step up), `PlayerLadder` (`LadderToClimbTime: 10`), `PlayerLadderToClimb`, and `PlayerTreeClimb`.
- `PlayerWallJump: JumpHeight 0.58, JumpSpeedF 0.2`. `PlayerWallSlip: JumpHeight 0.5`. Root-state `WallSlipEnableSpeed: 0.15`.
- `DoClimbForbidTime: 1.0` and `DoClimbForbidAngVel: 1.0`.

**Climbing Speed Up effect**
- The ArmorEffect `ClimbSpeed` has max level 3. The multiplier per level is not in the param file; it is likely hardcoded. — [ZeldaMods bgparamlist](https://zeldamods.org/wiki/Bgparamlist)
- [OBS] One guide claims the Climbing Gear set slows stamina use and stops rain slipping (set bonus). The page is low quality, so verify before relying on it. — [kelvarisstudios](https://kelvarisstudios.com/climbing-gear-botw/)
- [OBS] A GameFAQs user reports that drain is faster on sharper inclines and that climbing gear slows depletion. This matches the slope multipliers above. — [GameFAQs](https://gamefaqs.gamespot.com/boards/189707-the-legend-of-zelda-breath-of-the-wild/75078732)

### Inferences
- [INF] Unity recipe:
  - Detect climbing with a SphereCast or Raycast from about 1.8 m up, about 1.5 m forward. Climb when the surface angle is 50° or more.
  - Keep the body 0.35 m off the wall, aligned to the averaged normal.
  - Exit to walk when the normal angle drops below 45°. Mantle with a ledge probe when the top ray misses.
  - Drain = 36.5 × direction multiplier × slope multiplier (lerp 0.5–2.0 by wall steepness) × rain 1.2. No drain when stationary.
  - Climb jump: hold for 12 or 24 frames, distance 0.6–1.4x, cost 155, 248 or 310.

### Gaps
- No absolute climb speed in m/s, because climbing is likely animation-root-motion driven.
- The numeric value of Climbing Speed Up per armour level was not found. It is not in the param files I checked.
- Exactly which materials are non-climbable is not in these files. Observed examples include Divine Beast and shrine-interior surfaces, but I have no citation.

## 3. Gliding (paraglider)

### Takeaway
The paraglider is the `PlayerParashawlGlide` state. It drains 28 stamina per tick unit, has a wind scale of 10, and limits body pitch and roll angles. Bow aiming in the air triggers bullet time, which costs `EnergyBowSlow` 260, lasts 135 frames (90 when diving), and needs at least 1.5 m of height. Gliding does not regenerate stamina. Opening the glider even briefly cancels fall damage.

### Cited Findings
**[DM] State parameters** ([aiprog PlayerParashawlGlide](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `EnergyGlide 28.0`, `NoEnergyTime 30.0`.
- `GlideSpeedMax 0.02` and `Lv2GlideSpeedMax 0.35`. The meaning is unclear; possibly two speed tiers.
- `GlideBodyFrontX 20°`, `GlideBodyBackX 10°`, `GlideBodySideZ 15°`. These are visual body lean limits: forward pitch, back pitch and side roll.
- `GlideRotMax 1.0`, `GlideRotMin 0.1`, `GlideRotRate 0.2`, `GlideRotSpeed 20.0`.
- `WindScale 10.0`. Wind and updraft force multiplier.
- `OverSpeedDec 0.002`. Deceleration when above max speed, which bleeds off dive or launch speed.
- Kokko (Cucco) gliding uses identical values.

**[DM] Opening rules** ([aiprog root](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `ParashawlInvalidHeight 1.2` and `ParashawlInvalidHeightSurfing 1.0`. Link must be at least about 1.2 m off the ground (1.0 m from a shield surf) to open the glider.
- `PlayerCutFall ParashawlInvalidTime 10`.
- A `PlayerParasailAltitude` behaviour exists, likely driving altitude-dependent audio or VFX.

**[DM] Updrafts and Revali's Gale**
- `PlayerSuperJump: JumpHeight 40.0, WindScale 10.0`. Revali's Gale launches about 40 m. Wind support reuse time is 360 (`WindSupportReuseTime`).
- Wind-launch states: `InitSpeedWindRemain 0.5`, `JumpHeightWindRemain 12.0`, `AddLinearImpulseWindRemain 50.0`; `InitSpeedWind 0.4`, `JumpHeightWind 1.3`.

**[DM] Bow in mid-air**
- `BowSlowShootNum: 8`, `BowSlowMaxTime: 135`, `DiveBowSlowMaxTime: 90`, `BowSlowInvalidTime: 300`, `BowSlowRateDiam: 20.0`, `EnergyBowSlow: 260.0`. — [gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml)

**[OBS] Observed behaviour**
- When the stamina gauge reaches zero, the glider closes and Link falls. — search snippet of [Zelda Dungeon Paraglider](https://www.zeldadungeon.net/wiki/Paraglider)
- Opening the paraglider slows the descent and prevents fall damage. One user claims that about 0.1 s of open glider is enough. — [GameFAQs thread](https://gamefaqs.gamespot.com/boards/632936-the-legend-of-zelda-breath-of-the-wild/75085221)
- Hot air over fires and vents creates updrafts. Paragliding after jumping over a wind vent lifts Link quickly. — search snippets, [Zelda Dungeon Paraglider](https://www.zeldadungeon.net/wiki/Paraglider)
- After running dry, a re-opened glider lasts only "a couple of seconds" before Link drops. — [GameFAQs Q&A](https://gamefaqs.gamespot.com/switch/189707-the-legend-of-zelda-breath-of-the-wild/answers/494954-paraglider-and-stamina-question)

### Inferences
- [INF] Unity recipe:
  - Glide is a state with constant forward speed and capped fall speed.
  - Map stick input to yaw rate (`GlideRotSpeed` about 20°/s) and visual lean (20°/10°/15°).
  - Apply the wind volume force × 10. Updraft volumes push upward. `OverSpeedDec` bleeds off excess speed.
  - Drain 28 per second. There is no regen in the air.
  - Bow aim while airborne at ≥1.5 m: set Time.timeScale to about 1/20 for 135 frames (about 4.5 s), cost 260.

### Gaps
- Absolute glide forward speed and sink rate in m/s were not found in the files or in any measured source.
- Whether the glider cancels momentum when opened was not documented.

## 4. Swimming

### Takeaway
Swimming speeds are 0.052 m/frame forward and sideways, 0.0416 backward, and 0.0756 m/frame when dashing. That is roughly 1.56 / 1.25 / 2.27 m/s at 30 fps [INF]. Swimming costs 30, a swim dash costs 125, and floating still is free. At zero stamina Link drowns and respawns on land with damage.

### Cited Findings
- [DM] The values above come from `PlayerSwimMove` (`DecSpeedRate 0.25`), `PlayerSwimWait` (`EnergyWait 0`), `PlayerSwimDash` (`EnergyDash 125`), and `PlayerSwimJump` (`JumpHeight 1.0`, `JumpSpeedF 0.25`, `EnergyJump 100`). Spin attack in water costs `EnergyDash 125`. — [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml)
- [DM] Waterfall climbing (Zora Armor): `PlayerWaterFallJump JumpHeight 20.0` (70 with Zora), `PlayerZoraJump JumpHeight 5.0`. Water dive: `DiveSpeedF 0.185`, `DiveHeight 1.5`. — [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml)
- [DM] Shallow water and bog:
  - `MoveIgnoreWaterHeight 0.3`. Water shallower than 0.3 m does not slow movement.
  - `MoveMaxDecRateByWater 0.3`.
  - `NoSquatWaterHeight 0.6`. No crouching in water deeper than 0.6 m.
  - `MoveDecRateByBog 0.35`.
  - Jump height in water reduced up to `JumpHeightMaxDecRateByWater 0.5`.
  - `InWaterTimeForRagdoll 40`.
  - — [gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml), [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml)
- [DM] Naked swim bonus: 1.2x speed, 1.2x animation rate, 0.8x stamina. — [gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml)
- [OBS] Swim speed is an armour effect (`SwimSpeed` ArmorEffect, max level 3). — [ZeldaMods bgparamlist](https://zeldamods.org/wiki/Bgparamlist)

### Inferences
- [INF] Drowning: when stamina hits 0 while swimming, play a sink animation, take about 1 heart of damage, and respawn at the last safe ground point. This is standard BotW behaviour, but I did not fetch a citation, so verify it.

### Gaps
- I found no citable source for the exact drowning damage or respawn rule.

## 5. Sprinting, jumping, slope sliding, fall damage, shield surfing

### Takeaway
A standing jump is low (`JumpHeight` 0.58, plus a speed bonus ×2) and free. A sprint-jump costs 300. Slopes start sliding at 25°. Fall damage has a minimum of 8 HP. Falls of 100 m or more are lethal (`DeadHeight 100`) unless the glider is opened. Shield surfing reaches a max speed of 0.4 m/frame (about 12 m/s [INF]), costs no stamina, and wears the shield.

### Cited Findings
**[DM] Jump** ([aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `JumpHeight 0.58`, `JumpHeightAddByAngle 0.01`, `JumpHeightAddBySpeed 2.0`.
- `MaxSpeedInAir 0.3` (gparamlist) caps air speed.
- Backflip `BJSpeedF 0.15`. Jump attack `CutJumpHeight 1.5` (short version 0.98), `CutJumpSpeedF 0.16`.
- Plunge attack: `FallSpAttackHeight 1.0`, `FallSpLargeAttackRadius 5.0`.

**[DM] Sprint** ([aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml), [gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml))
- `EnergyDash 300`, `EnergyDashTrig 20`.
- `DashNoise 4.0`. Sprinting makes noise that enemies hear; whistle noise is 9.0.
- `DashToRunStickValueDec 0.03`.
- `VelDiamSand 0.8`. Movement is ×0.8 on sand and snow unless the armour has `SandMove` or `SnowMove`.
- Turn-in-place thresholds: `TurnEnableStickSub 30`, `TurnEnableDirSub 120°`.

**[DM] Slope slide** (`PlayerSlide`, [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `LimitAngleDegStart 25.0`, `FrontSlideMaxSpeedF 0.1`, `BackSlideMaxSpeedF 0.4`, `MaxSlipSpeed 12.0`, `SlipSpeedAdd 1.0`, `SlipSpeedDec 0.2`, `SlipDownTime 90`, `UpKeyInvalidTime 10`.
- Root `ForceSlipSpeed -5.25`.
- Tumbling down (`PlayerSlippingDown`): 1 damage every 15 frames once speed exceeds 15 (`EnableSpeedDamage 15`).

**[DM] Fall damage** (`PlayerLandDamage`, [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `DeadHeight 100.0`, `DamageMin 8`, `WaitTimeMin 0`, `WaitTimeMax 8`.
- `ToFallHeightForJustRush 2.0`.
- No-death safety: gparamlist `NoDeathDamageBase 20` and `NoDeathDamageAdd 4` are likely the "survive one lethal hit at full HP" rule.

**[OBS] Fall damage cancels**
- Speedrunners document fall-damage cancels: landing in water, opening the glider just before landing, shield-surf landing, and so on. — [Zelda Dungeon speedrun: Fall Damage Cancel](https://www.zeldadungeon.net/wiki/index.php?title=Speedrun%3ABreath_of_the_Wild%2FFall_Damage_Cancel&mobileaction=toggle_view_desktop)
- A GameFAQs thread notes that fall damage depends on height fallen, not on speed. — [GameFAQs](https://gamefaqs.gamespot.com/boards/632936-the-legend-of-zelda-breath-of-the-wild/75085221)

**[DM] Shield surfing** (`PlayerShieldRideMove`, [aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- `EnergyMove 0.0`, `MaxSpeed 0.4`, `JumpSpeedF 0.25`, `ContJumpSpeedF 0.17`, `JumpHeight 0.98`, `ContJumpInterval 90`.
- `ShieldBreakHeight 20.0`. Likely a fall of 20 m or more while surfing breaks the shield.
- `FinishSpeed 3.0`, `MaxJumpSpeed 0.15`, `LandSEOnInAirFrame 5` (landing sound plays after 5 or more frames airborne).

**[DM] Shield surfing globals** ([ZeldaMods bgparamlist](https://zeldamods.org/wiki/Bgparamlist))
- Per-shield `RideBreakRatio` (default 1.0) is the durability-loss multiplier while surfing; 0 disables it.
- `SurfingFriction` (1.0) is the multiplier on speed loss from ground contact.
- Globals: `ShieldRideBaseFrame 60`, `ShieldRideHitBaseDamage 1`, `ShieldSurfMasterFrictionRatio 0.2`. The "Shield Surf Up" modifier multiplies friction by 0.2.

### Inferences
- [INF] Fall damage formula guess: no damage below some threshold (not in the files). Above it, damage of at least 8 HP (half a heart = 2 HP in BotW, so about 2 hearts [INF]), scaling up to death at ≥100 m. Tune empirically.
- [INF] Shield surfing in Unity: switch to a low-friction physics material and add a slope-gravity force. Cap speed at about 12 m/s. Subtract durability on a timer and on impacts.

### Gaps
- The no-damage fall height threshold and the damage curve between 8 HP and 100 m were not found.
- Absolute run and sprint speeds (m/s) were not found. They are likely animation-driven.

## 6. Camera and controls

### Takeaway
The camera is a free orbit camera that the player drives with the right stick. Gyro aiming runs at 2x stick sensitivity in aim mode. Lock-on is ZL **hold** ("Focus"). It frames Link and the target with a pivot between them, and freezing camera rotation when the player touches the right stick. The datamined camera file holds only post-cutscene blend rates and roof-avoidance weights; most camera tuning is hardcoded or stored elsewhere.

### Cited Findings
**[DM] Camera blend after events** ([GameRomCamera.gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/GameRomCamera.gparamlist.yml))
- `LatConnectRateAfterEvent 0.75`, `LngConnectRateAfterEvent 0.325`, `DistConnectRateAfterEvent 1.5`, `FovyConnectRateAfterEvent 0.0`.
- `ConnectAfterEventMin/Max 30/90` frames (1–3 s blend back to gameplay).
- `RoofGradient*` weights of 1.0/0.45/1.0/0.25 handle low ceilings.

**[DM] Aim and look** ([Player_Link.gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml))
- Aim sensitivity: `LNGStickScale 0.2`, `LATStickScale 0.2`, `LNGGyroScale 0.4`, `LATGyroScale 0.4`.
- `StickRInputFrame 5`.
- Head/look IK: `LookPosOffsetXZ 7.5`, `LookPosOffsetY 1.5` (swim 0.1, horse 2.5), `LookEnableAngle 179`, `LookContinueTimeToCamera 30`.
- Waist IK limits: 45/45/50°.

**[DM] Lock-on target smoothing** ([gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml))
- `AtnPosInterPolationRate 0.2`, `Min 0.01`, `Max 0.2`, `PredictDiffAngleMax 45`.
- Guard angle `GuardableAngle 120°`.

**[DM] Hit feedback** ([gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml))
- Hit-stop and hit-slow: `HitStopTimeS/L 2/6` frames at `HitStopRate 0.1`; `HitSlowTimeS/M/L 1.5/3/4.5` at `HitSlowRate 0.1`.
- Perfect guard slow `GuardJustForceSlowTime 2.0`.
- [INF] Hit-stop and hit-slow are central to combat feel.

**[OBS] Lock-on behaviour** ([Zelda Archive: Z-targeting](https://zelda-archive.fandom.com/wiki/Z-targeting))
- BotW calls Z-targeting "Focus" or "Lock on", bound to **ZL hold**.
- It works on enemies, NPCs and some objects such as signs. It does not work on wildlife except bees.

**[OBS] Lock-on critique** ([critpoints review](https://critpoints.net/2017/05/02/zelda-breath-of-the-wild-review/))
- The pivot sits "halfway between you and the enemy" and zooms to keep both on screen.
- With no stick input, the camera holds an offset diagonal framing.
- Touching the right stick freezes it in place.

**[OBS] Player complaints** ([GameFAQs](https://gamefaqs.gamespot.com/boards/189707-the-legend-of-zelda-breath-of-the-wild/75857195?page=2))
- Lock-on releases unless held. The camera drifts to a side view.

### Inferences
- [INF] Unity (Cinemachine) recipe:
  - FreeLook orbit with damping. Fire a recenter-behind-player timer after a period of no right-stick input.
  - Lock-on: a TargetGroup (player + target) with weights, held on a trigger. Smoothing about 0.2 per frame. Release when the trigger is released.
  - Climbing: pull the camera back and allow a wider pitch so the player can see up the wall.
  - Gliding: raise the camera and lower pitch to show the ground.
  - Use a 1–3 s blend when returning from cutscenes. Add a roof or occlusion collider to avoid clipping.
  - Offer gyro aim at 2x stick sensitivity.

### Gaps
- Default orbit distance, FOV, and per-state (climb/glide) camera offsets were not found. They are not in `GameRomCamera.gparamlist`; they may live in hardcoded tables. No developer source describes camera behaviour during climbing or gliding.
- Official controller-layout philosophy statements were not found in sources I could fetch.

## 7. Game feel, UI minimalism, sensors, map and pins

### Takeaway
BotW's HUD is conditional and minimal. The stamina wheel appears beside Link only while stamina is being used or refilled. A "Pro" HUD mode removes almost everything. Feel comes from noise or stealth coupling, hit-stop, bullet-time slow-mo, rumble on heavy attacks, and many discrete transition states (land, slide-land, step-up, ledge catch) instead of hard snaps.

### Cited Findings
**[OBS] Stamina wheel visibility**
- The stamina wheel is hidden until an action consumes stamina, such as sprinting, swimming or gliding. — [Neoseeker HUD guide](https://www.neoseeker.com/the-legend-of-zelda-breath-of-the-wild/walkthrough/HUD)
- A UX article describes it as a "conditional UI element", appearing only when needed. — [Craft & Crew UX insights](https://craftandcrew.ca/blog/insights/what-zelda-breath-of-the-wild-taught-me-about-ux)

**[OBS] HUD options**
- Pro HUD mode strips the HUD down to almost nothing; the stamina wheel still shows when sprinting or refilling. — [Shacknews](https://www.shacknews.com/article/98914/zelda-breath-of-the-wild-hud-pro-mode-gives-you-a-sparse-ui), [MyNintendoNews](https://mynintendonews.com/2017/02/06/zelda-breath-of-the-wild-minimalist-hud-mode-shown/)

**[OBS] Design critique**
- One designer critiques the extra stamina wheels being drawn in a corner as inelegant and proposes shape-based alternatives. — [Antoine Fougea](https://antoinefougea.com/posts/breath-of-the-wild-stamina-bar-with-shapes/)

**[DM] Feedback hooks** ([aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml), [gparamlist](https://github.com/leoetlino/botw/blob/master/Actor/GeneralParamList/Player_Link.gparamlist.yml))
- `PlayerMoveNoise`, `PlayerLandNoise`, `FootstepReactionChanger`, `PlayerParasailAltitude`.
- Climb noise `NoiseMaxValueMove/Jump 1.0`.
- Rumble on plunge and spin attacks: `RumbleType 4`, power 1–2.
- Shield-surf landing sound gated on 5 or more frames airborne.
- Hit-stop and hit-slow values (section 6).
- Equip, attack and arrow-ready noise of 0.1/1.0/0.1.

**[DM] Many small transition states** ([aiprog](https://github.com/leoetlino/botw/blob/master/Actor/AIProgram/Player_Link.aiprog.yml))
- Animation and state granularity: `PlayerLand`, `PlayerSlideLand`, `PlayerBackJumpLand`, `PlayerSideStepLand`, `PlayerHangWallCatch`, `PlayerStepMove`, `PlayerLadderUpStart/End`, `PlayerSwimLand` and others.
- [INF] These are the anticipation and recovery beats that give movement its weight.

**[OBS] Developer source**
- The GDC 2017 talk "Change and Constant – Breaking Conventions with The Legend of Zelda: Breath of the Wild" (Fujibayashi, Takizawa, Dohta) covered the 2D prototype and the physics or chemistry systems. About 90 minutes, on GDC Vault. — [Game Developer](https://www.gamedeveloper.com/design/-i-the-legend-of-zelda-breath-of-the-wild-i-devs-are-speaking-at-gdc-2017-), [Nintendo Life](https://www.nintendolife.com/news/2017/03/zelda_breath_of_the_wild_gdc_presentation_shows_off_dev_process_and_bizarre_early_concepts), [Kartones talk list](https://blog.kartones.net/post/gdc-game-dev-talks-about-zelda-botw-and-zelda-totk/)
- I found no written summary of traversal-specific content from the talk.

### Inferences
- [INF] Unity:
  - Use a world-space stamina radial anchored beside the character. Fade it in on drain and fade it out about 1 s after full.
  - Flash it red and shrink it when tired.
  - Draw extra wheels as concentric rings (BotW's approach).
  - Use Timeline or short state clips for land and mantle instead of instant transitions.
  - Drive enemy hearing from movement-state noise values (sprint 4.0, whistle 9.0).

### Gaps
- No primary or developer source found for the sensor, telescope or pin design rationale (the scope, stamp pins up to 100 markers, and Sheikah Sensor+ settings).
- Haptic (HD Rumble) design details beyond the rumble params above were not found.
- Game Maker's Toolkit analysis was not retrieved. Video content is not text-fetchable in this pass.
