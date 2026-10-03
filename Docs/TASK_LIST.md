# VESPERTINE — Task List (authoritative)

Status: `[ ]` todo · `[~]` implemented, not yet fully play-verified · `[x]` done (player-facing behaviour verified) · `[!]` blocked

## Phase 0 — Foundation
| ID | Task | Acceptance | Deps | Status |
|---|---|---|---|---|
| F1 | Project setup: folders, asmdefs, layers, NavMesh areas + Vampire agent type, Main scene + GameRoot | Compiles; Main scene in build list; layers present | – | [x] |
| F2 | Design docs | All 10 docs exist and agree | – | [x] |

## Phase 1 — First playable (core loop in M01)
| ID | Task | Acceptance | Deps | Status |
|---|---|---|---|---|
| C1 | MapParser + LevelData + tests | Parses header/map/entities/routes/objectives/script; tests pass | F1 | [x] |
| C2 | LevelBuilder: tiles → merged meshes + colliders; palette materials | m01 renders with walls, water, floors | C1 | [x] |
| C3 | Runtime NavMesh (2 agent types) + links (ladder/climb/climbany/leap) | Vampire climbs pipes; humans use stairs only | C2 | [x] |
| C4 | TacticalCamera (pan/rotate/zoom/follow, bounds) | Smooth, clamped to map | F1 | [x] |
| C5 | GameInput (actions in code, rebind persistence) | All default bindings work | F1 | [x] |
| C6 | Vampire: direct WASD movement (was click-move, D114), run (Shift), link traversal by push or Space, noise | Moves, climbs, drops; running emits noise | C3,C5 | [x] Swept (part 4: walk, climb by push) |
| C7 | LightSystem + GameLight + visual spot lights | Light level sampling matches visuals; snuff | C2 | [x] |
| C8 | Perception (cone near/far bands, vertical rule, detection meter) + tests | Meter fills per rules | C7 | [x] |
| C9 | NPC brain: patrol/post, suspicious, investigate, alert (shout/shoot/chase), search, wary, panic | Full state loop observable | C8 | [x] |
| C10 | Vision cone renderer (inspect/hover, Alt for all) | Two-band coloured cones with occlusion | C8 | [x] |
| C11 | Feeding (Sip/Drain), blood qualities, humours; Dazed wake & report | Blood/HP change; victims behave | C9 | [x] |
| C12 | Bodies: corpse, carry, drop, dump in canal, hide spot; blood stains; evidence detection | Guards react to evidence | C11 | [x] |
| C13 | Health/damage/death; regen-in-dark with blood | Death → fail screen | C9 | [x] |
| C14 | Objectives + MissionController + script triggers + barks | M01 completable start→end | C1 | [x] |
| C15 | HUD (health, blood, light eye, objectives, ability bar, prompts, awareness markers, bark subtitles) | Readable at 1080p & 1440p | C11 | [x] |
| C16 | Save/Load (quick/auto/slots), mission snapshot restore + tests | F5/F9 restore exact state | C14 | [x] |
| C17 | M01 *The Drowned Ward* authored | Playable 10–15 min, 3 routes | C14 | [x] |
| C18 | Audio service + synthesized SFX (steps, heartbeat, alert stings, feed, bells, ambience) | Cues audible & positional | C9 | [x] NPC footsteps by surface near the camera (hounds pad, Bulwarks clink), positional brazier/bonfire crackle (4 nearest), lamp relight ignite; D112 |
| C19 | Post-processing + art palette pass | Ink & Ember look | C2 | [x] |

