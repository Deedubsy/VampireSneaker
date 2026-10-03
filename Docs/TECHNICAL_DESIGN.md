# VESPERTINE — Technical Design

Unity **6000.6.0f1**, URP (Forward+), Input System (new only), AI Navigation 2.x, UI Toolkit.
Developed headlessly through the **Unity CLI** (`unity command …`) against a live editor.

## 1. Guiding decisions
1. **One scene, runtime-built missions.** `Assets/_Game/Scenes/Main.unity` contains only a `GameRoot`. Missions are
   authored as **text tactical maps** (`Resources/Missions/mXX.txt`) and built at runtime: geometry, colliders,
   lights, NavMesh, links and entities. Benefits: text-diffable, fast to author, deterministic save/restore (state =
   level id + entity deltas), consistent art.
2. **Pure C# core, thin MonoBehaviours.** Rules that matter (blood math, detection maths, progression, parsing,
   save DTOs, objectives) live in plain classes that EditMode tests cover without play mode.
3. **Data tables in code** (`Vespertine.Data`): archetypes, abilities, skill nodes, missions. They are typed,
   refactor-safe and testable, kept apart from logic.
4. **UI Toolkit, built in code + USS** (`Resources/UI/Vespertine.uss`). Scales by PanelSettings (reference
   1920×1080, match 0.5).
5. **Procedural placeholder audio** (synthesised AudioClips) and **procedural low-poly characters** from primitives.

## 2. Assemblies
| asmdef | Path | Depends on |
|---|---|---|
| `Vespertine` | `Assets/_Game/Scripts` | Unity.InputSystem, Unity.AI.Navigation, Unity.RenderPipelines.Universal.Runtime, Unity.RenderPipelines.Core.Runtime |
| `Vespertine.Editor` | `Assets/_Game/Editor` | Vespertine (editor only) |
| `Vespertine.Tests` | `Assets/_Game/Tests/EditMode` | Vespertine, test framework (editor only) |

## 3. Module map (namespaces `Vespertine.*`)
| Module | Key types | Notes |
|---|---|---|
| Core | `GameRoot`, `GameFlow`, `Services`, `GameEvents`, `GameTime`, `Log` | Bootstrap; state machine MainMenu → Briefing → Mission → Debrief → … |
| Data | `Archetypes`, `AbilityDefs`, `SkillTree`, `BloodQualities`, `MissionCatalog`, `DifficultyDefs`, `Palette` | Static tables. |
| Level | `MapParser`, `LevelData`, `TileDef`, `LevelBuilder`, `GreedyMesher`, `LevelRuntime`, `NavBuilder` | Text → LevelData → GameObjects. |
| Stealth | `LightSystem`, `GameLight`, `NoiseSystem`, `Perception`, `DetectionMath`, `Evidence`, `AlarmSystem` | Simulation services. |
| AI | `Npc`, `NpcBrain` (+ states), `PatrolRoute`, `Combat`, `SearchPlanner`, `NpcVisual` | Ticked by `AIScheduler`. |
| Player | `Vampire`, `VampireStats`, `FeedAction`, `CarryAction`, `LinkTraverser`, `Interactor` | |
| Abilities | `Ability` (base), `AbilityRunner`, `Targeting`, concrete abilities | Data-driven costs/ranges from `AbilityDefs`. |
| Thralls | `ThrallCommander`, `ThrallCommand` | Thralls are `Npc` in Thrall state. |
| Mission | `MissionController`, `Objective*`, `ScriptRunner`, `Barks` | Win/lose, triggers. |
| Progression | `CampaignState`, `Progression`, `Dossier`, `WorldState`, `CampaignEconomy` | Pure C#. `CampaignEconomy` surveys the mission files and simulates the earning curve (X6, D100). |
| Save | `SaveSystem`, `SaveData` DTOs, `ISaveable` | JSON (JsonUtility) in `persistentDataPath/saves`. |
| UI | `UIRoot`, `HudView`, `MenuViews`, `SkillTreeView`, `BriefingView`, `DebriefView`, `SettingsView`, `WorldMarkers` | UI Toolkit. |
| Camera | `TacticalCamera` | Follows whoever is controlled, with an eased look-ahead offset; rotate/zoom. |
| Input / control | `GameInput`, `MoveMath` | InputActions defined in code; overrides persisted as JSON. `MoveMath`: pure camera-relative input, speed easing, link push detection. |
| Audio | `Synth`, `Score`, `AudioManager` | Sound effects and ambience are generated clips played from pooled sources. **Music** has three adaptive layers (calm / tension / alert) driven by `AudioManager.Mood`. A mission's `music =` key selects a `Score` theme: 10 themes, each with its own key, mode, metre, instruments and motif, written as degree patterns (`Pat`), chord progressions (`Prog`) and drum lines (`Beat`). A theme renders on a worker thread, is cached, and swaps in under a short dip. The three layers start on one DSP tick and never stop, and tension/alert are 4 and 2 bars long, so they always land on the calm layer's beat. |
| Visual | `Mats`, `CharacterFactory`, `ConeRenderer` (+ `Stealth.ConeContext`), `ExposureRims`, `LightOverlay`, `FxPool` | |

