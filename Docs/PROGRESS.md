# VESPERTINE — Progress

## 2026-10-01
- Concept analysis, design documents (GDD, Campaign, Progression, Enemies, Level Design, Technical) written.
- Next: Phase 0/1 — project foundation and the first playable (M01).

## 2026-10-01 (later)
- Foundation, core loop, campaign framework, abilities, thralls, Nightplan (removed 2026-10-02, D115), enemy roster and UI are implemented (see TASK_LIST statuses).
- **M01 The Drowned Ward** and **M02 Lantern Street** authored and played start→end.
- M02 added: friendly NPCs, home zones (threshold law), hidden staged objectives, vampire-link re-registration after nav rebuilds, closing dialogue before debrief.
- Blood Sense had no visuals: added SenseRenderer (x-ray heartbeats, state colours, path lines); Scent of Blood and Dread Feast now reveal through it.
- Familiar Voice, False Trail and the Clean Feeder sleep bonus now have gameplay effects.
- 41/41 edit-mode tests pass.
- Next: M03 The Fishmarket Hunger, then onwards through the campaign.

## 2026-10-01 (M03)
- **M03 The Fishmarket Hunger** authored (60×42 docks: the Herring tavern, the fish market, the ice house, the Ost
  canal with a swing bridge, the Customs Arch roof crossing, two skiffs, and the Harbourmaster's two-storey office).
  Played start→end: manifest → reveal escape → debrief; dispose, Connoisseur and shrine optionals verified.
- New systems: `grant` and `ferry` script actions, `dispose` objective, `count=` on multi-part objectives,
  `sailor` archetype, `Vampire.Teleport`, Blood Arts tab gating (`CampaignState.ArtsOpen`), Abbess subtitle styling
  (violet, italic).
- The Abbess dream (after the first feed, or after 150 s) grants **Beckon**.
- Fixed: the hidden escape objective toasted "There is still work to do here" while it was still hidden.
- 44/44 edit-mode tests pass.
- Next: M04 The Bells of Saint Corvin.

## 2026-10-01 (M04, end of Act I)
- **M04 The Bells of Saint Corvin** authored (64×46 Cathedral Quarter: Saint Brigid's almshouse and bell tower,
  the Pilgrims' Lane, the cathedral nave with aisle roofs, the Great Bell tower, the deacon's archive, the walled
  anchoress's cell, the Bishop's Close, the Watch-house with its bell tower and yard, the Weirside quay).
  Played start→end: three bells cut → register unsealed → Deacon Thorne walks to the Great Bell and back → register
  taken → escape revealed → win. noholy fails inside votive light; Spare-the-clergy completes on the win.
- New systems: `noholy` objective, conduct widening (fixes nokill_type/nolockdown/protect), `sealed` interactables
  with `seal`/`unseal`, `route` action with save persistence, `if` conditions on script rules, "Bodies hidden"
  debrief row.
- Fixed in authoring: a later street fill overwrote the east aisle drainpipe, so that roof was unreachable
  (caught by nav reachability checks).
- 46/46 edit-mode tests pass.
- Next: P4 self-review, then Act II (M05 The Guildhall of Lamps).

## Milestone review 1 — end of Act I (P4, 2026-10-01)
An honest read of where Vespertine stands against "commercial-quality", after playing M01–M04 start to end.

**Strong**
- The stealth core reads and plays: light/dark sampling matches the visuals, two-band vision cones, the wary
  memory, bodies and evidence, feeding with blood qualities. The verticality (pipes, wall tops, roofs, the
  vampire-only nav agent) gives every mission real alternative routes.
- The mission language plus validator has paid off. M04 (the largest map) went from grid to verified win in one
  pass, and the validator plus nav reachability checks caught the only real authoring bug (an overwritten pipe).
- Saves are exact (positions, states, routes, groups, stains, script state, timers, campaign), and covered by tests.

**Weak, in order of player impact**
1. **Art and animation.** Primitive figures with procedural bob are the biggest gap to commercial quality and need
   real assets (KNOWN_ISSUES). Code can't close this; it is listed for replacement.
2. **No music.** `music=` keys play nothing. Synthesised ambience carries the mood, but the score is a real gap (X4).
3. **Ability verification.** Most Phase 3 abilities are `[~]`: implemented and spot-checked, but not each verified
   against its acceptance. Abilities unlock from M05 onward, so Act II is where they must hold.
4. **Lockdown had no teeth.** The GDD promised reinforcements; none existed. **Fixed in this review** (D33): spawn
   points in M01–M04, a Vigil squad from M07, save-restored, unit-tested.
5. **Holy light was not readable.** A holy votive looked like any lamp, so the noholy optional was a trap.
   **Fixed:** burning lights draw a floor ring and a faint wash at the exact burn radius, clipped by walls.
6. **Difficulty curve.** M01 has not been replayed since guards got wary memory and reinforcements (X6).

**Also fixed during the review:** saves written before route persistence no longer clear an NPC's authored patrol
on load (`""` = no route, `null` = keep the authored one).

**Next:** Act II (M05–M08). Verify each ability as it is introduced, in the mission that introduces it, rather than
in a separate pass. Next review after M08.

## 2026-10-01 (M05, Act II opens)
- **M05 The Guildhall of Lamps** authored (64×44 Coldwater Cut: the gasworks yard with two gasholders and the
  retort house, the Guildhall (clerks' office, Penrose's study, the great hall), Guild Square, the lamplighters'
  depot and bunkhouse, Guild Street and its terraces, the Coal Wharf). Three gas mains (WORKS, GUILD, WHARF) each
  feed a lamp group. The first Vigil hunter in the campaign stands at the guildhall door.
- Played: shut the wharf main → Tobias, relighting a dark wharf lamp, walks to the depot valve and reopens it.
  Shut the guild main → Penrose storms out of the back door to the main (the lure route), and Moss reopens it
  within ~10 s unless he is dealt with first. Sip Penrose → carry → quick-save/quick-load mid-carry (carry, daze
  and the gas cut all restore) → drop on the coal jetty → kidnap complete → win. Tobias seeing Ilse fails
  "Keep Tobias out of it" and he panics.
- New systems: `GasCut` lamps (a shut main stops relighting), lamplighters walk to a lamp's main and reopen it,
  the `valves` objective (progress drops when a main is reopened), valves show their authored names.
- **Bug fixed:** dropping a carried body within 2.6 m of water always tipped it into the canal, so delivering a
  live victim to a boat drowned them and failed the mission. Drops inside an active `deliver` rect now always set
  the victim down (D35).
- Vigil reinforcements were gated on Act II; they now arrive from M07, where the Vigil starts hunting her (D36).
- Briefings added to M03 and M04 (both had none); a content test now requires one in every mission. The map parser
  reports an unclosed `|` block instead of swallowing the rest of the file.
- 50/50 edit-mode tests pass.
- Next: M06 Masquerade at Ashcombe House (Dominion: thralls invite her in; eavesdropping; crowds).

## 2026-10-01 (M06 Masquerade at Ashcombe House)
- **M06** authored on a 64×46 map.
  - **Grounds:** the frozen hedge garden and summerhouse, the kitchen yard, the ice house, the carriage drive and
    gates, the east garden and pond.
  - **House:** the entrance hall, ballroom with a musicians' gallery, card room, servants' passage and butler's
    pantry, kitchen, library, long gallery, Ashcombe's locked study, drawing room, cloakroom, and the raised
    terrace.
  - **Cast:** 41 NPCs, including about 20 guests and servants and three Council conversations.
- **Two ways into the house:**
  - The front door: Sir Barnaby's mask (he is asleep in the summerhouse) plus the Cranes' invitation from their
    coach.
  - The kitchen door: enthrall the footman smoking in the yard and walk him to the step. The Abbess gives Enthrall
    as a dream gift at the start.
  - Either way, one invitation opens the whole house.
- **Objectives:**
  - Overhear Crane & Lowell (card room), Halloway & the White Mask (library, restricted) and Ashcombe & Vane (the
    terrace, overheard from the hedges below).
  - That unseals the Lantern-Master's order in the study. The key is in the pantry, or on Ashcombe's body.
  - Leave with the carriages.
- **Optionals:**
  - Loosen the terrace rail once Vane has left; Ashcombe takes his air there.
  - Feed only on the drunk.
  - The third Abbess shrine in the ice house.
  - A secret: the draped mirror shows no reflection.
- **New systems:**
  - Mask social stealth (D37) with a HUD status line.
  - `trap` interactables and accident deaths (D39), with `kill <id> accident` objectives.
  - The `feedonly` conduct.
  - Multi-door homes (D40).
  - Story gifts that are free and survive a respec (D38).
  - Script-given keys now update vampire nav at once.
  - `interact <trap>.sprung` events.
  - A `notarget` dev-console toggle for QA.
- **Playtested in the editor:**
  - **Front door:** the footman refuses her without an invitation, then without a mask, then admits her.
  - **Mask:** the house opens, and a masked walk through the lit ballroom, card room and hall raises no
    detection.
  - **Thrall route:** a thrall walked to the kitchen step opens both doors. Vampire paths run start → yard →
    kitchen → ballroom/library, and start → east hedges.
  - **Rail trap:** with the rail rigged, Ashcombe walked his loop to the rail and fell to the garden. "Make it look
    like an accident" completed and the study key passed to Ilse.
  - **Exits:** Vane and the White Mask leave on their exit routes after their conversations.
  - **Win:** three conversations → order unsealed → order taken → reached the gates.
- **Bugs fixed:**
  - Thralls on the doorstep didn't invite her (D41).
  - The ballroom centre and the card table were dark (0.06), which defeats a masque.
  - Ashcombe's rail stop fell outside his listen range.
  - The garden lovers' route ran onto the start cell.
  - The validator rejected `<trap>.sprung` events.
- **Shadow budget (D42):** only the 8 nearest lamps cast shadows. This removes the URP shadow-atlas overflow
  (K3).
- 59/59 edit-mode tests pass.
- Next: M07 The Toll Bridges (Vigil hunters, hounds and the Dossier; Vigil reinforcements begin).

## 2026-10-01 — M07 The Toll Bridges; the Vigil learns
- **Systems:**
  - Blood trails and hound tracking (D43).
  - Countermeasure effects wired to rules (D44).
  - World-flag conditions and `if=` objectives (D45).
  - Dossier gated by the stolen field manual (D46).
  - `down` script event (D47).
  - `follow=` heel behaviour (D48).
- **Fixes:**
  - `paired=` never worked because entity `Link()` was never called.
  - Lone wall tops had no navmesh.
  - The vampire nav rebuild used mismatched settings (D49).
- **M07 The Toll Bridges:**
  - 76×48 cells, 4 banks split by the Mill Leat, the River Ost and the Fen Cut.
  - 40 NPCs (36 plus 4 Rumour Faithful), 40 lights.
  - 7 crossings:
    - the leat: the Watch barricade, the leat-house roofs, the mill wheel room or its wall top
    - the Ost: the toll bridge, by windlass or by climbing the arch; Jory's skiff, unsealed by `down ferry`
    - the Fen Cut: the hunter causeway; the lock catwalk past a chained hound
  - Optionals:
    - free the Faithful (Rumour only)
    - the field manual
    - no Lockdown
    - the fourth shrine in the wheel pit
  - A listen point, Warden Cobb on Sister Hollin, sets up M08.
- **Playtested in the editor:**
  - Vampire paths complete start → exit via the roofs and the toll arch.
  - The toll gate blocks humans until the windlass is worked, then they path across.
  - Dazing the ferryman unseals the skiff, and the skiff ferries Ilse to the wharf.
  - Hounds hold heel on both patrols.
  - No console errors.
- 64/64 edit-mode tests pass, including mission validation for M01–M07.

## 2026-10-01 — M08 Hollin's Hunt; the hunter becomes the hunted
- **Systems:**
  - Tracker hunts: periodic tightening scent fixes, broken by hiding or mist (D50); `hunt on|off|now`.
  - Weather: rain as a stealth state, scripted squalls, rain particles and `amb_rain` (D51).
  - Ward tearing on a feed (D52); `take` objective and `thrall` event (D53); `hpfloor` conduct objective.
  - Hounds cast about at each fix; an enthralled handler's hounds calm at once (D54).
  - Trap `sweep` (D55).
- **Fixes:** a stale MissionController from an earlier play session stayed subscribed to `NpcDowned` and threw on
  `take` (D56).
- **M08 Hollin's Hunt:** see CAMPAIGN "As built". 64×44, rain squalls, 16 Vigil (Hollin, 10 hunters, 5 hounds) plus Tamsin, 20 lights, 2 traps.
- **Playtested in the editor:**
  - Vampire paths reach every key point (store, reeds, dyke, tower top, Marrow house upstairs, drove road exit).
  - Hunt: in the open, fixes converge to within 0.5–3 m in about four fixes; hiding resets the run to 0 and Hollin
    goes back to her post.
  - Feed → ward torn → dazed → Enthrall: `take` completes, the hunt stops, both hounds go Relaxed at her heel.
  - The sluice trap and the drain-planks trap each kill Hollin by `accident` as she follows a fix, sweep the body
    away, and complete `take`; the hounds cross unharmed and search.
  - No console errors.
- 67/67 edit-mode tests pass, including mission validation for M01–M08.
- Next: P4 self-review (Acts I–II), then Act III (M09 Coldwater, Above).

## Milestone review 2 — end of Act II (P4, 2026-10-01)
This review used tools rather than a full replay. Each Act II mission was played to a win as it was authored. Here,
`DevAbilitySweep` tested every ability in play mode, the validator ran over all eight missions, and
`DevFrameProbe` measured frame times.

**Strong**
- **Abilities hold up under test.** The sweep casts every active through the normal path and asserts its result. It
  covers 32 checks in part 1 (M05; priest check in M04) and Lingering Dark, Terror, Dread Feast, False Trail, the
  Nightplan and dread presence in parts 2–5. All pass. Each check asserts its own precondition (the witness really
  saw her, the lamp really was relit), because the first version had vacuous passes.
- **The Nightplan works as designed.** World frozen, Ilse mesmerises one guard while a thrall kills another, both
  resolved on Execute.
- **Act II systems make each mission distinct:**
  - gas mains and relighting lamplighters (M05)
  - eavesdropping and the masque crowd (M06)
  - the toll gate and the ferry (M07)
  - tracker hunts, rain squalls and traps (M08)
- **The authoring pipeline is reliable.** The validator now also checks objective targets (X5). Combined with nav
  reachability, every mission went from grid to verified win without a broken build. There are 68 EditMode tests.

**Weak, in order of player impact**
1. **Art and animation**, still the largest gap (unchanged from review 1).
2. **No music** (X4).
3. **The difficulty curve is unmeasured** (X6). M01 has not been replayed since guards gained wary memory and
   reinforcements, and Act II was only played on Normal.
4. **Two promised features did nothing.**
   - Living Lie's "corpse puppets deliver False Orders": puppets could already do it, so half the node was empty.
   - Dread presence (Awakening 9) was never implemented.
   **Both fixed in this review** (D59, D60) and swept.
5. **Performance is unknown outside the Editor** (K8). It runs at about 22 ms per frame in every mission whatever
   the NPC count, so the cost is render- or Editor-side. Measure it in a build (P3).
6. **Some mods are code-reviewed but not tested:** drop-feed, Soft Landing, Herding, gas-main Smother and Shadowstep
   through bars (K9, X7).
7. **The holy aura was documented as burning.** **Fixed:** the aura suppresses abilities and never burns; lights
   burn (D57, K6 closed).

**Also fixed during the review:**
- The validator rejects bad `deliver`/`feed` targets and non-numeric areas (X5).
- Blood Sense, Hunter's Pulse (free while still, 1 blood/s otherwise) and Scent were checked in play.

**Next:** Act III, starting with M09 Coldwater, Above. Add sweep checks for each new ability or system as it is
introduced. Next review after M12.

## Act III — M09 Coldwater, Above (2026-10-01)
**New systems:**
- **Prisoners and escort** (`prisoner` flag, `escort` objective).
  - Shackles are broken by Ilse or a thrall.
  - Followers trail her in a line and can be told to wait.
  - They can't climb and are recaptured by a guard who reaches them.
  - Guards who see a freed prisoner raise evidence and search; civilians panic.
- **Fledglings:**
  - They balk at burning light and back out of it, and burn if caught in it.
  - The Drain key turns one loose: it hunts the nearest human, dies on alerted guards, kills the rest, and pays Terror.
- **Sunstone countermeasures:**
  - The `generator` cuts a light group; the engineer restores it.
  - A thrall can smash a sunstone lamp (`<id>.smash`); it stays dark for good and saves.
  - Guards notice smashed lamps.
- **Other:** the generic `use` interactable; the validator knows escort, prisoner, generator and smash ids; 2 new tests (71 total).

**Play-verified:**
- Fledgling escort through a blacked-out ward to the cart.
- Clement's escort across the map, with a quicksave and quickload mid-route.
- Two followers at once.
- Balk at a lit door, with no HP lost.
- Frenzy: kills the ward orderly, dies on the searching station orderlies.
- Recapture by the ward orderly.
- Smash, then generator cut and restore: the smashed lamp stays dark while the others relight.
- Win to the debrief.

**Bugs found and fixed in play:**
- The balk only halted for one frame, so fledglings crept into the light.
- Captives panicked at screams and fled their chains (D64).
- The recapture check counted deactivated NPCs.
- A frenzy charged through sunstone.
- The sunstones were too small to seal a 4-cell corridor (radius 5 burns only about 2 cells); raised to 7.
- Debrief countermeasure text didn't wrap.
- M08 trap verbs showed underscores.

**Next:** M10 The Gasworks.

## 2026-10-01 — M10 The Gasworks

**Built:**
- **The map:** 64×48 (see CAMPAIGN "As built"). It covers the lamp manufactory, loading yard, retort house, a 9 m gas
  holder in an iron frame, the coke yard, the engine house and the wharf.
- **Searchlights:**
  - three towers with manned arc lamps whose pools sweep, swing to suspicions and ride an alert
  - a lens, a faint shaft and a ring on the pool's edge
  - inverse-square Unity intensity
  - cut by the searchlight dynamo (a `generator`) or by downing the operator
- **Timed sabotage:**
  - burning the three sunstone consignments unseals the valve-house fuse
  - the fuse starts a 60 s HUD countdown
  - `blast`: fireball, shake, deaths within 24 m, panic, and loss if Ilse is inside
  - the gas holder is swapped for a burnt wreck (`swapprop`, saved as a flag)
  - the holder fires light up, the works and the city go dark, and the `blackout` campaign flag is set for M11–M14
- **Evacuation:**
  - the `worker` archetype, `evac=` destinations, the `evacuate` script action and objective
  - the works whistle as a mass scare that also brings the guards
  - everyone out: Rumour +1; not: Terror +1
- **Other:**
  - the `nowindows` header, `MeshBuilder.Strut`, and the `gasholder` and `gasholder_wreck` props
  - validator support for every new action and type
  - 2 new tests (73 total)

**Play-verified:**
- burning the crates unseals the fuse
- the whistle gets all 8 workers out and the objective completes
- the countdown survives quicksave and quickload
- the blast kills near the holder, swaps in the wreck, and fires the blackout
- searchlight spotting raises the alarm in about 1 s
- the dynamo cuts all three beams
- escaping with the fuse lit wins: blackout set, Terror +1, the workers objective failed when nobody was evacuated

**Bugs found and fixed in play:**
- The searchlight pool was invisible from the towers (inverse-square falloff).
- The shaft was an opaque double-sided cone.
- The start cell was lit by a wharf lamp, sat inside tower three's sweep, and fell inside the escape rect.
- Debrief objective lines overflowed into the next column; they now wrap.

**Next:** M11 The Opera of Lanterns (first mission in the blackout world state).


## 2026-10-01 — M11 The Opera of Lanterns

**Built:**
- **The map:** 64×48 (see CAMPAIGN "As built"). Stage Door Lane, the stage house behind a 12 m arch, the stalls,
  the pit, three box tiers and the royal box, the foyer, the cloakroom and bar, Halloway's private stair, the
  Vigil's coach yard and the square. 74 NPCs, including 20 seated stalls guests and an orchestra.
- **The crescendo:** the `crescendo <s>` action and event, a noise multiplier in `NoiseSystem` (bells and lures
  exempt), and a HUD chip alongside the countdown chip.
- **The frame:** the `framed` event (Ilse's or a thrall's kill beside a standing Vigil man), a named toast,
  `AIDirector.Split` (Watch and Vigil ignore each other's shouts in Act III once `watch_vigil_split` is set).
- **The window:** a 90 s countdown from the first death, then the house lights come up and the survivors evacuate to
  the coach yard; reaching it fails the primary.
- **Props:** `seats`, `curtain`, `scenery`, `balustrade`; the balustrade trap on Halloway's rail.
- **Tests:** 4 new (78 total): the hush rule, the blame distance, the split rule, and the opera script vocabulary.

**Play-verified:**
- the swell countdown and hush chip
- a mesmerised hunter, enthralled and ordered to strike Crane: Crane dies, the frame completes, `watch_vigil_split`
  is set, and no alarm is raised
- after 90 s the survivors run for the coach yard (Halloway about 15 s, Lowell about 25 s) and the night is lost
- the loosened balustrade takes Halloway into the stalls as an accident (not counted as a kill)
- all three dead completes the council; the lane's north end wins; the debrief lists the frame and the audience

**Bugs found and fixed in play:**
- Stage lights shared ids with stalls guests (`st1`–`st4`).
- Stairs beside tall walls linked to the wall tops; the wing and private stairs moved.
- The start sat inside the escape rect.
- The stage was too dark, and two curtains' valances hid it from the camera; it is now one arch with one curtain.

**Next:** M12 Vane's Bastion.

## 2026-10-01 — M12 Vane's Bastion

**Built:**
- **The map:** 64×52 (see CAMPAIGN "As built"). St Calder's island in the Vesper: the prison, the barracks and
  kennels, the bailey, the keep (great hall, chapel, generator, archive upstairs) and the cloister inside a curtain
  wall. Three crossings from the mainland: the weir, the skiff and the causeway. 48 NPCs, 56 with the full Dossier.
- **The full Dossier:** header `dossier = all` (`CampaignState.FullDossier`, weight ≥ 2), the briefing line "Vane
  knows her: …", and four dormant `cm_*` groups.
- **The choice:** `choice <id> "prompt" key "text" …` opens a modal (number keys or click). The answer sets the
  mission flag and the campaign flag `<id>.<key>` and fires `choice <id>.<key>`. The validator checks the keys.
- **The deed:** kill Vane (the sanctuary-lamp trap counts as an accident), or burn the archive (fire lights,
  `archive_burned`, then a lockdown from the smoke 40 s later). The campaign records `vane_dead` or `vane_alive`
  on leaving.
- **The stair rule:** `FindUpNeighbour` prefers a straight flight (D80). M12's keep stair was fixed, and so was
  M06's gallery stair.
- **The intro card** shows the level file's subtitle (D81).
- **Tests:** 3 new (83 total): the straight-flight rule, the corner-stair fallback, and the choice/Dossier
  vocabulary, including a rejected unknown answer.

**Play-verified (Hunter, Act III Awakening):**
- All three crossings connect. Wall-crawl from the weir to the curtain needs Awakening 4.
- The Dossier opens the choice. Burn: lights, flag and countdown, then the lockdown. Kill: the lamp trap kills Vane
  at prayer, the Dossier settles the deed with no choice, and escaping wins with +1 Terror.
- The Faithful escort works, guards recapture on contact, and the objective reads 2/3 when two get out.
- With a seven-habit Dossier, all seven countermeasures are active. The dormant groups spawn (56 NPCs). Anselm
  stays unwarded while the hunters are warded.
- The generator cuts the keep's sunstones and the keep searchlight, and an engineer restores them. The west
  searchlight is on its own supply.
- **Frame time:** median 11.1 ms, p95 13.5 ms, worst 20.8 ms (48 NPCs, 50 lights). This is half the 22 ms in K8's
  runs, on the same Editor after a host reboot, which supports K8's reading that the earlier cost was host-side.

**Bugs found and fixed in play:**
- Vane's study-to-chapel route went round the curtain wall (158 m). The keep stair laddered onto a wall top; fixed
  by the stair rule.
- The intro card showed the catalogue place, not the night's subtitle.
- An `id:key` choice flag broke rule parsing; the separator is now `.` (D79).

**Open:** K18 (a one-off instant `out` completion after the burn, not reproduced) and K19.

## Milestone review 3 — end of Act III (P4, 2026-10-01)
Each Act III mission was played to a win on Hunter as it was authored, with the new systems checked in play. The
validator and 83 EditMode tests cover the content and the deterministic rules.

**Strong**
- **Each mission adds a system:**
  - M09: escort and fledglings
  - M10: searchlights, the blast and evacuation
  - M11: the crescendo and the frame
  - M12: the full Dossier and the choice

  Every system is a reusable script verb, so M13 and M14 start from a large vocabulary.
- **The campaign remembers what happened.** Act III writes `faithful_bastion`, `watch_vigil_split`, `vane_*`,
  `archive_burned`, the shrine flags and the countermeasures, so the endings have real inputs.
- **The fiction still holds the power curve.** In Act III Ilse frees prisoners, frames the Vigil, and burns Vane's
  life's work or ends him. The arc from "hiding from humans" is visible.

**Weak, in order of player impact**
1. **Art and animation.** This is unchanged and is still the largest gap.
2. **No music** (X4, K15). Missions name a `music` key, but no score plays.
3. **The difficulty curve is unmeasured** (X6). Act III was played on Hunter with an injected Awakening, not with
   a campaign's real progression.
4. **The countermeasure groups exist only in M12** (X8). Countermeasures change behaviour everywhere, but before
   M12 they add no bodies.
5. **Unreproduced:** K18.

**Next:** Act IV. M13 The Long Night, then M14 The Abbess Beneath with the endings. Next review after M14.

## 2026-10-01 — M13 The Long Night
- **M13 The Long Night** authored (72×62: the Wick, the Mercy canal, the market district, the cathedral close) and
  played through: all three cordon squads broken, the west doors unsealed, a win inside St Vesper's.
- **New: hunter squads.** `squad` entities with shared nerve, wedge formation, rearguard glances, rings after shocks,
  promotion when the leader falls, rout to a rally point, and save/load. They come with the `break` objective,
  `broken`/`routed` events and the `rout` action. Squads muster lazily so script groups can bring them in (the Candle
  Row raid).
- **Vane** (if he lives) leads the close squad: +30 nerve, and he stays to hunt her when his men run. Killing him
  there writes `vane_dead`.
- **Burning the archive** in M12 now wipes the Dossier once (D86).
- **Fixed:**
  - a stack overflow when a squad broke (officer search assignment and leader-forwarding recursed, D84).
  - officers conscripting friendlies.
  - the M13 script listened for `pris.loose` (fledglings only) instead of `pris.free`.
  - props in one-cell lanes cut off two rally points (D88).
- **Verified in play:**
  - the raid countdown fails the barricade and loses Tobias.
  - breaking the raid in time unlocks Candle Row and sets `candle_row_held`.
  - Nell's escort completes.
  - quicksave/quickload mid-shock restores nerve, ring and broken state.
- 91/91 edit-mode tests pass.
- **Next:** M14 The Abbess Beneath and the endings, then milestone review 4.

## 2026-10-02 — M14 The Abbess Beneath, and the endings
- **M14 The Abbess Beneath** authored (64×64) in three stages: the nave of St Vesper's, Saule's undercroft and the
  Council's vitae vault. Played through on every branch.
- **New systems:**
  - `sunbeam` lights: dawn through the clerestory. Each is a burning pool that creeps along a cell path from a start
    time, can't be snuffed, and the HUD names it "Sunlight". The path maths is in a pure, unit-tested `SunbeamMath`.
  - The `moon=` header, and `intensity=` on any light.
  - The `abbess`, `vat`, `still` and `bricks` props, and the `saule` archetype.
  - Ten shrines, the last in M14's ossuary.
  - A flag-driven epilogue on the ending screen (`CampaignState.Epilogue`): one line each for Tobias, Vane, Hollin,
    Saule, Clement, the Council, the Faithful and the shrines.
  - The Refuge subtitle names how the campaign ended.
- **`needs=` on `reach` also accepts a mission flag.** M14's hidden `flee` objective was live before it was
  revealed, so walking into the river room before the choice would have won the night. It now waits for
  `abbess.destroy`.
- **Fixed during play-testing:**
  - The squad started on top of the player.
  - A stair against a `T` wall gave roof access past both locked ways down.
  - Both `saule_dead` and `saule_spared` were written, because `down` fires on death too (D92).
  - The destroy blast reached the nave and raised framing toasts. Its radius is now 24.
  - The vault lit by the moon through its open top, and the overbright council chandelier.
  - The escort and flee rects were 2 cells wide, so followers who keep their spacing never entered them. They now
    cover the whole river room.
  - "Dr. Saule was found in his own purge chamber" showed for any death. A new `saule_purged` flag now drives it.
- **Verified in play:**
  - The three ways down: the bricks deed, the sexton's stair and Ashby with `cf_faithful_bastion`.
  - The vault gate, and humans pathing between the lab and the vault.
  - The purge accident completes `saule` and `purge` and unseals the Abbess.
  - `gen_lab` darkens the ring and the lab lamps.
  - Two fledglings led out complete `subjects`.
  - **free** wins → *The Night Court* with the epilogue.
  - **consume** → *The Pale Lady* (+100 vitae).
  - **destroy** → countdown, burn lights and `flee` revealed. Reaching the river wins → *Dawn*; the timeout loses.
  - Vane leads the nave squad when he lives.
- 97/97 edit-mode tests pass.

## Milestone review 4 (after M14)
**Where it stands:** the campaign is authored end to end. There are fourteen missions across four acts, each
play-verified on its main routes. World flags from every act feed six matrix endings and a per-character epilogue.
The core fantasy reads in the arc:
- In M01 she hides from orderlies.
- By M13 she breaks Vigil squads by fear.
- In M14 she decides what the city's night becomes.

**Strong**
- **Systemic depth.** Light/dark, verticality, blood, thralls, the Dossier countermeasures, squads, searchlights,
  sunstones and sunbeams all compose through one script vocabulary and validator. Every mission validates with
  0 problems.
- **Consequences carry.** Tobias, Vane, Hollin, Saule, Clement, the Faithful, the Council and the shrines all reach
  the ending screen.

**Weak, in order of player impact**
1. **Art and animation.** These are still primitive-built blockout. This is the largest gap to commercial quality.
2. **No music** (X4, K15).
3. **The difficulty curve is unmeasured over a real campaign** (X6). There has been no continuous M01 → M14
   playthrough with real progression; every mission was tested with injected Awakening and flags.
4. **No interludes (P1) or mission select with bests (P2).** The between-mission rhythm is thin.
5. **Performance in a player build is unknown** (P3, K8).

**Next:** Phase 6:
- P2 mission select, then P1 interludes.
- X4 music.
- A full-campaign playthrough (X6).
- P3 perf, then P5 the Windows build.

## 2026-10-02 — P2 challenges and replay
- **Seven challenges per mission:** Unseen, Merciful Hunger, Silent Night, Before the Bell (under the mission's par),
  Unbroken (no loads), Every Thread (every optional in one night) and Apex (on Apex difficulty).
  - The logic is pure (`Progression.Challenges.Earned`), with 4 tests.
  - The mission record keeps every challenge ever earned.
  - The debrief lists all seven, earned ones in gold with NEW on a first earn.
  - The Missions tab shows n/7 and the badges, and the Record tab shows the campaign total.
- **Fixed:** the Refuge header was squeezed by a long Missions list. The header and tabs no longer shrink, and the
  list scrolls.
- 101/101 edit-mode tests pass.

## 2026-10-02 — P1 blood-dreams
- **13 interludes** (`Progression.Interludes`), one after each night M01–M13. Their lines come from what she did:
  - Hollin enthralled vs buried, the locket, Clement, the notes burned, Vane dead vs alive (and the archive).
  - The Faithful, and Tobias alive / thrall / lost.
  - In the last dream, whether the city hides from her (Terror) or leaves her candles (Rumour).
- **Presentation:** after the debrief's Continue on a first win, a dark-red screen fades in one line every 2.6 s
  over a slow heartbeat. The Abbess speaks in red italics and whispers; other speakers get a dim name tag. "Show all",
  then "Wake" goes to the Refuge.
- **Journal:** each dream is filed under its mission as "Blood-dream: …".
  - Fixed: the Journal list and page used to float in the middle of the screen; they now start at the top.
- 104/104 edit-mode tests pass (3 new interlude tests).
- Verified in play: winning M08 with Hollin enthralled → dream → Wake → Refuge, and the dream is in the Journal.

## 2026-10-02 — X4 mission music
- **`Audio.Score`: ten themes, one per `music =` key:**
  - drowned: A minor; pale pad and falling drops
  - lantern: D Dorian; street-organ drone and a music-box tune
  - docks: E Phrygian; cello, foghorn and creaking ropes
  - cathedral: C harmonic minor; organ, chant and a deep bell
  - gasworks: F minor in 5/4; throb, clanks and steam
  - masque: G harmonic minor waltz; pizzicato, harpsichord and melody
  - bridges: B-flat minor; running string ostinato
  - hunt: D minor; toms, horn fifths, a call and an answer
  - opera: E-flat; strings, pizzicato and a soprano aria
  - bastion: C-sharp Phrygian march; snare, timpani, low brass
- **Layers:** each theme has tension and alert layers in its own root and tempo. They are rendered off-thread,
  cached, swapped in under a 0.3 s dip, and beat-locked: all three layers start on one DSP tick and never stop.
- **Verified in play (M06):** the masque theme swapped in, and the layer positions matched modulo 8.57 s and
  4.29 s.
- 106/106 edit-mode tests pass:
  - every mission's key has a theme
  - every theme renders without NaNs, at matched loudness, with no click at the loop point, and with layer
    lengths that divide each other

## 2026-10-02 — X7 flag-mod sweep, K18 tripwire
- **`DevAbilitySweep` part 6**: each check runs with and without its node.
  - Soft Landing: body noise from a pounce, 1 → 0.
  - Drop-feed: a synthetic jump `EndLink` beside a calm human with a pending Feed. Awakening 6 doesn't feed;
    Awakening 7 does.
  - Herding: a human panicked by a decoy point re-aims their flight away from Ilse only with the mod.
  - Black Main: one smother puts out 1 lamp, or the whole main of 5 (M05) or 15 (M10) with the mod, and no other
    lights.
  - Between Bars: Shadowstep's line of sight through a bars collider is blocked without the mod and clear with it.
  - 5/5 pass in M05 (M10: 4 pass, bars skip).
- **Escape objectives now check `needs=`.** Before, it was only implied through the primaries.
- **K18 not reproduced.** Tried Dossier → burn → 40 s smoke lockdown → death, and both autosave restores. An
  `[K18]` error with a stack trace now fires if an escape ever completes with Ilse outside its area.
- 106/106 edit-mode tests pass.

## 2026-10-02 — X8 Dossier bodies in M07–M11
- **Four dormant groups per mission.** M07–M11 now carry what M12/M13 already had, so each answered habit brings people
  as well as its systemic effect:
  - `cm_rooftop`: sentries on the mill roof and the Vigil hut (M07), the hall wall and the camp wall (M08), the
    south wall (M09), the north wall and the office roofs (M10), and the east roofs and the west parapet (M11).
  - `cm_paired`: partners on existing hunter and watch beats.
  - `cm_inquest`: an inquisitor on a new loop.
  - `cm_censer`: two alchemist posts at bridges or doors.
- **Verified in play with all four countermeasures forced on.** After 25–30 s every group NPC is on the navmesh and
  Relaxed, the patrols are moving, and the posts hold.
- **First pass caught three problems:**
  - M09's map starts with two blank rows, which my map tools skipped. The parser counts them, so every M09 cell was
    two rows off and the sentries fell to the ground. `mapgrid.py` now parses exactly like `MapParser`.
  - M09's south-wall sentry saw Ilse at the start and raised the whole ward.
  - M08's inquisitor loop and M11's parapet route ran past the start.
- **`DossierContentTests` (7 cases)** holds M07–M13 to this: all four groups present, and no new spawn or route
  within 10 cells of the start (D98).
- **Fixed K23:** `AudioManager` threw IndexOutOfRange every frame after a play-mode script reload (a serialised
  empty array). Found while testing this.
- 113/113 edit-mode tests pass.

## 2026-10-02 — P5 Windows build + smoke test, P3 performance
- **Windows build from the CLI:** `Tools/build.sh` drives the live Editor's Pipeline `build` command and polls
  `build_status`.
  - Only `Main.unity` ships (D99).
  - Result: `Builds/Win64/Vespertine.exe`, Succeeded, 0 errors. The 10 warnings are expected; see TECHNICAL_DESIGN §11.
- **Player smoke test** (`Core/SmokeTest.cs`, `Tools/smoke.sh`): the shipping exe with `-smoke` runs a throwaway
  campaign through all 14 missions, calm and then under full lockdown hunt.
  - **PASS:** every mission started, the player spawned, NPCs registered, and there were 0 errors and 0 warnings.
    Lockdowns and reinforcements now work in every mission of a real build.
  - Saves go to a scratch root (`SaveSystem.RootOverride`), so a smoke run never touches player profiles.
- **P3:** with `-uncapped` (vsync off), the worst case is M11 hunted (79 NPCs): median 3.5 ms, p95 4.4 ms at 1080p.
  Calm missions take 1.3–2.0 ms. 720p is the same, so the game is CPU-bound. No optimisation work is needed on
  this hardware; low-end numbers remain open (K24).
- 113/113 edit-mode tests pass.

## 2026-10-02 — X6 economy audit and difficulty curve
- **Found:** PROGRESSION and GAME_DESIGN promise objective Vitae, but the tally paid only Marks. Vitae came from
  feeding alone, so a ghost or merciful player was starved of Awakening.
- **Found:** draining a notable paid a Mark every time, so a replay could farm M06's seven notables.
- **Fixes:**
  - `BuildResult` now pays +60 on first clear, +30 per new optional and +20 per new secret, alongside the Marks.
  - Notable Marks are recorded per mission (`MissionRecord.Notables`) and paid once per notable.
- **`Progression.CampaignEconomy`:**
  - `Survey` reads a mission's feedable people, blood values, notables, optionals, secrets and scripted vitae/mark grants.
  - `Simulate` runs ghost, typical and predator campaigns on each difficulty.
  - The Awakening thresholds were retuned against it (D100); the table is in PROGRESSION.
- **New `CampaignEconomyTests` (5):**
  - Tiers open on time.
  - Nobody reaches Awakening 10 before M10.
  - Nobody can afford every node before M12.
  - Typical finishes awakened; a ghost does not.
  - Marks fill half to most of the trees.
  - Difficulty scales feeding.
- **Pars** look consistent with map size and the number of primaries.
- 118/118 edit-mode tests pass.

## 2026-10-02 — Campaign-flag carry-over (M01 → M14)
- **Audit:** 40 distinct `campaign_flag`s are set across the missions. Seven were never read anywhere: `heard_singing`,
  `hollin_journal`, `knows_guild_seal`, `knows_vane_distrust`, `weirside_beast`, `tobias_met`, `blackout`.
- **New payoffs:**
  - M06: reading the Saint Corvin register in M04 gives Ilse a line about the Council's forty guineas.
  - M11: the Gasworks manifest's Guild seal turns up on the opera's scenery crates.
  - M12: Vane's letter (M11) means Ilse notices there is no Watch on the bastion walls.
  - M14: the singing heard on Candle Row (M13) is answered by the Abbess.
  - Epilogue: Hollin's field book changes her thrall and death epilogue lines.
- `tobias_met` and `blackout` are record-only (D101).
- **`CampaignFlagTests` (2):**
  - Every flag set is read later: by a `cf_` condition, a `flag_` group, or a string literal in the game's code.
  - Every `cf_` read was set on an earlier night.
- **Runtime check** (`flagrun.sh`, play mode): t_ledger (M06), t_seal (M11) and t_sing (M14) fire with their flag set
  and stay silent without it.
- M06 now has an assembly script (`asm06.sh`) like M07–M14.
- 120/120 edit-mode tests pass.

## 2026-10-02 — X2 closed, frame-rate stuck bug
- **X2:** Familiar Voice, False Trail and Clean Feeder all have effects, and each passes its ability-sweep check.
- **Sweep fixes (tooling):**
  - The sweep no longer targets anyone an objective needs alive. It had rended M05's Penrose and failed the mission.
  - Lethe picks a target that isn't already Wary.
  - Lingering Dark now uses a real lamplighter and a lamp it can walk to, and checks that the lamplighter got there.
- **Game bug found (K25, D102):**
  - `Npc.Stuck` counted under 2 cm of movement per frame as stuck. Above ~65 fps every walking NPC qualified, and gave up after 3 s.
  - In practice, lamplighters abandoned lamps more than a few seconds away, and investigations and searches ended early.
  - It is now a speed test (`Npc.Crawling`, under 0.12 m/s), covered by `StuckTests`.
- New helper: `sweep.sh <mission> <part>` runs a sweep part on a fresh mission and prints anything that didn't pass.

## 2026-10-02 — Known-issue pass (K2, K4, K10, K22)
- **K2 (restart after a load):**
  - A load never set the restart snapshot. Restart then kept Vitae fed before the save, or, if another mission had run that session, rolled the campaign back to *that* mission's start.
  - Saves now carry `CampaignAtStartJson`, and a load uses it. Older saves fall back to their own snapshot.
- **K4 (pause during the closing lines):** the closing freeze is its own pause (`Game.ClosingPaused`). Opening and closing the pause menu no longer thaws it (checked in play mode).
- **K10:** a recaptured prisoner walks back to its cell instead of sitting where it was caught (checked in M09).
- **K22:** `needs=` now works on `take` and `interact`. An early completion is held as a saved `held.<id>` flag and lands once the needs are met.
- **Regression:** ability sweep parts 1–6 all pass (34 + 3 + 1 + 1 + 1 + 5). 129/129 edit-mode tests pass.
- **Tooling:** `compile.sh` now stops play mode first. A recompile during play had nulled `Npc.Arch` and flooded the console with NREs, which looked like a game bug. New `errs.sh` counts real console errors through `LogEntries`.


## 2026-10-02 — Whole-campaign flow check (X9)
- **`DevCampaignRun`** (`camprun.sh <prefer>`) plays a throwaway campaign from M01 to an ending through the real flow (see TECHNICAL_DESIGN §10):
  - mission start and script rules;
  - the debrief's Continue, the blood-dreams and the ending.
  It drives objectives rather than sneaking. Use, Take and secrets go through the entity's own `Use`, areas are entered by teleport, and choices go through the new `UIManager.AutoPick` seam.
- **Three runs, three endings, all PASS with 0 errors:**
  - free → *night court*;
  - consume → *pale lady*;
  - burn + destroy → *dawn with Tobias*, including the burning-vault flee.
  Each run won 14 nights and showed 14 blood-dreams, and Awakening reached 8 with every optional taken.
- **Content bugs it found:**
  - M06 never revealed `study` or the `away` escape. After the order, nothing named the way out. Now `reveal`ed, and the validator rejects any hidden objective that no rule reveals (D104).
  - M03's "first shrine" set no flag, while the epilogue counted M04's anchoress cell as shrine 1. The shrines are now M03, M05–M12 and M14 (D103). `TheShrinesAreFoundInOrderOncePerNight` holds the numbering.
- **Tests:** 130/130 edit-mode.

## 2026-10-02 — Front-end and refuge verification, the Codex
- **Tooling:**
  - `DevUi.Buttons()` / `DevUi.Click(label)` drive any UI Toolkit screen from an eval.
  - `ui.sh "Label" …` presses buttons in order and screenshots, and `hub.sh <tab> '<setup C#>'` opens the refuge on any tab with a seeded campaign.
  - `SaveSystem.DevSandbox` (set by `compile.sh`, editor session only) points every save at `Temp/DevSaves`, so automated play never touches real profiles.
- **Front end and pause, play-verified:**
  - Main menu: Continue, Load and Credits all work. The Credits panel was see-through, and `.panel` is now 97 % opaque.
  - Pause: save to a slot, then load with a confirmation (the position was restored), the death screen's five options, and the Abandon confirmation.
  - Settings persist and rebinding swaps on conflict.
- **Refuge layout fixes** (all from the global `.row { align-items: center }`, D105):
  - Next Night's Begin button fell out of its card.
  - The skill tree overflowed the screen in staggered columns. It is now a scroll view that keeps its place across rebuilds, and the empty Dominion "GIFT" tier is hidden until given.
  - On the Loadout tab, slot arrows overflowed, the known-arts text clipped, and the upkeep text overlapped Marks.
  - The Missions list squashed the footer.
  - The Dossier and Record pages were centred vertically.
- **The Codex (G7):**
  - A new refuge tab. An archetype is recorded (`CampaignState.Bestiary`, saved) the first time Ilse is within 18 m of one with nothing in the way, and a toast says so.
  - Pages are grouped by faction ("8 of 26 known"; unseen entries show as ???). Each page has Ilse's own note on them (`Codex.Lore`), the archetype's summary, then sight, hearing, weapon, nerve and blood. These are built from the live archetype numbers, so the Codex can't disagree with play.
  - "What to know" lists one line per behaviour flag.
  - Verified: a sighting in M02, and the tab with seeded entries.
- **Test results stay in the project:** the performance-testing package wrote `TestResults.xml` into the real `persistentDataPath` after every editor test run. `Editor/QuietTestResults` removes that callback after each domain reload (D106).
- **Tests:** 136/136 edit-mode (+6 `CodexTests`).

## 2026-10-02 — The Dossier, play-verified; caged lamps reworked
- **G6 in play (M07):** snuffing five lamps and two feeds raised the habits, `cm_caged` came out as the Vigil's answer, and the debrief and the Dossier tab both showed it.
- **Caged lamps had two flaws.** M08 got no cages at all: the rule only covered street and wall lamps, and a hash threshold set the share. The design's "lamplighter patrols doubled" was also missing. Both are fixed (D108):
  - Exactly half the hand-snuffable gas lamps, wall lamps, braziers and candles are caged, always the same ones (`GameLight.PickCaged`).
  - Cages are sized to the fixture: a broad grille over a brazier, a small lantern cage over a candle.
  - Every human faction member carries a taper under `cm_caged` and relights a dark lamp he sees.
  - Survey with `cm_caged` on, caged/eligible: M07 14/27, M08 4/7, M09 11/22, M10 14/28, M11 20/40, M12 11/22, M13 20/39, M14 9/17.
  - In M08, a lamp put out in front of a Vigil hunter was relit within about 6 s.
- **Harness note:** with edge-pan on, the editor's idle cursor in a screen corner drags the camera to the map corner. Turn `Game.Settings.EdgePan` off in memory before framing screenshots.
- **Tests:** 139/139 edit-mode (+3 `CagedLampsTests`).

## 2026-10-02 — Archetype silhouettes (E1)
- `Core/DevLineup` (editor/debug only) stands every listed archetype in rows. It puts them on open street ground, where the line to the camera is clear on the grid. `Frame(row)` gives a close-up of one row.
- The M07 lineup turned up two problems at play zoom. Priest and servant read the same: dark coat, no hat. Vigil hunters read like guests, both in a brimmed hat. The alchemist's censer also had no mesh.
- Fixes in `CharacterRig.BuildHuman`:
  - New hat keys: `mitre`, `beak` and `mortar`.
  - A Vigil mantle and coat-tail.
  - A `Weapon.Censer` mesh (a chain and a brass ball).
  - Undead get grey skin and a narrower build.
- Archetype hats changed: priest → mitre, alchemist → beak, scholar → mortar, sentry → stovepipe.
- Re-shot at default zoom: the pale Vigil mantles stand out across the square. At close zoom the mitre, helm and mantle read clearly.
- Tests 139/139.

## 2026-10-02 — Holy aura made visible; inquisitors read bodies (E3, E5, E6)
- `Visual/AuraRenderer` draws a ring at the edge of each waking priest's 6 m aura (D110). Checked in M04 beside Brother Aske: it reads apart from the holy-light burn rings, and the priest wears the new mitre.
- `Npc.Examines` / `ExamineHabit` (D111): inquisitors, Hollin and Vane add the habit a body shows to the Dossier and see through staged accidents. 5 new ExamineTests.
  - Checked in M08: an inquisitor 3 m from a fresh "accident" went to Searching, the body was reported, and the lethal habit rose 0 → 1.5.
- Codex: the censer trait now says 4.5 m, mist and shadowstep (it wrongly said 3 m and Dominion). Added a new trait, "Reads bodies".
- E6 checked in M08: Hollin entering a search sent 3 nearby searchers (d_h1, d_h2, an inquisitor). A hound whose handler was killed found the body and searched.
- Tests 144/144.

## 2026-10-02 — Audio pass (C18 closed)
- NPC footsteps (D112): by surface, faster and louder when running, hounds padding, Bulwark armour clinking, only near the camera's focus. Checked in M08: the handler h_dyke and his hound d_dyke each sounded steps at their own positions as they walked the dyke.
- Fires: a new synthesised `amb_fire` loop (flame roar, pops and wood splits). Four positional voices follow the nearest lit braziers and bonfires. Checked in M08 at Tamsin's brazier (amb_fire at 0.43).
- NPCs relighting a lamp now play `ignite` at the lamp.
- Bugs fixed:
  - Wading was silent: `step_water` was played by its bare name, but the clips are `step_water0–2`.
  - The menu backdrop asked for nonexistent `wind`/`drips` ambiences; it now uses `amb_night`/`amb_drip`.
- 4 new FootstepTests. One builds the real Synth bank and checks that every clip the step code asks for exists. Tests 148/148.

## 2026-10-02 — HUD cost, garbage and world barks (D113)
- Profiled hunted M11 in the editor (Tools/scratch_backup/prof.sh for self time, profgc.sh for GC bytes). The HUD, not the AI, was the largest game-side cost: `UIManager.Update` 1.9 ms plus 1.3 ms of UI Toolkit re-tessellation, against 0.4 ms for all 79 NPCs.
- Detection meters skip off-screen NPCs and write only changed values. `PlaceAt` skips sub-pixel moves, key labels are cached per binding, and the verb bar refreshes at 10 Hz. HUD plus UIR fell by about 2 ms a frame in the editor.
- Garbage: 1,284 B a frame down to 173 B. Fixes: vitals and status text rebuilt only on change, hashed verb and objective signatures, the hover prompt rebuilt only for a new target or at 10 Hz, cached pin distances, a struct `Living()`, a reused timer key list and a static shadow-sort comparer.
- World barks now stay inside the screen and above the vitals panel. A speaker more than 250 px off screen is not shown, so a crowd's barks don't pile up at the edges.
- 148/148 tests pass, and the Windows build succeeds. All 14 missions pass the uncapped 1080p smoke run, but its timings are void: Valheim was running on the same machine (fps 1–11, multi-second stalls everywhere, including in missions measured at 3.5 ms before). Re-measure on a quiet machine.

## 2026-10-02 — Direct control: WASD, real-time stealth, thralls as a party (D114–D118)
Play-test feedback: movement should be WASD, the planning mode wasn't understood and didn't seem to work, and it should be real-time stealth with thralls controlled like a BG3 party (click a portrait). Agreed choices: a follow camera with look-ahead, WASD only (left-click uses and targets, never moves), thralls hold position unless told to follow, and P as a plain pause.
- **Ilse** moves with WASD (or the left stick) relative to the camera, through `NavMeshAgent.Move`, so every navmesh rule still holds. Shift runs (loud). Speed eases in and stops crisply, and she slides round living humans. Feed, carry, use and ability keys still walk her into range, and a key push cancels that. `Player/VampireMovement.cs`, `Controls/MoveMath.cs`.
- **Climbs, drops, leaps and mist passages** are taken by walking into them for 0.18 s (0.3 s for a drop off a roof) or at once with Space. The verb bar names the move on offer (Climb / Drop / Leap / Slip through).
- **The Nightplan is gone** (the `Plan/` module, its HUD frame, its styles and its bindings). P pauses on every difficulty, and no orders are taken while paused.
- **Thralls as a party.** A Party panel lists Ilse and each thrall. Click a portrait (or press T to cycle) to put the keys and the camera on that character; RMB or Ilse's portrait goes back. Whoever isn't controlled holds position. X toggles follow (for one thrall, or for all of them from Ilse). Thrall commands: 1 Distract, 2 False Orders, 3 Puppet Strike, 4 Release, and G or a click to use thrall-usable objects. A thrall steered into a ladder climbs it. The controlled thrall wears a pulsing violet ground ring, and its Party row reads "Under your hand".
- **Camera** follows whoever is controlled. Arrows, MMB drag and opt-in edge pan look ahead (max 22 m), which eases home once the character moves; Home re-centres.
- **Settings v1** migration turns edge pan off and drops old binding overrides (WASD used to pan). The M01 tutorial hints and M11's briefing are rewritten for the new controls, and the keys prefer what is next to her over what is under the cursor.
- **Tests:** 11 new `MoveMathTests` (camera-relative input, easing, push detection, look-ahead, migration); 159/159 pass. Sweep part 4 now checks direct control in M02 (walk, thrall moves while Ilse holds, follow, a thrall's Puppet Strike, hand back, a climb by pushing): 6/6. Parts 1 (M05) 33/33, 2 (M02) 3/3, 3, 5 and 6 (M05) 5/5 pass. Two sweep checks were fragile and are fixed: Nightblood now picks a dark spot with nobody near (Court's mesmerised walkers carried lanterns up to her), and Terror picks a witness who can actually see her and feeds without Gorge/Stalker (a 0.67 s sip ended before the witness's first sight tick).
- **Whole campaign:** `camprun.sh free` plays M01 → ending (night court) through the real debriefs and blood-dreams: PASS, 0 errors. The M02 snuff tip now reads "walk up to it and press [G], or click it".
- **Build and smoke:** the Windows build succeeds. The uncapped 1080p smoke run, this time on a quiet machine (the D113 run was void), passes all 14 missions calm and hunted with 0 errors: 300–550 fps, median 1.7–3.0 ms, worst p95 4.7 ms (M11 hunted, 79 NPCs).

## 2026-10-02 — M01 play-test fixes: sneak, footsteps, doors, hiding, the gate (D119–D123)
Play-test feedback on M01: the bodies around her read as kills, there was no sneak and walking was silent, hiding herself or a body wasn't found, the gate did nothing, doors could be walked through, and the level could be finished in about 15 seconds. Agreed choices: hold Ctrl to sneak, E opens doors, M01 "real but fair", bodies shrouded on slabs.
- **Footsteps** (D119): sneak (Ctrl, 1.75 m/s, silent on dry ground), walk (heard within 3.2 m) and run (8 m). Wading and carrying are always audible. Each audible step draws a faint ring. A calm human who hears her turns to look.
- **Keys** (D120): E interact, Ctrl sneak, Z/X rotate, H follow/hold. Settings v2 drops old overrides. Tips name actions (`{Interact}`) and show the current key.
- **Doors** (D121): shut by default, stop her, block sight and muffle sound (new `Door` layer), creak unless she sneaks. Humans open the doors they walk through and shut them behind them. The open state is saved.
- **The interact key** (D122) uses the nearest thing beside her and the HUD names it ("[E] Hide inside", "[E] Hide the body"). Things she can't use still answer ("Barred. The wheel valve beside it lifts the gate.").
- **M01** (D123): the antechamber door is locked, and the valve wheel sits on the stoker's tool box in the boiler room. Turning it brings two orderlies, and the gate takes 2.5 s to rise. The dead lie shrouded on slabs and beds, her own sheet is thrown back, and she names them as her Ward C patients.
- **Tests:** 8 new `SneakAndDoorTests`; 166/166 pass. New sweep part 7 (`sweep.sh m01 7`), 5/5:
  - the same clerk ignores her sneaking within 2.5 m and turns when she walks;
  - a shut door stops her 0.4 m short, E opens it and she walks through;
  - an orderly opens a door, walks through and shuts it;
  - E hides her in a wardrobe and lets her out, then hides a carried body;
  - the M01 valve is sealed until the wheel is taken, and turning it calls the rushers. The gate rises over 2.5 s, she walks out and the mission is won.
  
  Parts 1–6 still pass (33, 3, 1, 6, 1, 5).
- **Whole campaign:** `camprun.sh free` first stalled in M01. The dev runner teleported into the escape before taking the hidden wheel objective, then forced the escape. It now waits for hidden primaries, as the game does. The re-run passes, M01 → night court ending.
- **Build and smoke:** the Windows build succeeds (0 errors). The uncapped 1080p smoke run passes all 14 missions, calm and hunted, with 0 errors. Worst p95 was 5.8 ms (M12 hunted).

## 2026-10-02 — Redesign week 1: quick wins and stealth readability (D124–D136)
The first slice of GAMEPLAY_REDESIGN, in its agreed order: Quick Wins 1–6, 9 and 10, then the readability Quick Wins 7 and 15–19. Hunt and mission tuning wait until the Readability Test passes.
- **Bugs and rules** (D124–D131):
  - a shout carries 10 / 15 / 22 m (it was 15 times that);
  - a sipped victim leaves Dazed before waking and reports once;
  - `Cast` repeats the running-water check;
  - the Arts open after M01, and Beckon is granted in M02's waking dream;
  - sneak is 2.2 m/s;
  - no auto-walk: keys act at once or refuse with the distance, and feeding lunges from 2.6 m;
  - challenges pay +1 Mark the first time;
  - Unbroken means no alarm and no loads.
- **Readability** (D132–D136):
  - exposure rims at each nearby light's 0.35 contour;
  - Unity spot and point lights fitted to that contour;
  - contextual cones (aware, or within 4 m of her now or in 1.5 s; at most 4; near-sector-only past the budget);
  - a cone redraw: solid near sector and edge arc, far band filled only where she'd be lit (20 samples a ray), touch circle, red shout ring;
  - the Spotted caption naming the band, numbers and modifiers.
- **Play checks in M02:**
  - Contextual cones picked the right guards (w1 and the beggar Full, w4 None).
  - The shout ring, touch circles and alarm-red cones showed.
  - Stepping into w1's near sector gave "Near band: 2.6 m (limit 8.3 m)…".
  - Standing under the gas lamp 9 m in front of him gave "Lit by the gas lamp: light 0.91 (exposed above 0.35)…".
  - The first play showed no caption: mission teardown clears `NpcSpottedPlayer`, which the persistent UI had subscribed to in Awake. Fixed with its own event (`GameEvents.SpottedCaption`).
  - Guards who joined the alarm replaced the first caption with their own ("Near band 5.5 m … hunting"), so only the first spotter's caption shows now.
  - The first cone and touch alphas blew the ground out to white (linear-space blending). They were retuned lower.
- **Other play checks:**
  - M07 on Hunter: the worst-placed shout (15 m) reaches 3 NPCs; the mean is 1.6 of 36. Before D124 it reached the whole map.
  - M02: a watchman dazed as a sip leaves him wakes at 45 s, reports once (`WitnessReports` 1), searches, then goes back to Relaxed and wary.
  - Beckon from 25 m is refused with "Too far (25.0 m, reach 20 m)": no blood is spent and she doesn't move.
- **Tests:** 20 new (`QuickWinTests` 6, `ReadabilityTests` 14); 186/186 pass.
- **Ability sweeps:**
  - Part 1 is 34/34 on both M05 and M02.
  - Part 6 is 5/5, run twice.
  - The earlier failures (Beckon and Snare in M02, Herding in M05) didn't reproduce on these runs. The likely cause is state left by earlier evals in the same editor session (there is no domain reload between runs), but this is not proven. Watch for them in later sweeps.
- **Economy:** after D130, Ghost ends with 58 Marks, Typical 52 and Predator 70 (PROGRESSION, earning curve).
- **Campaign run** (`DevCampaignRun`, prefer free, optionals on): M01 to the ending passes, 0 errors in every mission; ending night_court, 107 Marks.

## 2026-10-02 — Redesign week 1, part 2: Exposure Field, cone-edge grace, cone redraw, the gym (D137–D140)
- **Exposure Field** (D137, SR.14 step 1): 0.5 m cells list the static lights that reach them and whether a wall cuts one partly; reads are exact. `DevExposureCheck` on all 14 missions: 10,000 points as lit, again with a third of the lamps off, and 2,000 around a moved lamp, with **0 points over 0.02** on every map.
  - Reads cost 1.7–10 µs against 3.5–21 µs for `LightAt`. Bakes take 13–68 ms (M10 is the largest at 164,000 linecasts). Coverage is 97–100%; the rest falls back to `LightAt`.
  - The first sweep failed on M05 and M13: border points sat in an untested strip (fixed by sampling 0.03 m outside the cell), then a grazing ray past a roof corner was lit at feet + 0.05 m but blocked at the tested height (fixed by testing the cell's whole read box, ±0.2 m).
- **Cone-edge grace** (D138): half rate in the outer 10° and outer metre, a 0.25 s onset that refills only after 1.2 s unseen; Merciful ×1.5. Searching cones red at 60% and dashed, blinded cones grey and dashed.
- **Cone redraw** (D139): ownership (the rising guard's cone on top, others at half), an origin arc at the feet, a faint fill and side outline on the dark far band within 8 m, outline-only cones for entranced guards.
- **Pins and difficulty ranges** (D140): middle-click pins up to 3, hover shows a guard's cone, Merciful counts 6 m as close, Apex shows unaware guards' near sector only and rims within 6 m.
- **Readability gym** (`gym.txt`) and the `investigate` script action.
- **Play checks in the gym:**
  - The near sectors, edge arcs, origin arcs and lit far fill read clearly.
  - The first far-band hatch (alternating vertex alphas) made a strong moiré that outshone the near sector. It was replaced with a faint fill.
  - At 0.05× time, Ilse 40° off a guard's axis at 5 m was in his grace (meter 0.10, rising slowly). His cone took the top sorting order and the seeker's yielded.
- **Tests:** 22 new (`GraceTests` 9, `ExposureFieldTests` 6, `ReadabilityTests` +6, `Mission_gym` validation); 208/208 pass. `EveryMissionMusicKeyHasATheme` caught the gym's missing music key.

## 2026-10-02 — Readability gym play check (D141)
- **Map fix:** every gym row began with a space, so the map sat one column east of its entities (the door landed in a wall). Stripped; LEVEL_DESIGN now warns about it.
- **Doorway spill:** the first room lamp was too far from the door for its 2 m-tile radius, and a corner lamp was shaded by the jamb. With the lamp two cells in (`radius=10`), light down the door's axis reads 0.85 at the door, 0.65 at 2 m, 0.37 at 4 m, 0.20 at 5 m. The walls either side stay at 0.06. The drawn rim follows the tongue; the ground glow itself is too faint to see (K34).
- **Shadow wall and lamp kinds:** the short wall casts its shadow in the field and in the rim; gas lamp, wall lamp and brazier rims match the field. The field agreed with `LightAt` at every point checked.
- **Hunter and roof:** the roof was dark and his stops left her on his cone's edge, so a lamp went on the roof and his stops moved. He then saw her on the lit roof, but his pinned cone stopped at the building face, and contextual cones never offered him (they skipped guards more than 3.5 m below her).
  - **Roof deck** (D141): rays stopped by a wall carry on over raised walkable ground and draw where the detection rule says she would be seen, with the edge bisected.
  - **Honesty sweep** at 0.25 m over the roof with the hunter held facing north: 634 points seen and drawn, 3,942 neither, 32 disagreements, all slivers within half a metre of an edge (24 drawn-not-seen, mostly under 0.04 alpha). Before bisection it was 94.
  - Contextual cones now use `ConeContext.FarReaches` (tested) instead of the 3.5 m cut.
- **Pins and states:** pinning a 4th guard dropped the oldest (max 3 holds). A blinded guard's cone is a 0.8 m grey stub (his vision range while blinded); it reads as blind, though the dashing barely shows at that size. The red Searching style hasn't been seen in play yet: the gym's seeker stays Investigating.
- **New issue:** K37, a raised guard's cone is drawn flat at his height and floats over the street.
- **Tests:** +1 (`OnARoofOnlyAGuardWhoLooksUpCountsHisFarBand`); 209/209 pass.
- **Next:** the WASD Stealth Readability Test (§44.1) needs 6–8 human testers. Hunt and mission tuning waits on it.

## 2026-10-02 — Ilse's ground disc and toe (D142)
- **Built:** `IlseDisc` (hidden ring / exposed fill, power tints, burn ring, watcher ticks, near warning, toe, pad rumble) and the pure `StepRead` rules. `Npc.SeenBand` and `Npc.Overlooks` came out of `Perceive`, which now judges `Vampire.SightLight`, so the disc and toe use the guards' own calls.
- **Play checks in the gym** (time at 0.002× to hold the frame):
  - Hidden ring at spawn; exposed fill by the shadow-wall lamp with ticks for the two guards that saw her (full length) and the seeker (meter 0.21, short).
  - Toe: Warm stepping toward the lamp with guards off, Seen into cross_a's lit far band, Flash 0.8 m outside his near sector.
  - Fixes from the check: her body hid the ground-level toe (ticks and toe now draw over her); Ember read cream, like a relaxed guard (linearised); the colour-only near warning didn't show against the bone ring (the rim now thickens).
- **Tests:** `StepReadTests` +17 (thresholds, leaves, toe table, reach, ticks, near warning, a walk into a cone); 226/226 pass.

## 2026-10-02 — Meter reading, boundary glint, off-screen pips (D143)
- **Built:** `MeterRead` (pure), `BoundaryGlint`, meter fill patterns and edge pips in the HUD (`EdgeArrow`), with `Npc.MeterFeed`, `HeardAt` and `MeterStarted`, and `ConeRenderer.AllShown`.
- **Play checks in the gym:**
  - Striped fill on cross_a, lit by the shadow-wall lamp's far band (meter 0.77); waves and dots forced on cross_a and cross_b.
  - Light-rim glint on that lamp, clipped where the wall stops the light; near-arc glint on cross_a's 6 m edge just behind her.
  - Edge pip with the camera turned away: on the right edge, his meter striped, the pointer outward and the wedge pointing back in, since he faced her. The first pip clipped its pointer at the edge, so the margin went from 34 to 52 px.
- **Bug found and fixed (K38):** an on-screen guard had no overhead meter, but an edge pip showed. During Update the camera sat 8 m above its pivot, looking level, because `AudioManager` moves the audio listener every Update and the listener was on the camera. It now has its own object. Mouse hover and click targets should be re-tested in play.
- **Tests:** `MeterReadTests` +15 (feed by band, smell and searchlight, latest sense wins, glint per band, start, pip rule, edge point, behind-camera mirror); 241/241 pass.

## 2026-10-02 — Spotted picture and debrief Detections page (D144)
- **Built:** the Spotted picture in `BoundaryGlint` (held band, spotter ring, sight-line) and `ConeRenderer.Spotter`; `SpottedRecord`, kept by `MissionController` and carried on `MissionResult`; `DetectionSketch` and the debrief's Detections modal; `MeterRead.PictureTime` (4 s), now also the caption's life.
- **Play checks in the gym:** cross_a spotted her in his near band at 4.8 m: his cone drawn full and on top, the near arc held in white, a ring at his feet and a line from his eye to her. Opened the debrief with the night's records: Detections (2) showed a sketch for cross_a (cone cut by a wall, near arc in white) and cross_b, each with its caption.
- **Not built (D144):** slow-mo, dimming others, searchlight pool and noise ripple in the world, Hunt causes, saved records.
- **Tests:** `SpottedRecordTests` +6 (rays, bounds, walls, fit, map, cap); 247/247 pass.

## 2026-10-02 — The light knee (D145)
- **Built:** `LightKnee` (pure) and knee cookies in `GameLight.FitUnityLight`/`FitSpot`; pool spots for point lights with the shadow moved onto them; fixture shadows off; `Tools/knee_check.sh` (rendered edge vs contour).
- **Measured (M02):** street lamps s1, s2, lodge1: edge +0.03 m, contrast 4.3× (s1 before: −0.87 m, 1.1×); hall candle +0.01 m, 3.5×; gate wall lamp 0.00 m, 3.6×; brazier +0.13 m, 2.3× (3.9× alone in the gym); tavern window and fire 1.4–1.5×.
- **Seen:** the gym shadow-wall lamp's pool is flat with a clear edge; the post's top had been throwing a 1 m hexagonal shadow under the lamp (fixed). The brazier pool is round with its edge on the contour. In M02 the street pool reads clearly; the tavern fire's pool stops at the room's walls.
- **Bug found and fixed:** a new pool spot kept a 30° inner angle because Unity clamps the inner angle to the outer one already set.
- **Tests:** `LightKneeTests` +4; 251/251 pass.

## 2026-10-02 — Cone state marks and time to contact (D146)
- **Built:** the Wary ring at a guard's head; `Visual.IntentPaths` (dotted marching path, end ring) for Investigating (amber) and Searching (red 60%); `ConeContext.TimeToContact` feeding contextual cones and edge pips.
- **Seen (gym):** the seeker's amber path across the yard to its ring; after an alarm, three Searching guards' dim red paths converging on her last known position; the Wary guard's small ring.
- **Measured:** the carrier walking at her: contact 2.0 s at 9 m, 1.4 s at 8.1 m, 0.8 s at 7.1 m (his near range is 6 m, his speed 1.6 m/s). His cone had already shown from 17 m by the far-band rule, so on a straight approach the new rule adds nothing; it matters for turns.
- **Tests:** 256/256.

## 2026-10-02 — One HUD threshold and accessible cones (D147)
- **Built:** the HUD eye with one threshold (DARK/LIT, SHADOW removed); a slow pulse on Suspicious cones; settings *Always show all vision cones*, *Cone key toggles instead of hold*, *Shape-coded cone states*; high contrast doubling edge widths. The controls hint says "Press Alt" when the key toggles.
- **Seen (gym):** the investigating seeker's large broken origin arc; alerted guards' large solid arcs after an alarm; always-all plus high contrast showing every nearby cone with thick edges; the eye reading DARK in the dark.
- **Not seen:** the toggle by a real key press (the latch logic is unit-tested); the pulse (a still can't show it).
- **Tests:** 260/260.

## 2026-10-02 — The Readability Test harness (D148)
- **Built:** `Stealth/ReadTest` (pure: scoring for Q2–Q5, the stop clock, probe timing and rotation, the verdict and report) and `Core/ReadTestRunner` (telemetry, freeze probes, the cause question, the probe screen, JSON logs); the *Readability test mode* setting; the `routechoice` script action and one route choice in the gym (the dark lane behind the lit room, or past its doorway).
- **Seen (gym):** the freeze showing the overlays with the question and the subject ringed, then the dark ask screen; Q1 and Q6 answered by Y/N, Q7 by a digit (logged as Near, with the Spotted caption), Q2, Q3 and Q4 clicked on the true boundary (0.00 m), a straight Q5 trace through light scored unsafe, the route choice logged as safe, the log and `report.txt` written to `Temp/DevSaves/readability` on leaving.
- **Fixed while checking:** a click without mouse movement used the old cursor; overdue probes came back to back; a flare could be Q4's lamp; Q7 came after every repeat spotting in an alarm; the gym's route offer was dropped because `on start` runs before the log opens.
- **Not seen:** a pad (stick cursor, d-pad list); a correct Q5 trace drawn by hand.
- **Tests:** 269/269.


## 2026-10-02 — The cone truth sweep (D149)
- **Built:** `DevConeCheck` with `Tools/cone_check.sh`. It builds every seeing guard's full cone off screen
  (`ConeRenderer.DrawFor`), reads the mesh back, and compares the drawing with `SeenBand` plus line of sight at random
  standing points. Points too close to call are counted, not judged. Also `ConeTruthTests` (3).
- **Found and fixed:** captives in M09 drew full cones though they can't see (`Seeing` now needs `CanSee`). In M06
  the lit far band was drawn over a hedge where the leaves hide her (far samples use the judged light). The cone light
  cache is now cleared on leaving a mission.
- **Result:** gym and M01–M14 pass with 0 disagreements (6,800–51,700 points per map). Tampering with the read-back
  makes it fail.
- **Not covered:** the roof deck (checked by eye). The cache lag behind a carried lantern is K39.
- **Tests:** 272/272.

## 2026-10-02 — Hatched dark band, hover watchers, state styles checked (D150)
- **Built:** the dark far band within 8 m hatched with world-space diagonal lines (Overlay `_HATCH`, `cone_hatch` material), replacing the faint tint; `Visual.HoverWatchers`, a ring at the feet of every guard whose cone covers the ground under the cursor.
- **Seen (gym):** lines over cross_a's dark far band beside Ilse, solid fill over lit ground; the cursor in front of cross_a ringing cross_a and cross_b (and `Covers` agreeing for all six guards); cross_b forced into Searching drawing red with a dashed end arc (alternating alpha 0.12/0.02) and his dotted path; cross_a in a blind zone drawing grey, shrunk to his 0.8 m sight.
- **Spec gap:** the style table says a blinded cone draws both ranges at ×0.8; the rule blinds him to 0.8 m, and the cone draws the rule.
- **Not done:** K37 (projecting a raised guard's cone down onto a lower street), since no mission has such a guard yet.
- **Tests:** 272/272.

## 2026-10-02 — Save clock, camera, Dossier (D152–D154)
- **QW8 save (D152):** three rotating quick slots, the save-age line above the HUD eye, and no save while a hostile
  human sees her. M02: quicksaves rotated quick0→1→2→0; in front of npc_w1 the line read "Watched: can't save" and
  no file was written; quickload took the newest.
- **QW11/QW12 camera (D153):** distance 20, tap Z/X for a 45° snap, hold to turn, the move frame locked while the keys
  are held, and a velocity lead. Gym: the pivot sat 1.6 m ahead walking and 2.5 m running, and settled back on her
  when she stopped. Simulated key presses didn't reach the editor, so tap versus hold is covered by tests only.
- **QW13 Dossier (D154):** answers from M03, two at most, one per tree, lapsing after two quiet missions, each shown
  with its deed. M03 briefing: "You left 7 dead: tonight, paired patrols." / "You took the mist 5 times: tonight,
  censer-bearers."
- **Not done:** Ironblood's +1 Mark; countermeasure bodies in M03–M06 (mission content, after R22).
- **Tests:** 281/281.

## 2026-10-02 — Shadow Dash (D155)
- **Built:** Shadow Dash on Q (pad East), replacing the Shadowstep teleport. A 6 m navmesh burst, two charges that
  refill only in the dark, unseen in the dark and a +0.3 blur in light, the Rise up a wall of up to 4 m, and Umbral
  Step and Between Bars as the Shade nodes that replace Shadowstep's two. Old saves carry the nodes over. HUD Dash slot.
- **Seen (gym, god mode):** 6.0 m per dash; one charge back after 5 s of dark; a closed door stopped her from either
  side; dashing into a dark 3 m wall put her on top, into a lit one or the 4.5 m pipe stopped her at the foot; the
  blur took the watching guard from 0 to 0.30 and left the other five at 0.
- **Sweeps:** ability sweep part 1, 34/34 (M02, `shade.dash` 6.0 m, charges 3 → 2); part 6, 5/5 (M05,
  `shade.dash_bars`: the bars stop her without the node, she goes through with it).
- **HUD:** the charges are drawn as small diamonds, not text glyphs (the font made ◆/◇ into dots): 1 of 2 showed one
  filled and one hollow; 0 showed both hollow on a dimmed slot.
- **Sweeps:** also part 7 (M01, footsteps, doors, hiding, the gate) 5/5 after the dash's silent steps.
- **Not done:** Pounce through the dash (§24) and Warm's faster recharge (both need their own systems). Key presses
  weren't simulated (the editor ignores them unfocused), so Q itself is untested by hand.
- **Tests:** 285/285.

## 2026-10-02 — Movement Test kit (D156)
- **Built:** `movegym` (the corner-and-door gym); *Movement test mode*, which logs wall-sticks with their positions,
  door stops, dashes, camera turns and zooms, and detections at a cone edge, then asks two questions when the map
  ends; and **Test maps** on the main menu while a test mode is on. The maps run on a throwaway campaign in a sandbox
  folder, which also lets Readability testers reach the gym without the console.
- **Seen (driven by `DebugMove`, god mode):** a 5 s push into a wall counted 1 stick, not 250 frames. Steering
  north-west into the corner at the jagged diagonal's foot counted 1 (a real stick: the input pointed into the
  corner). Pushing north-east up the diagonal ran its full length with none. A shut door counted a door stop and no
  stick, and a 45° camera snap 1 turn. At the exit the two questions came up over the debrief, and the JSON and report
  were written. Through Test maps → Movement Gym, saves went to `playtest/`; Continue put back the profile, the
  campaign and the save root and returned to the main menu.
- **For people:** the test (5 new players, KB/M and pad: movegym and M02, Main menu → Test maps with *Movement test
  mode* on). Logs are in `movement/` beside the saves, with the verdict in `report.txt`.
- **Tests:** 292/292.

## 2026-10-03 — Housekeeping
- **QW14 (R15):** checked every save for the Bug B Dossier (20k Sightings). None has it: all Dossiers are empty or
  normal, so nothing was reset and nothing outside the project was touched.
- **Dev logs:** deleted the 33 readability sessions and the movement logs from `Temp/DevSaves` so the R22 and
  Movement Test reports start clean.