## Phase 2 — Campaign framework
| ID | Task | Acceptance | Deps | Status |
|---|---|---|---|---|
| G1 | CampaignState + progression (Vitae/Awakening/Marks) + tests | Correct thresholds/unlocks | – | [x] |
| G2 | Main menu (new/continue/load/settings/quit), difficulty select | Navigable, styled | C15 | [x] Play-verified via DevUi: New/Continue/Load/Settings/Credits/Quit, difficulty select, Load enabled once a profile exists |
| G3 | Briefing → loadout → mission → debrief flow | Full loop | G1 | [x] |
| G4 | Skill tree screen (4 trees, prerequisites, tooltips, respec) | Spend/refund Marks | G1 | [x] Play-verified: learn (Marks 12→10, child unlocks), unlearn-all confirm + refund, scroll kept across rebuilds, gifts hidden until given |
| G5 | Settings (audio, graphics, UI scale, difficulty, rebinding) persisted | Persist across runs | C5 | [x] Play-verified: values persist (sandbox settings.json), rebinding swaps on conflict |
| G6 | Dossier & countermeasures + world flags (terror/rumour, curfew, blackout) | Habit tracking → groups activate | G1 | [x] Play-verified in M07: habits recorded, cm_caged chosen and shown in the debrief and Dossier. cm_caged reworked (D108) |
| G7 | Codex (enemies, lore notes found), Tutorial hints system | Hints trigger contextually once | C15 | [x] Codex hub tab (D107): first sighting within 18 m with line of sight records an archetype (toast), page = Ilse lore + live stats/traits/blood; lore notes in Journal; hints are script `hint` actions gated by Tutorial hints. Play-verified |
| G8 | Pause menu, fail screen, save/load menus | Works in mission | C16 | [x] Play-verified: pause menu, save to slot, load w/ confirm (position restored), death screen options, abandon confirm. F5/F9 not key-tested (editor key injection impossible, K-list) |

## Phase 3 — Abilities & thralls
| ID | Task | Acceptance | Deps | Status |
|---|---|---|---|---|
| A1 | Ability framework (targeting modes, costs, cooldowns, range rings, loadout) | Bar shows equipped; costs | C15 | [x] |
| A2 | Beckon (+mimic) | Lures target alone | A1 | [x] |
| A3 | Predator: Pounce, Stalker, Gorge, Scent, Rend, Terror, Herd, Bound, Apex, Dread Feast | Each verified | A1 | [x] Swept or played. Herding and Soft Landing are code-reviewed one-liners (K9) |
| A4 | Shade: Smother (+mods), Shadowstep (+bars), Nightblood, Gloom, Mist, Eclipse, Shroud | Each verified | A1 | [x] Swept, incl. Lingering Dark. Gas-main Smother and Shadowstep through bars are code-reviewed (K9) |
| A5 | Dominion: Mesmerize (+Lethe), Enthrall, thrall commands, False Orders, Puppet Strike, Court, Living Lie | Each verified | A1 | [x] Swept. Living Lie now gates puppet False Orders (D59) |
| A6 | Sanguis: Blood Sense, Bloodmend, Clean Feeder, Snare, Hemorrhage, Corpse Puppet, False Trail, Silverblood, Communion, Vessel | Each verified | A1 | [x] Swept; Blood Sense, Hunter's Pulse and Scent checked in play |
| A7 | ~~Nightplan~~ removed after play-testing (D115). Replaced by direct party control of thralls (portrait / T, X follow/hold, 1–4 commands) | Thrall moves under the keys, Ilse holds, strike, hand back | A5 | [x] Swept (part 4) |
| A8 | Awakening capabilities (roof-leap, wallcrawler, drop-feed, dread presence) | Gated correctly | G1 | [x] Leap, wall climb and dread presence (D60) swept. Drop-feed play-verified through a real drop link in M08: a Feed order from a 3 m ledge drained the victim on landing |

## Phase 4 — Enemy roster
| ID | Task | Acceptance | Deps | Status |
|---|---|---|---|---|
| E1 | Archetype visuals (silhouettes per faction) | Recognisable at default zoom | C9 | [x] Checked with `DevLineup` (all 26 archetypes in M07, at default and close zoom). Vigil: pale mantle and coat-tail. Priest: mitre. Alchemist: beak mask and censer. Scholar: mortarboard. Sentry: Watch stovepipe. Fledgling: grey and starved |
| E2 | Lamplighter relight behaviour | Relights dark lamps on circuit | C9 | [x] |
| E3 | Priest holy aura + bells + alarm/lockdown + reinforcements | | C9 | [x] The aura is drawn as a ground ring (`AuraRenderer`, D110), checked in M04. Bells, lockdown and reinforcements were already play-verified in M04 |
| E4 | Hunter (LooksUp, flares, silver), Hound (smell, trails, bark, heel) | | C9 | [x] |
| E5 | Alchemist censer, Inquisitor, Bulwark, Sentry, Squads | Squads done in M13 | C9 | [x] Inquisitors read bodies (D111, ExamineTests). Checked in M08: a staged accident was seen through, the body reported and the Dossier's lethal habit rose by 1.5 |
| E6 | Officers organise searches; paired patrols (paired=/follow= now linked) | | C9 | [x] Checked in M08: Hollin's search pulled in 3 nearby searchers, and a hound found its downed handler and searched |