## 4. Level pipeline
1. `MapParser.Parse(string)` → `LevelData` (header, char grid, entity specs, routes, objectives, script lines).
   Pure, unit-tested.
2. `LevelBuilder.Build(LevelData)`:
   - Classify each cell via `TileDefs` (height, walkable, blocksVision, layer, material).
   - `GreedyMesher` merges same-type rectangles into boxes. One combined mesh per material for render, and box
     colliders per merged rectangle.
   - Spawns lights, props, interactables, NPCs, the player, objective volumes.
3. `NavBuilder` (runtime `NavMeshBuilder`, no NavMeshSurface components): two NavMeshData, one per agent type
   (`Humanoid` id 0, `Vampire` id 7777):
   - Human surface: walls/bars/vents/water are obstacles.
   - Vampire surface: bars and vents are walkable with area **Mist** (only in mist form); thresholds are
     **NotWalkable** via vampire-only `NavMeshModifierVolume` until invited (then the surface is re-baked).
   - Links: `Ladder` (stairs `s`; both types), `Climb` (pipes/ivy `p`; vampire), `ClimbAny` (every
     raised edge, generated every 2 cells; vampire, enabled by area mask at Awakening 4), `Leap` (roof gaps;
     vampire, Awakening 3).
   - Agents traverse links manually (`autoTraverseOffMeshLink=false`) with `LinkTraverser` (climb/drop/leap
     animations).
   - Vampire blockers: un-invited threshold doors and `zone … home=<door>` volumes are excluded from the vampire
     sources. Inviting a door calls `RequestVampireNavRebuild` → `UpdateNavMeshDataAsync`; when the async op
     finishes `NavBuilder.Tick` removes and re-adds every vampire link (the async update disconnects them).
4. `LevelRuntime` registers every entity by **stable id** (from the map file, or generated as `type@x,y`).

## 5. Simulation
- **AIScheduler**: NPC perception ticks at 10 Hz, staggered across frames; movement/brain logic at frame rate
  but light. Vision raycasts only when the target is inside cone range and angle.
- **LightSystem**: a registry of `GameLight` (radius, intensity, kind, group, on/off) plus a uniform spatial
  hash (4 m buckets). `SampleLight(pos)` iterates nearby lights with an LOS raycast against the `Wall` layer,
  and results are cached per frame for the player position. Visual Unity lights are spot (downward/outward) with
  shadows so that the rendered pools match gameplay LOS.
- **NoiseSystem**: `Emit(pos, radius, kind, source)` → NPCs within radius (walls halve) receive stimuli.
- **DetectionMath** (pure): `Visibility(distance, near, far, light, elevated, looksUp)`, `FillRate(...)`.
- **Evidence**: corpses, dazed bodies and blood stains register in `EvidenceRegistry`; NPC perception checks them
  at 2 Hz.
- **AlarmSystem**: level alert level (Calm, Alert, Lockdown), bodies found, bells.
- **Rescue states** (`AI/NpcEscort.cs`, partial `Npc`): `Captive`, `Escort` and `Amok` are appended to `NpcState`
  (saved as an int, so new states must always be appended). `Npc.Rescue` covers all three. Every state-entry method
  (`EnterSuspicious`, `EnterInvestigating`, `Spot`, `Alert`, `EnterSearching`, `EnterPanic`, `EnterDistracted`)
  returns early when `Rescue`, so only the escort code moves a prisoner between states. A `Shackles` interactable
  follows each prisoner (free, wait or follow). `AIDirector.RescuesLoose` gates the guards' `ScanRescues` pass, so it
  costs nothing until a prisoner is out. `LightSystem.BurnAt` drives the fledgling balk (a probe 1.2 m along the
  steering target, then hold for 1.5 s) and `FleeBurn` (back to the last dark position).
- **Exposure Field** (`Stealth/ExposureField.cs`, D137): `LightSystem.Field`, baked on first use in a level.
  0.5 m cells (`ExposureGrid`, pure and tested: 4 per tile, row 0 north). Each cell holds up to 6 `ushort`
  entries: a static light that reaches it and a "partial" bit when a wall cuts it there. The bake linecasts from
  each light to the cell's centre (top + 1 m) and to the eight corners of its read box (0.03 m outside the cell,
  ±0.2 m), only for cells within the light's flat reach; the moon needs no linecasts. `LightAt(feet)` adds
  falloff from listed lights that are on (linecast for partial entries only), then the moving lights (lanterns,
  searchlights, sunbeams, and lamps promoted after 3 moves), and applies Gloom and ambient exactly as
  `LightSystem.LightAt`. `Sync` (once a frame) re-bakes a static lamp that moved more than 0.02 m over its old and
  new cells. Off-grid points, feet more than 0.2 m from the cell's top and full cells fall back to
  `LightSystem.LightAt` (counted as `Fallbacks`). `DevExposureCheck.Run()` is the SR.3 honesty test in a loaded
  mission (as lit, a third of lamps off, a moved lamp): it must report 0 points over 0.02.

## 6. Save system
- `SaveData { meta, campaign, mission? }`.
  - `CampaignState`: difficulty, current mission index, vitae, marks, unlocked nodes, loadout, dossier counters
    and countermeasures, world flags, terror/rumour, per-mission bests/challenges.
  - `MissionSnapshot`: mission id, elapsed time, player (pos, hp, blood, humour, carried), NPCs (id, alive state,
    pos, rot, brain state, patrol index, wary, thrall, escort wait, loose), lights (id, on, snuffed, broken), evidence (spawned bodies and
    stains), objectives (state), triggers fired, script flags (including `#swap|x|y|type` prop swaps, re-applied after the
    rebuild), script timers, searchlight pool position and sweep index, alarm level, ability cooldowns, plan.
- Restore = rebuild the level from its file, then apply the snapshot (entities by id; dynamic evidence
  re-spawned).
- Slots: `quick0..2` (rotating; quickload takes the newest; a legacy `quick` still loads), `auto0..`, `slot1..8`
  (D152). `MissionController.CanSave` refuses while `WatchedBy` finds a hostile human who `SeesPlayer`; `SavedAt`
  (mission time of the last save or load) drives the HUD's `save-age` line.
- Files are `persistentDataPath/saves/<slot>.json` with a JSON header for the
  load menu.
- Settings are separate (`settings.json`), and input overrides are in settings.

- **DevSandbox (editor only):** `SaveSystem.DevSandbox` lives in `SessionState`, so it lasts until the editor closes. While it is set, `BaseDir` is
  `<project>/Temp/DevSaves`, never `persistentDataPath`. Automation turns it on; ordinary play in the editor leaves it off and uses the real folder.
- **Codex:** `CampaignState.Bestiary` holds archetype ids. `MissionController.TickCodex` checks twice a second for NPCs within 18 m that a wall linecast from
  Ilse's eye doesn't block. `Data/Codex` builds every page's text from the `Archetype` itself, plus a hand-written `Lore` line.

## 7. Input
`GameInput` builds an `InputActionAsset` in code (maps: `Gameplay`, `Camera`, `UI`). Rebinding uses
`PerformInteractiveRebinding`, and overrides are saved with `SaveBindingOverridesAsJson`. UI Toolkit navigation
uses its own event system; gameplay checks `UIRoot.PointerOverUI` before handling clicks.

**Direct control (D114).** `Vampire.ReadMoveInput` turns `Move` (WASD composite + left stick) into a flat world
direction with `MoveMath.CameraRelative(input, cam.Yaw)` and hands it to Ilse, or to `SelectedThrall.DirectMove`.
- **Ilse** (`Player/VampireMovement.cs`): `TickDirect` eases speed (`MoveMath.Ease`), calls `Agent.Move` (plus a
  small separation push away from living humans), and turns toward the input. `NavMeshAgent.velocity` stays zero
  under `Move`, so a measured `_actualSpeed` drives the pose, footsteps and noise. Any key push clears a pending
  feed, a use or a channel, and leaves a hiding spot.
- **Shadow Dash (D155)** (`Player/VampireDash.cs`): `Update` runs `TickDashClock` every frame (charges refill through
  `MoveMath.DashRecharge` while `SightLight` < 0.35; the Umbral second runs down). `TickDirect` starts a dash on
  `GameInput.Dash` (or `DebugDash`) and, while `Dashing`, hands the frame to `StepDash`: `FindTraverse(dir, dash: true)`
  looks only for a Climb/ClimbAny link (taken if `MoveMath.DashRises`) or, with `shade.dash_bars`, a Mist link over a
  Bars tile; otherwise up to 30 m/s of `Agent.Move` in ≤ 0.5 m steps through `Door.ClampStep`, ending when 6 m is
  spent or a step gets under a quarter of its length. `TickMovement` emits no step noise while `Dashing`;
  `UpdateStats` zeroes `VisibilityMul` while `DashVeiled`; `Blur` calls `Npc.Glimpse` once per dash in light.