## Phase 5 — Missions (each: authored, objectives, secrets, script, playtested start→end)
| ID | Mission | Status |
|---|---|---|
| M01 | The Drowned Ward | [x] |
| M02 | Lantern Street | [x] |
| M03 | The Fishmarket Hunger | [x] |
| M04 | The Bells of Saint Corvin | [x] |
| M05 | The Guildhall of Lamps | [x] |
| M06 | Masquerade at Ashcombe House | [x] |
| M07 | The Toll Bridges | [x] |
| M08 | Hollin's Hunt | [x] |
| M09 | Coldwater, Above | [x] Escort/prisoners, fledgling frenzy, sunstone generator + thrall smash, `use` deeds |
| M10 | The Gasworks | [x] Searchlights + operators, countdown/blast/swapprop, evacuation (`evac=`, `evacuate`), gas holder + wreck, citywide blackout flag |
| M11 | The Opera of Lanterns | [x] Crescendo (`crescendo`, hush chip, noise ×0.25), `framed` event + Watch–Vigil split, 90 s window + scatter to the Vigil's coaches, the balustrade accident, cloakroom masks, house lights on the scatter, theatre props |
| M12 | Vane's Bastion | [x] `dossier = all` (full Dossier), `choice` modal + `<id>.<key>` flags, three crossings (weir, skiff, windlass portcullis via an unwarded lay brother), the kill/burn deed, Faithful escort, sunstone grid + generator, ninth shrine |
| M13 | The Long Night | [x] Hunter squads (`squad`, `squad=`, `lead`, shared nerve, wedge, ring, rout, promotion), `break` objective + `broken`/`routed` events + `rout` action, lazy muster/Remuster for script groups, Vane leads the close squad, Candle Row raid countdown, Nell's escort, Tobias world flags, the archive burn wipes the Dossier |
| M14 | The Abbess Beneath + endings | [x] Three stages (nave / undercroft / vault), time-driven `sunbeam` lights, three ways down (west door, bricks deed, sexton's stair via Ashby), Saule (generator-cut ring, purge accident, thrall/spare), vault gate, fledgling escort, the Abbess and the free/consume/destroy choice (destroy = timed escape), flag-driven epilogue, tenth shrine, `moon=` header, `intensity=` on lights |

## Phase 6 — Polish & ship
| ID | Task | Status |
|---|---|---|
| P1 | Interlude blood-dream narrative screens between missions | [x] `Progression.Interludes`: 13 dreams (after M01–M13; the ending follows M14) that branch on campaign flags, Tobias and Terror vs Rumour. Each plays once after a first win (`dream_<id>` flag), line by line over a slow heartbeat, then is filed in the Journal |
| P2 | Mission select / replay with challenges & best results | [x] Seven challenges per mission (`Progression.Challenges`): Unseen, Merciful Hunger, Silent Night, Before the Bell (par), Unbroken, Every Thread, Apex. Recorded per mission and shown in the debrief (NEW on first earn), the Missions tab (n/7 plus badges) and the Record total |
| P3 | Performance pass (profiling, AI scheduling, light counts) | [x] Measured in the shipping build (smoke `-uncapped`): worst case M11 hunted, 79 NPCs, median 3.5 ms, p95 4.4 ms at 1080p on an RTX 3070. CPU-bound; resolution barely matters. Follow-up D113 cut HUD cost (~2 ms a frame in the editor) and garbage (1,284 B to 173 B a frame) |
| P4 | Self-review milestone reports (after M04, M08, M12, M14) | [x] Reviews 1–4 done (PROGRESS) |
| P6 | Campaign-flag carry-over, M01 → M14 | [x] `CampaignFlagTests`: every `campaign_flag` is read later (by a `cf_` condition, a `flag_` group, or code), and every `cf_` read was set by an earlier mission. Seven flags were set but never read; five now pay off (M06, M11, M12, M14 lines and Hollin's epilogue), and two are record-only (D101). Runtime-checked: each new line fires with its flag and stays silent without it |
| P5 | Windows build via CLI, smoke test | [x] `Tools/build.sh` (Pipeline `build`), `Tools/smoke.sh` (player `-smoke`: all 14 missions calm + hunted, 0 errors). 103 MB incl. symbols folder |

## Open follow-ups (from play-testing)
| ID | Task | Status |
|---|---|---|
| X1 | Blood Sense / Scent of Blood / Dread Feast x-ray heartbeat renderer (SenseRenderer) | [x] |
| X2 | Familiar Voice (beckon without Wary), False Trail (runes lure hounds), Clean Feeder sleep bonus | [x] All three have effects, and DevAbilitySweep checks each in play mode (Familiar Voice and Clean Feeder in part 1, False Trail in M08 part 3) |
| X3 | Reinforcements restored from mission saves | [x] |
| X4 | Music clips per `music=` key (currently ambience only) | [x] `Audio.Score`: 10 synthesised themes (drowned, lantern, docks, cathedral, gasworks, masque, bridges, hunt, opera, bastion), each with calm, tension and alert layers in one tempo. Rendered off-thread and kept beat-locked |
| X5 | Validator: check deliver/feed npc ids | [x] Also checks reach/escape/deliver areas and `on feed` |
| X6 | Re-play M01 with guards for difficulty curve | [x] Measured, not hand-played. `CampaignEconomy` surveys every shipped mission and simulates ghost, typical and predator campaigns. Two fixes: objective Vitae was promised but never paid (now +60/+30/+20 on first completion), and notable Marks were farmable on replay (now once per notable). Awakening thresholds are retuned and 5 `CampaignEconomyTests` hold the curve. Pars were checked against map size and primaries and are consistent |
| X7 | Sweep checks for drop-feed and the flag mods listed in K9 | [x] `DevAbilitySweep.Run(6)`: Soft Landing, drop-feed, Herding, Black Main, Between Bars. Each runs with and without the mod. 5/5 pass in M05; M10 has no bars, so that one skips. Escape objectives now honour `needs=`, and a K18 tripwire is in |
| X8 | Dormant `group cm_*` blocks in M07–M11 (extra bodies per countermeasure; the systemic effects already apply) | [x] Each of M07–M11 has four groups: roof/wall sentries, partners on existing beats, an inquisitor loop, and two censer posts. All were verified in play with every countermeasure forced on: every NPC is on the navmesh and Relaxed after 30 s, and none sees the start. `DossierContentTests` holds M07–M13 to this |
| X9 | Whole-campaign flow check: M01 → ending through the real debrief, blood-dream and ending flow | [x] `DevCampaignRun` (`camprun.sh`). Free, consume and destroy runs each reach their ending (night court, pale lady, dawn with Tobias) with 0 errors, 14 nights won and 14 blood-dreams. Found two content bugs: M06 never revealed `study`/`away`, and M03's "first shrine" set no flag (D103). The validator now rejects unrevealed hidden objectives |
| X11 | Play-test feedback on M01 (2026-10-02): bodies read as kills, no sneak, hiding not found, the gate did nothing, doors walked through, M01 too easy | [x] D119–D123. Ctrl sneak and gait footsteps; E interacts with the nearest thing and explains what it can't do; real doors; the gate valve's wheel is taken from the stoker; shrouded dead on slabs. 8 `SneakAndDoorTests` |
| X10 | Play-test feedback (2026-10-02): movement should be WASD, real-time stealth, thralls controlled like a party (click a portrait). | [x] D114–D118. Direct camera-relative WASD with Shift run; climbs, drops and leaps by walking into them or Space; follow camera with look-ahead; click-to-move and the Nightplan removed; P is a plain pause; thrall direct control with a Party panel (Ilse + thralls), X follow/hold, 1 Distract, 2 False Orders, 3 Puppet Strike, 4 Release. Settings v1 migration. 11 `MoveMathTests`, sweep part 4 (6 checks) |

## Phase 7 — Redesign (GAMEPLAY_REDESIGN.md)
Order agreed: quick wins first, then the Exposure Field, the cone redraw and the readability gym, then the WASD
Stealth Readability Test (§44.1). **Gate:** no Hunt or mission tuning until that test passes.

| ID | Task | Status |
|---|---|---|
| R1 | QW1 Shout radius in metres (bug A) | [x] D124. `QuickWinTests`. M07 play check: the worst shout reaches 3 NPCs (mean 1.6 of 36) |
| R2 | QW2 Dazed wake leaves Dazed first, reports once (bug B) | [x] D125. M02 play check: woke at 45 s, one report, searched, then Relaxed and wary |
| R3 | QW3 Water check in `Cast` for movement abilities | [x] D126 |
| R4 | QW4 Arts after M01, Beckon in M02 | [x] D127. M02 waking dream, M03 second contact |
| R5 | QW5 No auto-walk; out-of-range targets greyed with the distance | [x] D129. Lunge 2.6 m. Play-checked feed, use |
| R6 | QW6 Sneak 2.2 m/s | [x] D128 |
| R7 | QW9 Challenges pay +1 Mark first time | [x] D130 (optionals now Vitae only) |
| R8 | QW10 Unbroken = no alarm and no loads | [x] D131 |
| R9 | QW7 Shout ring, near sector as a solid fill, 1.4 m touch circle | [x] D135. Play-checked in M02 (shout ring, touch circles, near fill) |
| R10 | QW15 Contextual cones (4 m or aware, 1.5 s hysteresis, max 4) | [x] D134. `ReadabilityTests` (pick, budget, near-only). Play-checked in M02 |
| R11 | QW16 Exposure rim at the 0.35 contour | [x] D132. `ReadabilityTests` (contour = threshold, table radii). Play-checked in M02 |
| R12 | QW17 Clamp Unity light ranges toward the contour | [x] D133. The authored lamp pools are faint either way: art pass (K34) |
| R13 | QW18 Spotted caption from `Classify` | [x] D136. `ReadabilityTests` (`Explain`). Play-checked in M02 (near-band and lit far-band captions) |
| R14 | QW19 Far-band light sampling every 0.5 m | [x] D135 (20 samples per ray, cached 0.5 m cells) |
| R15 | QW14 Reset dev Dossiers (saves are outside the project: ask first) | [x] 2026-10-03, approved. Nothing to reset: every save (Temp/DevSaves profiles 0–2 and campaignrun, and LocalLow/DefaultCompany/StealthVampire profile 0) has an empty or normal Dossier (largest: lethal 1.5) and no inflated witness counters. The inflated one from Bug B is no longer on disk. No files outside the project were changed |
| R31 | QW8 Save timer + 3 rotating quick slots, no save while seen | [x] D152. `SaveSlotTests` 3. M02: quicksaves rotated quick0→1→2→0, the age line counted, in front of npc_w1 the line read "Watched: can't save" and no file was written, quickload took the newest. Ironblood's +1 Mark not built |
| R32 | QW11 Camera distance 20, tap Z/X = 45° snap, WASD frame locked while held | [x] D153. `MoveMathTests` +3 (`SnapYaw`, `RotateStep`, `FrameYaw`). Gym: distance 20. Not tried with a real key press (simulated keys didn't reach the editor) |
| R33 | QW12 Velocity look-ahead | [x] D153. `MoveMathTests` +1 (`LeadFor`). Gym: 1.6 m ahead walking, 2.5 m running, back on her when she stops |
| R34 | QW13 Dossier from M03, max 2, one per tree, lapses, shown on the briefing | [x] D154. `CampaignStateTests` +3 (one replaced). M03: the briefing listed both answers with their deeds. M03–M06 bodies wait for R22 |
| R35 | §11 Shadow Dash replaces Shadowstep: Q / pad East, 6 m navmesh burst, 2 charges (3 with Umbral Step) refilling only in the dark, blur in light, Rise ≤ 4 m, Between Bars | [x] D155. `MoveMathTests` +3 (recharge, refusals, Rise), `CampaignStateTests` +1 (old save renamed). Gym: 6.0 m per dash, a charge back in 5 s of dark, a closed door stopped her from both sides, a dark 3 m wall carried her up (Awakening 10), a lit one and the 4.5 m pipe stopped her at the foot; the blur took cross_b 0 → 0.30 and no one else. Sweeps: part 1 34/34 (M02), part 6 5/5 incl. `shade.dash_bars` (M05). Pounce through the dash (§24) and the Warm recharge wait for their systems. WASD Movement Test is for players |
| R36 | WASD Movement Test kit (§44): corner-and-door gym, *Movement test mode* log (sticks, door stops, camera, edge detections, two questions), main-menu *Test maps* on a sandboxed campaign | [x] D156. `MoveTestTests` 6. movegym: a complete path start → exit (111 m), the validator passes. Driven in play: 5 s pushed into a wall = 1 stick; steering into the corner at the diagonal's foot = 1 stick; pushing up the jagged diagonal = none; a shut door = 1 door stop, not a stick; a 45° camera snap = 1 turn; at the exit both questions came up over the debrief, and the log and report were written. Test maps → Movement Gym ran in `playtest/`, and Continue restored the real profile and save root (profile_0 not written). The test itself is for 5 new players: [ ] |
| R16 | SR.14 step 1: Exposure Field | [x] D137. `ExposureFieldTests` (grid). `DevExposureCheck` on M01–M14: 0 points over 0.02 (as lit, a third of lamps off, a moved lamp); reads 2–10 µs vs `LightAt` 5–21 µs; bakes 13–68 ms |
| R17 | §10 / SR.5 cone-edge grace and onset; SR.5 state styles (Searching red 60% dashed, Blinded grey dashed) | [~] D138. `GraceTests` 9. Gym, 0.05× time: 40° off cross_a's axis at 5 m, `InGrace`, meter 0.10 and rising slowly; fringe drawn dimmer and broken. Searching and Blinded styles not yet seen in play |
| R18 | SR.14 step 4 rest: cone ownership (rising meter on top, others 50%), outline and hatching on the dark far band within 8 m, origin arc, Mesmerised violet outline | [~] D139. `ReadabilityTests` (owner 3). Gym: the rising guard's cone took sortingOrder 1, the seeker's yielded. Hatch built (D150): world-space diagonal lines on the dark far band within 8 m. Gym: lines over cross_a's dark band, solid over the lit band |
| R19 | Readability gym (`gym.txt`: lamps, doorway spill, shadow wall, crossing cones, Wary guard, seeker, hunter on a roof) and the `investigate` script action | [x] Play-checked: field = `LightAt` everywhere; doorway spill reaches 4 m down the door's axis and the walls either side stay at 0.06; the shadow wall casts; three lamp kinds match their rims; the hunter sees her on the lit roof and his cone now draws there (D141). Map rows had a leading space (fixed). Pins: a 4th pin drops the oldest. Blinded: the cone shrinks to a 0.8 m grey stub (vision range 0.8), so the dashing hardly shows; the stub reads as blind. The carrier's lantern is on. The red Searching style hasn't been seen in play yet (the seeker stays Investigating)
| R20 | SR.14 step 5: pins (middle-click, up to 3), hover, Merciful 6 m contextual distance, Apex limits | [~] D140, D150. `ReadabilityTests` (pins, Merciful, Apex). Ground hover built as a ring at each covering guard's feet (`HoverWatchers`, D150), not a character outline. Gym: cursor before cross_a ringed cross_a and cross_b, as `Covers` said. Pins and hover not yet tried with a real mouse |
| R21 | SR.14 step 2: light knee art pass (K34) | [x] D145. `LightKneeTests` 4. `Tools/knee_check.sh`: M02 lamps, brazier, candles, gate wall lamp: rendered edge within 0.15 m of contour + half feather, contrast 3.5–4.3× (was 1.1×, s1's edge 0.87 m short). Screens: gym shadow-wall lamp and brazier, M02 street and tavern. Not done: occlusion cookies (step 9; would blacken lit facades), blackout refit. Weak: windows and pools among other lights (1.4–1.5×) |
| R23 | SR.14 step 6: Ilse's ground disc, watcher ticks, near warning, toe (SR.7, SR.8) | [~] D142. `StepReadTests` 17. Gym, time at 0.002×: hidden ring; exposed fill with two full ticks and the seeker's short one (meter 0.21); Warm toe 6.2 m south of a lamp (orange); Seen toe into cross_a's lit far band; Flash toe 0.8 m outside his near sector; near warning 0.6 m out. Not built: warm-blood pulse (no Warm in game), breath audio cue. Not seen in play: masque/mist/gloom tints, burn ring, dashed grace tick, rumble |
| R24 | SR.9 meter patterns and boundary glint; SR.11 off-screen edge pips | [~] D143. `MeterReadTests` 15. Gym: striped far-band fill on cross_a (meter 0.77); waves (heard) and dots (smelled) forced on cross_a and cross_b; light-rim glint on the shadow-wall lamp, clipped at the wall; near-arc glint at cross_a's 6 m edge; edge pip on the right edge with the guard off screen, pointer outward, wedge pointing back in (he faces her). Found and fixed on the way: the camera was displaced during Update (audio listener on the camera). Not built: the 2.5 s time-to-contact pip. Not seen in play: the touch-circle glint, a pinned pip at long range, the Alt pip |
| R25 | SR.10 Spotted picture in the world and debrief Detections page | [~] D144. `SpottedRecordTests` 6. Gym: spotted by cross_a in the near band at 4.8 m; his cone full and on top, near arc held in white, ring at his feet, sight-line to her; the debrief Detections page listed both sightings (cross_a, cross_b) with sketches and captions. Not built: Reaction Window slow-mo, dimming other guards, searchlight pool and noise ripple in the world, "what started each Hunt", saving records. Not seen in play: the lamp rim and lamp ring for a far-band sighting, touch circle |
| R26 | SR.5 state marks (Wary ring, dotted Investigating/Searching paths), SR.6 time-to-contact rule and its edge pip | [x] D146. `IntentPathsTests` 3, `ReadabilityTests` +2, `MeterReadTests` +1. Gym: the seeker's amber dotted path with its end ring; Searching paths in dim red after an alarm; the Wary guard's ring; the carrier's contact time 2.0 s at 9 m. Not seen in play: the contact rule being the first reason a cone shows (a turning sentry) |
| R27 | SR.7 one threshold on the HUD eye; SR.5 Suspicious pulse; SR.12 accessibility (always all cones, Alt toggle, shape-coded states, high-contrast widths) | [x] D147. `ConeAccessibilityTests` 4. Gym: shape-coded arcs (broken on the investigating seeker, wide solid on alerted guards), always-all with high contrast showing four cones with doubled edges, the eye reading DARK. Not checked in play: the Alt toggle by keyboard (needs a human), the pulse (animation; a still can't show it) |
| R28 | §44.1 harness: freeze probes Q1–Q6, Q7 after detections, route choices, telemetry, JSON logs and the verdict | [x] D148. `ReadTestTests` 9. Gym: each probe opened and answered by simulated keys and mouse (Q1/Q6 by Y/N, Q7 by digit, Q2–Q4 clicked on the true boundary: error 0.00 m, Q5 a straight trace through light scored unsafe), the west route choice logged, the log and report saved on leaving the yard. Not done: route choices in M02 and M05 (§44.1 asks for three per tester); pad answers not tried with a real pad |
| R29 | SR.3 / SR.14 cone truth sweep: every vertex of the drawn fill agrees with `Classify` plus line of sight | [x] D149. `ConeTruthTests` 3. `Tools/cone_check.sh` on the gym and M01–M14: 0 disagreements after two fixes (captives drew cones; the lit far band was drawn over hedges). The sweep fails when its read-back is tampered with. Covers ground at the guard's height; the roof deck is checked by eye |
| R22 | WASD Stealth Readability Test (§44.1): 6–8 human testers. **Gate** for Hunt and mission tuning | [ ] Harness ready (R28): turn on *Readability test mode*, then Main menu → *Test maps* (D156) for the gym, M02, M05 |
| R30 | **The blood remembers** (§48): sip shows the victim's next minute; drain shows what he knew (partner's beat, check-ins, keys, lamps, search plan), drawn as intent-path ink, breaking off when his absence is noticed; per-archetype knowledge table; 1–2 notable secrets per mission | [ ] D151. Designed, not built. After R22 (the Readability Test) |