- **No auto-walk (D129)**: `Act` resolves at once (`VampireActions.Resolve`). A target out of reach is refused with
  `TooFar(d)`. Feeding within `LungeRange` (2.6 m) of a victim beyond `FeedRange` is a `Lunge`: a NavMesh.Raycast
  check, then `ManualLeap(hit, 0.25 s)` whose after-leap callback starts the feed. Reach helpers (`FeedGap`, `CarryGap`,
  `InUseReach`, `InSnuffReach`, `VampireAbilities.RangeProblem`) are shared by `Act`, `ValidateAim` and the HUD's
  greyed prompts (`UIHud.FarVerbs`). Only a feed pressed during a link is held as `_pending` and resolved when the
  link ends (drop-feed). `Act(Move)` still paths, for scripts and dev runs.
- **Links**: every 0.1 s (every frame while pushing) `FindTraverse` scans the level's vampire links that her area
  mask allows, both ends of two-way links, with `MoveMath.PushesInto` (height within 0.9 m, flat reach 1.1 m or 2.2 m
  for leaps, heading dot ≥ 0.55, wide links entered where she stands). Priority Mist > Leap > Ladder > Climb >
  ClimbAny > Jump. Pushing 0.18 s (0.3 s for drops) or pressing `Traverse` calls `StartTraverse`: the agent's
  `updatePosition` goes off and the same `SetupLink`/`TickLink`/`EndLink` code as path-driven links runs; `EndLink`
  warps the agent back (`AfterManualLeap`).
- **Thralls** (`NpcInteractions.TickDirect`): `DirectMove` is applied with `Agent.Move` at walk/run speed; any push
  drops a standing order (follow, an errand). Pushing into a human ladder link for 0.18 s becomes `OrderMove(top)`,
  which the agent's own link traversal finishes. Uncontrolled thralls hold (`ThrallOrder.None`) or follow.
- **Gait and footsteps (D119)**: `Gait` is Sneak (Ctrl held, `SneakSpeed` 2.2 m/s, D128), Walk or Run. Each step
  (`MoveMath.StepInterval`) emits a `Footstep` noise of `MoveMath.StepNoise(gait, wading, carrying)` metres and a
  faint ring; sneaking on dry ground is silent.
- **Doors (D121)**: `Door : Interactable`, shut by default. A child `shut` collider on layer `Door` (19) stops sight
  (`Layers.SightMask`, `VisionBlockMask`) and muffles noise (`NoiseSystem.Occluded`), but not light. Doors stay off
  the navmesh, so humans path through them: `Door.Scan` (10 Hz) opens a door for any living human walking at it and
  shuts it 0.8 s after the doorway clears. Direct movement (Ilse and steered thralls) runs every step through
  `Door.ClampStep`, which removes the component into a shut panel (`BlockStep`, pure). When Ilse follows an agent path
  (`Act`), the door opens for her as it does for humans. Saved as `B2`.
- **Nearest use (D122)**: `VampireActions.NearUse` (cached 0.1 s) picks the nearest enabled interactable within 2.6 m,
  favouring her facing, usable things, and hiding places while she carries. The interact key and the HUD verb and prompt
  use it; for an unusable target the key reports `Interactable.Unavailable`.
- **Camera**: `FollowTarget(t)` on mission start, load and every control switch. Arrows, MMB drag and (opt-in) edge
  pan add to a look-ahead `_peek` (max 22 m) instead of breaking follow; `EasePeek` eases it home once the followed
  character moves faster than 0.5 m/s. `FocusOn` (mission scripts) sets the peek while following.
  Default distance 20 m. A velocity lead (`LeadFor`: velocity × 0.6 s, max 5 m, SmoothDamp 0.4 s) adds to the pivot,
  none while Ilse sneaks within 10 m of a hostile. Z/X go through `RotateStep`: a tap under 0.18 s snaps to the next
  45° (`SnapYaw`), a hold turns smoothly. `VampireMovement.ReadMoveInput` reads the keys against
  `MoveMath.FrameYaw`, which keeps the yaw from when the keys went down until they are released or turn 15° (D153).
- **Pause** (`P`) sets `Game.TacticalPaused`; `Vampire.Update` returns early at dt = 0, so no orders are taken.
- `SettingsData.Version` 1 migrates older files: edge pan off and binding overrides cleared (the old WASD camera
  overrides would collide with movement).

## 8. Rendering
- URP PC asset: Forward+, additional light shadows ON (atlas 4096, low-res tiles), soft shadows low.
- Global volume: bloom, vignette, colour adjustments (desaturate, cool), film grain, tonemapping ACES.
- Materials are created at runtime from URP/Lit and URP/Unlit (`Mats` cache, palette-driven), with
  `enableInstancing`.
- **Vision cones** (`ConeRenderer`, D134–D135, D138–D141): one pooled dynamic mesh per shown cone, rebuilt at 20 Hz.
  36 rays × 30 vertices: the origin arc (two vertices), near-sector fill, a doubled vertex where the grace fringe
  starts (so the fringe begins with a hard step), the fringe, a solid edge strip, 20 far-band samples (about 0.5 m apart), and the end strip.
  Rays in the outer 10° (×`DifficultyDef.Grace`) are drawn at half strength, alternate rays dimmer, to read as
  dashed. Rays are clipped by `LightBlockMask`. Far samples read the Exposure Field (`LightSystem.LightAt` before
  it exists) through a 0.5 m cell cache cleared every 0.25 s, so overlapping cones share samples. Searching cones
  are red at 60% with a dashed end arc; blinded cones are grey and dashed. Each frame `Ownership()` passes the shown
  cones' "sees her this tick" and meters to the pure `ConeContext.Owner`; the owner's renderer gets `sortingOrder` 1
  and the rest rebuild at `ConeContext.Yield` (0.5). Pins are a list (`ConeContext.TogglePin`, max 3). Which cones show is the pure `ConeContext.Pick` (tested):
  `ConeRenderer` measures each seeing guard's gap with `DetectionMath.DistanceToSector` (near sector; the whole cone
  if she is lit, or the far band + 2 m), from her feet and from feet + velocity × 1.5 s. Cones fade over 0.25 s and
  hysteresis holds them 1.5 s; inspected and Alt cones are instant. Touch circles are unit discs scaled to
  `Peripheral`. Colours come from `ConeRenderer.StateColor`.
  **Roof deck** (D141): each cone has a `ConeDeck` child mesh (36 rays × 30 samples). A ray stopped by a wall keeps
  sampling past it every 0.5 m on `Grid.WalkTop` tiles at least 1 m above his feet, and draws where `Classify` gives a
  band and `Npc.LineOfSight` reaches her at 1.0 or 1.6 m. Seen/unseen changes are bisected 4 times, and the first hidden
  sample after a seen one ends the ray. Contextual cones count the far band only when `ConeContext.FarReaches`.
  **Time to contact** (D146): `Contextual()` also runs the pure `ConeContext.TimeToContact` per guard (agent velocity,
  turn rate from `YawRate`, her smoothed velocity) into `Candidate.Soon`/`ContactIn`; `ConeRenderer.ContactIn(n)` feeds
  the HUD's `MeterRead.EdgePip(..., soon)`.
- **Accessible cones** (D147): `SettingsData.AlwaysAllCones`, `ConesKeyToggles`, `ShapeCodedCones`. The pure
  `ConeRenderer.ConeKey(ref latched, ...)` turns the Alt key into held or latched; `AllShown` (which the HUD and intent
  paths read as "tactical view") follows only the key, while *always show all* only adds Full cones to `_want` (and
  fades them in, since `instant` follows the key). `OriginShapeOf(NpcState)` picks the origin arc (Plain 0.25–0.5 m,
  Dashed/Wide 0.1–0.85 m, Dashed blanking every other 6 rays); `EdgeWidth(w, hc)` doubles `EdgeW`/`EndW`;
  `Pulse(t)` scales a Suspicious cone's alpha. All tested in `ConeAccessibilityTests`. The touch circle's rim width
  is fixed geometry and is not doubled (it gets the high-contrast alpha only).
- **Readability Test harness** (D148, §44.1): the pure `Stealth/ReadTest` holds the log types (JsonUtility), the
  scoring (`ArcError`, `NearError`, `ContourError`, `TraceSafe`), `StopClock`, `ProbeDue`, `Next` and `Verdict`/`Report`;
  all tested in `ReadTestTests`. `Core/ReadTestRunner` (a GameRoot child) does nothing unless
  `SettingsData.ReadabilityTest`. It opens a log when a mission starts and writes it on the mission's end (or quit) to
  `SaveSystem.BaseDir/readability/<mission>_<time>.json` (inside `Temp/DevSaves` under DevSandbox), then rewrites
  `report.txt` from every log there. A probe's `Prepare(q)` computes the truth at that frame and freezes the geometry
  in closures; the probe screen is a `UIManager` screen (`pausesGame`, `hideBelow: false`), transparent for the 2 s
  freeze, then black. Input is read from the Input System devices directly: Y/N or A/B; a click or the pad's stick
  cursor and A, projected onto the subject's ground plane; a held trace; digits or the d-pad for Q7. Route choices come
  from the `routechoice` script action (offers survive until the mission ends; the first zone she enters is her
  choice).
- **Movement Test kit** (D156, §44): the pure `Controls/MoveTest` holds the log, `StickClock`, `TurnClock` and
  `Verdict`/`Report` (tested in `MoveTestTests`). `Core/MoveTestRunner` (a GameRoot child) does nothing unless
  `SettingsData.MovementTest`. It records only while play is running and unblocked: `IntentSpeed` against
  `DirectSpeed`, with `Dashing`, `Busy`, `Concealed`, `TraverseLink` and a scripted path exempt, and
  `Vampire.DoorHeld` (set in `TickDirect` when `Door.ClampStep` cut the step under half its length) marking a door stop.
  It also reads `Game.Cam` yaw and distance, each `Npc.InGrace`, and `GameEvents.SpottedCaption`. When
  `Mission.Ended` it pushes a black `movetest` screen (`pausesGame`) over the debrief for the two questions, then
  writes `SaveSystem.BaseDir/movement/`. `ReadTestRunner.PadInput` is the shared pad-or-keys check. **Test maps**:
  `GameRoot.StartPlaytest(id)` stashes `SaveSystem.RootOverride`, `Profile` and `Game.Campaign`, points the root at
  `BaseDir/playtest`, deletes and recreates profile 0 there, and starts the map. `AfterDebrief` and `GoToMainMenu`
  call `EndPlaytest`, which restores all three. The test logs use `BaseDir`, so they land beside the real saves.
- **Cone truth sweep** (D149, SR.3): `Core/DevConeCheck` (dev) calls `ConeRenderer.DrawFor(npc)`. That builds a full
  cone into a pooled mesh, then reads back each ray's near end, reach and far end, and which far samples drew lit
  (`Cone.Lit`, `Cone.FarOn`). The pure `DevConeCheck.Read(drawn, point)` answers Near, Far, Off, or Edge where
  neighbouring rays or samples disagree (tested in `ConeTruthTests`). `Run` compares that answer at random navmesh points
  on the guard's ground with `Npc.SeenBand` at the judged light (leaves included). It skips points within 0.4 m of a
  boundary in the rule. `Tools/cone_check.sh [points] [seed]` runs it in play mode. Far-band samples use
  `GroundLight`: the cached cell light, dimmed as hers is among leaves.
- **Cone hatch** (D150): the cone mesh renderer uses `ConeRenderer.HatchMat`, a copy of the overlay material with the `_HATCH` keyword (`Shaders/Overlay.shader`, `multi_compile_local`). Vertices carry `uv2.x` = hatch weight (1 on dark far-band samples within 8 m, else 0); the fragment multiplies alpha by antialiased diagonal lines in world x+z. The deck and circles keep the plain material.
- **Hover watchers** (`Visual.HoverWatchers`, D150): with `HoverValid` and nothing hovered, every `ConeRenderer.Seeing` guard within `FarRange` + 0.5 m whose `SeenBand(point, 1)` is not None gets a feet ring (overlay layer, `StateColor`). `HoverWatchers.Covers` is the test.
- **Dossier** (D154): `CampaignState.UpdateCountermeasures` runs after every mission. `Counter.Tonight` (cleared there)
  tells it which habits she repeated; an `Answer` per countermeasure holds the calling habit, its count then, and
  missions idle. Lapse at 2 idle (the habit's weight halves); fill free slots from M03 (`DossierStartsAt` 2) up to
  `MaxCountermeasures` 2, one per `Habits.CounterTree`. `AnswerSource` gives the briefing line.
- **Intent paths** (`Visual.IntentPaths`, D146): one overlay mesh rebuilt each frame from `NavMeshAgent.path` corners
  of walking Investigating/Searching guards; dot placement is the pure `IntentPaths.Dots` (tested).
- **Exposure rims** (`ExposureRims`, D132): one 56-segment ring mesh per light near Ilse, at
  `LightSystem.ExposureRadius(l)`. That is `DetectionMath.ExposureRadius`, the closed form of the 0.35 contour of
  `LightFalloff` at chest height: d = R·√(1 − (0.35 − ambient)/(I·scale)), ground = √(d² − (H − 1)²). Spokes are
  linecast-clipped like the burn ring. A rim rebuilds when its radius moves 5 cm, and portable lights every 0.2 s.
- **Fitted Unity lights** (`GameLight.FitUnityLight`, D133, D145): once, after the burn ring is built, each light gets
  a radial knee cookie (`LightKnee.Cookie`, `GameLight.FitSpot`; RGBAHalf 128², destroyed with the light) and its spot
  angles, range and intensity from the contour (`LightKnee.Shape`). Point lights get a child `pool` spot at the flame
  that carries the cookie and the budgeted shadow (`GameLight.Shadowed`); the point light is cut to `GlowRange` 2.5 m at
  `GlowShare` 0.4. Set the outer `spotAngle` before `innerSpotAngle`: Unity clamps inner to the current outer.
  `PoolBright` (2.2) is the pool's brightness per unit Intensity. `FitToExposure = false` keeps the authored lights.
  `Tools/knee_check.sh <light> [before]` measures a pool's rendered edge against its contour.
- **Overlay alpha**: `Overlay.shader` blends in linear space, so a vertex alpha of 0.22 reads as about 50% on dark
  ground. Keep overlay fills at or below 0.15.
- **Spotted caption** (D136): `Npc.Perceive` records a `DetectionMath.SightCause` for whichever sense raised the
  meter (vision band, distance, light, modifiers; searchlight; smell). `DetectionMath.Explain` formats it (tested).
- **Ilse's ground disc** (`IlseDisc`, D142): one mesh at her feet, two submeshes: the fill and rims depth-tested, the
  watcher ticks and toe on the x-ray overlay (ZTest Always) so her body doesn't hide them. Rules are the pure
  `StepRead` (tested). Exposure is `Vampire.SightLight` (`Vampire.JudgedLight`: ×0.25 among leaves), the value
  `Npc.Perceive` gives the far band. The toe runs every 0.05 s (unscaled): the step point is
  `feet + MoveIntent × StepRead.Reach(IntentSpeed)`, clipped by `NavMesh.Raycast`; its light is the field's `LightAt`
  with the same leaf factors; each watcher's band now and next comes from `Npc.SeenBand` (cone + LOS at 1.0 or 1.6 m),
  the same call `Perceive` makes, and `Npc.Overlooks` (masque, Stalker) drops guards who wouldn't count her.
  Rumble: `Gamepad.SetMotorSpeeds` for 0.12 s, only while the left stick is in use.
- **Meter reading** (D143): pure `MeterRead` (tested) picks the fill pattern (`FeedOf`), the glint (`GlintOf`), the
  start (`Started`, 0.05) and the edge pip rule and position (`EdgePip`, `EdgePoint`). `Npc` exposes `MeterFeed`,
  `HeardAt` (set in `Hear`) and the static `MeterStarted` event, raised after the meter step. `BoundaryGlint` takes the
  shape when the meter starts and holds it 0.3 s unscaled (`Hold` adds seconds for gym screenshots). In `UIHud`,
  `GetMeter`/`FillMeter` serve both the overhead pool and `_edgePool`; patterns are generated textures on a 28 px
  `det-pat` child inside the fill (overflow hidden), tinted with `unityBackgroundImageTintColor`. Edge pips are placed
  with `RuntimePanelUtils.ScreenToPanel`; `EdgeArrow` repaints only when its direction, facing or colour changes.
- **Spotted picture and Detections** (D144): `BoundaryGlint` listens on `GameEvents.SpottedCaption`; shots carry a
  `Life` and a `Held` flag (steady, fading in the last 0.6 s), and `Picture` adds the spotter ring and sight-line each
  frame. It sets `ConeRenderer.Spotter`/`SpotUntil`, which force his cone to `Full`, instant, and owner of overlaps.
  `MissionController.OnSpotted` stores `SpottedRecord.Take` (linecasts on `LightBlockMask`; capped by `Keep`) in
  `Detections`, copied to `MissionResult.Detections`. `SpottedRecord.Bounds/Fit/Map` are pure (tested);
  `UI.DetectionSketch` draws with Painter2D on `generateVisualContent`. Records are not part of the save.
- **Audio listener** lives on its own `Ear` object under GameRoot. `AudioManager` parks it above the camera pivot
  every Update; while it sat on the camera, that moved the camera too, and anything projecting to the screen in
  Update saw a level camera 8 m above the pivot until `TacticalCamera.LateUpdate` put it back.
  `UIManager` listens on `GameEvents.SpottedCaption`, which `RaiseSpotted` fires alongside `NpcSpottedPlayer` and
  which mission teardown does not clear.
- **SenseRenderer**: x-ray (ZTest Always overlay) heartbeats for Blood Sense (all within 30 m, plus NavMesh path
  lines), Scent of Blood (dazed, wounded, hounds, stains) and Dread Feast (`Npc.RevealedUntil`). Pooled quads,
  per-renderer MaterialPropertyBlock tint; pulse speed follows the NPC's state.
- Mission fog is exponential-squared, set per mission (`fog`, `fogColor`).
- World markers (awareness "?"/"!", interaction prompts) are UI Toolkit elements positioned by world→panel
  transforms.

## 9. Performance budgets
- ≤ 60 active NPCs per mission; perception ≤ 0.5 ms/frame on desktop. (M11's opera runs 74–79, audience included.
  That is measured and fine: see below.)
- Merged level geometry: < 150 draw calls before characters.
- Lights: ≤ 64 gameplay lights, ≤ 24 shadowed visible.
- No per-frame allocations in AI tick (pooled lists; non-alloc physics queries).
- **Measured** (2026-10-02, shipping build, `smoke.sh -uncapped`, RTX 3070, 1920×1080):
  - Calm openings: median 1.3–2.0 ms.
  - Hunted (lockdown, every NPC searching, reinforcements in): median 1.4–3.5 ms.
  - Worst: M11 hunted, 79 NPCs, p95 4.4 ms.
  - 720p gives the same numbers, so the frame is CPU-bound.

## 10. Testing
- **EditMode** (`Vespertine.Tests`): parser, detection math, blood/feeding rules, progression (vitae → awakening,
  node prerequisites), dossier/countermeasures, save round-trip, objective logic.
- **ContentTests**: skill tree references, archetype sanity, `MissionValidator` over every shipped mission file,
  objective-category semantics.
- **Play verification** via CLI: `editor_play`, `simulate_key/pointer`, `capture_game_view`, `console`.
- A **debug console** (`~`) in development builds: `mission mXX`, `god`, `blood N`, `awaken N`, `reveal`,
  `win`.
- **`DevAbilitySweep.Run(part)`** (Editor/DEBUG only, `Core/DevAbilitySweep.cs`): a play-mode check of every active
  ability and most passives/mods/Awakening gates in a loaded mission. It grants all nodes, picks unused calm humans,
  places Ilse behind them, casts through the normal `Cast` path and asserts the result (state, damage, blood, noise,
  nav mask). The campaign is restored afterwards. Part 1 runs on one mission (M05 covers it all except the
  priest check, which needs M04). Parts 2–5 each need a fresh mission load, because their preconditions (unlit lamps,
  calm witnesses, hounds, calm citizens) are spoiled by part 1: 2 Lingering Dark + Terror (M02), 3 Dread Feast (M02)
  + False Trail (M08), 4 direct control: WASD walk, a thrall under her hand (camera follow, Ilse holds, follow/hold, strike, hand back),
  climbing by a push (M02), 5 dread presence (M06). Results go to `Report` and the console
  (`[sweep] PASS/FAIL/SKIP …`, `[sweep] DONE x passed, y failed`); a SKIP means the mission lacks a suitable target.
- **`DevCampaignRun.Run(prefer, optionals)`** (Editor, `Core/DevCampaignRun.cs`): plays a throwaway campaign from
  M01 to an ending through the real flow: mission start, script rules, the debrief's Continue (`AfterDebrief`),
  blood-dreams, and the ending. It does not sneak. It drives each night's objectives in the order the mission reveals
  them, optionals first:
  - Interactions go through the entity's own `Use`, so secrets, documents and levers fire their rules.
  - Reach and escape areas are entered by teleporting Ilse into them, which also exercises the K18 tripwire.
  - Everything else is completed directly.
  - Scripted choices are answered through the `UIManager.AutoPick` seam, taking the first key in `prefer` that is
    offered (e.g. `"burn,destroy"`).
  - The report lists, per night: Vitae and Awakening, Marks, new campaign flags, blood-dreams and logged errors. It
    ends with the ending reached. Saves go to a scratch root.
- **`DevFrameProbe.Run(seconds)`**: median, p95 and worst frame time, mono heap growth per frame, NPC and light
  counts. Editor numbers include Editor overhead; use them to compare missions, not as a budget.

- **Player smoke test** (`Core/SmokeTest.cs`, in every build): `Vespertine.exe -smoke [-uncapped] [-smokeout file]`
  starts a throwaway campaign (saves go to `SaveSystem.RootOverride`, a scratch folder) and loads all 14 missions
  in turn. For each one it checks that the mission started, the player spawned, NPCs registered and no error or
  exception was logged. It samples frame times calm, then hunted (lockdown, every NPC searching at Ilse in god
  mode). It writes the report, and quits with code 0 on PASS and 1 otherwise. `-uncapped` turns vsync off, so the
  frame times are real.

- **UI automation:** `Core/DevUi` lists the visible buttons and clicks one by label. Simulated keyboard input does not reach the
  game while the Game view is unfocused (K27), so key-driven paths are exercised by calling their handlers.
- **Test result files:** `Editor/QuietTestResults` strips the performance package's `PerformanceTestRunSaver` callback after each
  domain reload, so test runs write nothing to `persistentDataPath`.

## 11. Builds
- `Tools/build.sh`: Windows x64 player via the live Editor's Pipeline `build` command (async, polled with
  `build_status`) into `Builds/Win64/`. Only `Main.unity` ships.
- `Tools/smoke.sh [-uncapped] [unity player args]`: runs the smoke test on that build and prints the report.
- With no Editor open, batch mode works too:
  `Unity.exe -batchmode -quit -projectPath … -executeMethod Vespertine.EditorTools.VespertineBuild.Windows`.
  The result goes to `Builds/last_build.txt`.
- The build's ten warnings are expected. Nine are UAC1001 (runtime-only fields that are deliberately not
  serialised); one says the Pipeline is disabled in players. URP also logs that the DoF and Panini shaders were
  stripped: the game doesn't use those effects.

## 12. CLI workflow (dev notes)
```
U="/mnt/c/Program Files/Unity Hub/resources/cli/unity.exe"
"$U" command recompile ; "$U" command recompile_status
"$U" command console --level error
"$U" command run_tests --mode EditMode
"$U" command editor_play ; "$U" command capture_game_view --save_path …
```
