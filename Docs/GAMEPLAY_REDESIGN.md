# Vespertine: Gameplay Redesign (follow-up to GAMEPLAY_AUDIT.md)

**Status:** design proposal, 2026-10-02. No code has been changed.
**Reads with:** `GAMEPLAY_AUDIT.md` (problems), `GAME_DESIGN.md` (current design), `DECISIONS.md` D114–D119 (direct control).
**Immutable:** Ilse moves with WASD / left stick, directly, relative to the camera. Nothing below takes that away.

**Revision 2 (2026-10-02): stealth readability.** The direction of Revision 1 is approved. This pass tightens it so the player has enough information to decide at WASD speed.
- **New section: STEALTH READABILITY** (after §14). One visual language for:
  - cones;
  - near bands;
  - light coverage;
  - Ilse's exposure;
  - noise, ranges and Hunts;
  - off-screen threats.

  It also adds the **Exposure Field** (one source of truth for light), contextual cones that don't need Alt, and the **Spotted Explanation**.
- **New root cause R6** (§6.2): the game shows a different world from the one it simulates.
- **Corrections to Revision 1:**
  - the near band is a *sector* inside the cone, not a ring (§7.3, P10);
  - guards walk at 1.6 m/s, so sneak already keeps pace (§10);
  - §15 had three new problems, not four.
- **New section: RE-EVALUATED MECHANICS.** Revision 1 mechanics are cut or replaced:
  - passive drain → Warm blood;
  - Gorged penalties → Overflow;
  - Shadowing speed-match → footfall masking;
  - the objective lockout → consequences;
  - mastery unlocks → Preparations.
- **New §20.4:** a Hunt budget, and why staying unseen pays.
- **Rewritten:** Top 20, Top 10 and acceptance criteria. **New:** the WASD Stealth Readability Test (§44.1).
- Readability is woven into §7–§14, §19–§20, §23–§24, §28–§30, §33–§34, §37–§39, Quick Wins, Fundamental Changes, Research, §47 and the verdict.

Evidence tags, as in the audit:
- **[Code]** read in source;
- **[Played]** observed in the editor with the bot harness;
- **[Research]** from the market report (Steam reviews, postmortems, GDC talks);
- **[Proposal]** my design, not yet tested.

---

## 0. The redesign on one page

**The commitment.** Vespertine becomes a **direct-control predator stealth game** in which being found starts a **hunt you can win**. It is not "Shadow Tactics with WASD".

Every system serves one arc: *prey who hides from light* → *hunter who isolates and feeds* → *predator whom the city organises against*.

**The ten decisions everything else follows from:**

| # | Decision | Replaces |
|---|---|---|
| 1 | Fix the two AI bugs (shout radius ×15; dazed wake loop) before any design work | — |
| 2 | **The Hunt.** Detection goes Suspicious → Spotted (a 1 s **shout wind-up** you can interrupt) → **Hunt** (local, with a visible last-known position and break-contact rules) → Lockdown (map-wide, costly, survivable) | A binary alarm: nothing in Act I, death at mid-game |
| 3 | **Shadow Dash** replaces the mouse-aimed Shadowstep teleport. It is a navmesh-bound burst in the direction you are moving, with 2 charges that recharge only in darkness. It is baseline from M01; the Shade tree upgrades it | A 12 m line-of-sight teleport aimed with the cursor while the keys move the body |
| 4 | **Grab, drag, feed.** F grabs and silences the victim with a short lunge. While you hold them, WASD drags them toward the dark. Then sip or drain. Feeding in darkness is faster and unseen | A stationary 1.6/3.2 s channel that any key press cancels |
| 5 | **Abilities cast while moving.** Tap casts on a soft target in your aim direction; hold previews. Nothing walks Ilse into range | Hover-aim mode plus walk-into-range auto-pathing |
| 6 | **A camera built for direct control:** occlusion cutaway, velocity look-ahead, 45° snap rotation that doesn't swing your heading, and off-screen threat markers | A tactical orbit camera with no occlusion handling |
| 7 | **Vampire Laws are the level-design contract:** running water, thresholds, holy light, garlic and silver bind every power. Every gate has at least two answers | Powers designed separately from map gates |
| 8 | **A playstyle-paid campaign.** Powers from the end of M01; four trees cut from 42 nodes to about 32; challenges pay and unlock diegetic **Preparations** (bribed servants, keys, compromised guards); the Dossier starts at M03, capped at 2 visible countermeasures per mission | Act I without powers; first-clear-only rewards; a 42-node tree |
| 9 | **The inversion.** Late in the game, Terror makes the Watch break and flee. Only the Vigil hunts you. The prey-to-predator arc becomes a mechanic, not a stat curve | Late game equals the early game with bigger numbers |
| 10 | **Stealth Readability** (Revision 2). The game draws the rules it judges by, from the same numbers:<br>• light pools re-rendered to end where exposure ends;<br>• cones that appear when they matter;<br>• near sectors that are always drawn when close;<br>• a ground disc and a step preview under Ilse;<br>• a one-line explanation of every detection.<br>**"If his colour is on the ground under you, he can see you there."** | Light that looks 1.5× bigger than it is; cones only on Alt; an unexplained Spotted |

**What I would not do:**
- bring back a planning mode (it was built and cut in play-test, D115);
- add bat form or ceiling walking;
- add a parry or combo combat system;
- switch to a third-person camera;
- cut missions;
- remove quicksave.

---

## 1–5. Ground rules as applied

- **WASD stays** (brief §1). I also treat the controls in D114, D116, D118 and D119 as the foundation and build on top of them: camera-relative movement through `NavMeshAgent.Move`, push-to-climb, the follow camera, and three gaits.
- **Sunk cost is ignored** (§2). The proposals below cut or replace finished work:
  - Shadowstep's teleport;
  - Bloodmend;
  - Corpse Puppet and Living Lie;
  - roughly 10 nodes;
  - the walk-into-range layer of ability targeting;
  - the six-slot loadout.
- **Research challenges the design** (§3). Section 46 maps each research finding to a game change.
- **Big changes encouraged** (§4). See §36 and §41.
- **Not Shadow Tactics with WASD** (§5). There is one protagonist. Pursuit is a pillar, and feeding is a physical act you steer. Its closest relatives are Mark of the Ninja's readability, Dishonored's survivable failure and Aragami 1's light-cost tension. It borrows none of their structures wholesale.

---

## 6. What the audit actually found: problems and root causes

### 6.1 Extracted problems

| Category | Problems (audit id) |
|---|---|
| Critical | I-01 shouts reach ≈225 m; I-02 dazed victims never wake and report every frame |
| Major | I-03 no survivable detection middle ground; I-05 no powers for ≈40 min; I-06 Act I detection costs nothing; I-04 Shadowstep dominance (**partly wrong, see §7**) |
| Significant | I-07 challenges unpaid; I-08 action auto-pathing (**overstated, see §7**); I-09 Predator overflows progression and Ghost trails; I-10 toolkit overlap (42 nodes); I-11 invisible near band; I-12 save UX |
| Dominant strategies | Quicksave every guard; sip everything (bug); one movement power for everything; Apex→Rend refund chains; walk through Act I alarms |
| Boring behaviour | Waiting for patrol windows with no tool to create one (Act I); reloading after every sighting (mid-game) |
| Missing expectations | A visible near band; save-age timer and slots; camera occlusion; aim mode that doesn't fight movement; rewards for how you play |
| Progression | Locked for 3 missions; stat-led Awakening; Predator out of things to buy at M10 |
| Abilities | 6 ways to remove an unaware human; 5 ways to neutralise vision; Bloodmend contradicts the dark-regen rule |
| AI | Bugs A and B; panicked civilians act as map-wide sirens (a consequence of bug A); near band unreadable |
| Pacing | Powerless start; challenge cliff at M04–M07; Predator maxed at M10 |
| Controls | Two movement authorities (keys versus auto-walk); mouse aim for point abilities while the keys steer; no camera occlusion |
| Level design | Gates designed without a movement-power contract |
| Replayability | First-clear-only rewards; challenges are records |
| Fantasy | No vampire verb for 40 min; folklore thinner in play than in the GDD; "humans hide from me" only at A9 with full blood |
| Full-game risks | "Powers trivialise stealth"; "quickload simulator"; "not a vampire for the first hour" |
| Industry gaps | Readable failure states; save UX; demo shows no powers |

### 6.2 Connected root causes

Six root causes explain almost every symptom (R6 was added in Revision 2).

**R1. Detection has no designed middle.** The game knows "unseen" and "everyone knows" and nothing in between.
- **Symptoms:** I-01, I-03, I-06, quickload dominance, mid-game death wall, chaotic players quitting, emergence blocked, panicked civilians as sirens.
- **Why:** the alarm is a single integer (`Alarm` 0–3 in `AIDirector`). It is driven by shouts with no locality (bug A) and by no player-facing state such as "they're hunting *here*". Nothing in the design describes what the player does *during* an alarm.

**R2. The player has too few verbs that *create* an opportunity, especially early.**
- **Symptoms:** waiting for patrols, Act I powerless, low ability use before M04, Shadowstep used for everything once it arrives, feeding as a refill.
- **Why:** Act I has observe, walk, feed and carry. Beckon arrives as a story grant in M03 [Code CampaignState.cs:194]. Smother, Mesmerize and Pounce need Marks and an open tree. The first manipulative verb arrives after the refund window.

**R3. Controls were converted to direct movement, but the layers above movement were not.**
- **Symptoms:** auto-walk into range (I-08), a cursor-aimed teleport, a two-stage aim for Beckon and False Orders, a stationary feed channel that any key cancels, a camera with no occlusion, thralls driven by portrait swaps for "coordinated" kills.
- **Why:** D114 changed locomotion and kept the click-era action model ("Feed, carry and ability keys still walk her into range"). Every layer above locomotion still assumes the player points and waits.

**R4. Powers were built without a counter-rule, and the counters were built as a separate system.**
- **Symptoms:** Shadowstep's breadth, the Gloom+Shadowstep loop (the Aragami pattern), Bloodmend undercutting dark-regen, toolkit overlap.
- **Why:** the Vampire Laws exist (running water is enforced for Shadowstep's aim [Code VampireAbilities.ValidateAim]), but nothing states them as a contract that every ability and every map obeys. The Dossier countermeasures (7 of them [Code]) arrive from M07, after the player has built habits.

**R5. Rewards ignore how you play.**
- **Symptoms:** I-07, I-09, replayability, Act I recklessness rewarded, no reason to try another build.
- **Why:** `BuildResult` pays first clear, optionals, secrets and notables [Code MissionController.cs:910–940]. It never reads sightings, alarms, loads or challenges.

**R6. The game simulates one world and shows another.** (Revision 2)
- **Symptoms:**
  - "That looked dark enough";
  - deaths at 4–5 m in darkness (I-11);
  - "How did he see me?";
  - Alt held while trying to steer;
  - routes chosen by fear of the whole visible pool.
- **Why:**
  - Unity renders lights about 1.4–1.6× wider than the 0.35 exposure contour, and lamps outside the shadow budget shine through walls that gameplay light respects [Code `GameLight`, `LightSystem`, `BudgetShadows`].
  - Cones are hidden unless Alt is held.
  - The far band samples light at only 2 points per ray.
  - The near *sector*, the 1.4 m touch zone and height rules are not drawn.
  - At the moment of Spotted, the game knows exactly why, and shows nothing.

  Full analysis: STEALTH READABILITY, SR.1.

Everything in §15 onward is aimed at one of these six. **R1, R3 and R6 are the three that WASD makes urgent.**
- A direct-control player expects their hands to be able to save a bad moment, and expects their hands never to be taken away.
- They also need **to read the rules at the speed their hands move**. Unlike a click player, they can't stop to measure.

---

## 7. Challenging the original audit

I re-checked the audit's findings against the code and against the bot harness. **Two findings were wrong or overstated. Three recommendations were too small.**

### 7.1 Corrections (the audit was wrong)

**I-04 "Shadowstep bypasses M07's crossings" was a test-harness artefact.**
- My bot's `cast` helper called `Vampire.Act` directly and skipped `ValidateAim`.
- A real player's aim goes through `ValidateAim`, which refuses with "Running water" when `CrossesWater(Feet, point)` is true. It samples every 0.75 m against `TileKind.Canal`.
- I re-ran the check in the editor in M07 through the real code path. All three crossings I used (16,46→21,45 over the Leat; 43,35→47,35 over the river; 58,38→62,38 over the Fen Cut) return `CrossesWater = True` [Played, 2026-10-02].
- **So the running-water rule already exists for the player, and M07's crossing structure holds.** The audit's "1:15 completion" and "Running water should block Shadowstep" recommendation should be withdrawn.
- **What survives of I-04 is narrower:**
  1. `Cast` itself doesn't re-check water, so scripts and future callers can bypass the rule. That is a latent bug: move the check into `Cast`.
  2. The unverified concern is that a 12 m line-of-sight teleport to any dark point still skips walls, fences and climb routes, and is cheap (12 blood, 1 s cooldown).
  3. **The stronger objection is control fit, not balance.** It is a cursor-aimed point teleport in a game whose body is steered by the keys (§13).

**I-08 "Feed auto-chase has no cap" is overstated.**
- In `TickDirect`, any movement input cancels `_pending`, `_useTarget`, the channel and the path [Code VampireMovement.cs:74–78]. A human who touches WASD takes the walk back.
- The bot pressed no keys. That is why it chased Wick across the map.
- **The real problem is different:** the game has two movement authorities. When the player lets go of the keys, the character moves on its own. That is a click-era behaviour in a direct-control game, and it should go entirely (§13), not be capped at 3 m as the audit proposed.

**Minor correction.** The audit says Act I has "no vampire verb beyond feeding".
- Awakening 3 grants roof leaps, and A4 grants Wallcrawler, in code [Code Vampire.cs:185–186].
- Beckon is a story grant in M03.
- The conclusion stands (no *manipulative* power before M03–M04), but the vampire does gain traversal through Awakening.

### 7.2 Recommendations that were symptoms, or too small

| Audit recommendation | Problem with it | Better answer |
|---|---|---|
| I-03: three alarm stages with a break-contact timer | Correct direction, but it describes the AI's states, not what the player *does*. A break-contact timer alone gives the Aragami 2 outcome: run, hide, reset, consequence-free | **The Hunt (§20–21):** a shout wind-up you can interrupt, a visible last-known position, pursuers who split up and can be picked off, persistent costs (Wary, lamps relit, Dossier weight) and Lockdown as a survivable map state |
| I-05: open the Arts after M01; free Smother | Better, but a free Smother is still a click-a-lamp verb. The early *body* stays a human body | Give a **body** verb from minute one (Dash, drop-feed and grab-drag are baseline) and the **first chosen power** at the end of M01 (one of Smother, Mesmerize or Pounce) |
| I-06: lockdown seals the objective door | That punishes, but doesn't create play. Hunter would become stricter, not more interesting | Act I alarms raise a **Hunt with a lantern party** that clears in about 40 s if you break contact. Challenges pay, so clean play is visibly worth more |
| I-10: prune to ≈30 nodes, Bloodmend first | Pruning without changing *what each verb is for* repeats the overlap at a smaller size | Prune by **role**: one verb per job per tree, plus a baseline kit every build shares (§24) |
| I-12: save-age timer and slots | Correct and expected. But quicksave dominance comes mainly from R1; fix R1 first, or the timer only nags | Keep, ranked after the Hunt. Add an opt-in **Ironblood** challenge, not a forced mode |
| "Running water blocks Shadowstep" | Already true for the player (above) | Write the Vampire Laws into the ability and level contract (§33.4) so the rule applies by construction to every new power |

### 7.3 Recommendations I still agree with
- **The bug fixes:** confirmed in code and play; still the first thing to do.
- **Paying challenges.** Research and code both support it.
- **Removing Bloodmend.** It still contradicts the best rule in the game.
- **Showing the near band** (corrected in Revision 2). WASD players steer by feel; an invisible 4–7 m rule is the most common unfair death.
  - The audit's *ring* was the wrong shape. `Classify` checks the cone angle before the near range, so the near band is a **sector at the front of the cone**.
  - Only the 1.4 m peripheral zone is all-round.
  - A ring would mark safe ground behind a guard as dangerous. SR.5 draws the true shapes.
- **The Dossier from M03, capped and shown.** This is the MGSV lesson.

### 7.4 What the audit could not judge and this document cannot either
- How the game *feels* by hand: acceleration, camera and cone-edge tolerance.
- The bot drives `Agent.Move`, not a keyboard, and editor automation cannot send keystrokes (KNOWN_ISSUES K27).
- Every number in §10–§13 is a **starting value for a hand-played tuning session**, not a measured result.

---

## 8. TARGET PLAYER EXPERIENCE

### Movement
- Moving Ilse should feel like **a predator settling her weight**. She starts instantly and stops dead. A sneak outpaces a walking guard, so the player chooses the gap. A run is a commitment you can hear.
- She never moves without the player's hands.
- Her vampire body is visible in her movement from the first minute:
  - a short shadow dash;
  - silent drops of any height;
  - walking into a pipe climbs it.
- The best feeling in the game is **closing the last three metres** on someone who hasn't turned around.

### Camera
- A high three-quarter tactical view, so cones, lamps and patrol routes stay readable. It follows Ilse with a lead in the direction she moves.
- **Nothing ever hides her or a threat near her.** Walls and roofs between the lens and her are cut away.
- A threat off-screen is marked at the screen edge.
- Rotating the camera never yanks her off course.

### Stealth
- The constant decision is **"which person, where, and how do I get them alone?"**, not "when will the patrol turn?".
- Light is the map:
  - dark is safe outside a guard's near sector (4–7 m by type, always drawn when close);
  - light exposes you within his far range (10–18 m);
  - darkness is something you can make, but making it is noticed.
- **The ground tells the truth.** If a guard's colour is on the ground under Ilse, he can see her there. A lamp's pool ends where it stops exposing her. She never has to guess "dark enough" (STEALTH READABILITY).

### Detection
- Mistakes are **local and readable**. Every detection says who saw her, from where, and which line she crossed (SR.10).
- A guard who sees you **winds up a shout** (1 s, a red ring growing at his mouth). If you silence him, nobody else knows.
- If he shouts, a **Hunt** forms around that spot: his group, about 15–20 m, with lanterns. The rest of the map hears a bell only if someone reaches one.

### Pursuit
- Being hunted is **the second-best part of the game**. You break line of sight, go up, dash through a gap, pour into mist under a door, or wait in a dark loft while the lanterns pass.
- Or you turn around and **take the one who split from his partner**.
- A hunt you escape **leaves marks**:
  - the area stays Wary;
  - lamps get relit;
  - the Dossier weighs it.
- A hunt never ends in an automatic reload.

### Combat
- Combat is a **short, expensive, loud outburst**. Its purposes are to:
  - silence a witness;
  - break out of a grab;
  - get through a lone watchman.
- Early on it is barely possible and always costs. Late on, a Predator can **break a whole Watch post in ten seconds**, and pay for it with half their blood and a Lockdown.
- Vigil hunters (silver, flares) and groups of three or more stay dangerous for every build, all game.

### Feeding
- Feeding is **a physical act you steer**:
  1. grab from behind or the side;
  2. drag into the dark;
  3. choose sip (quick, a witness who'll wake) or drain (full, a body, a stain).
- Whom you feed on matters: blood types give humours, and notables give Marks.
- Where you feed matters: in light it is seen across a guard's whole cone; in the dark, only inside his near sector. The disc shows who can see the feed (SR.7).

### Blood
- Blood is **fuel, health and a noise problem at once**:
  - below 15% your heartbeat is audible (Starving);
  - after a feed you are **Warm** for a while: faster dark regen and Dash recharge, a moment to push;
  - blood beyond max **overflows** into healing and longer Warm.
- No blood drains while you wait. You are always deciding **whether to feed now and leave evidence, or push on hungry**, and **when to spend your Warm**.

### Abilities
- Powers change **what is possible from where you stand, while you keep moving**:
  - call a guard to you;
  - freeze him;
  - kill a lamp;
  - pour into mist.
- Every power obeys the Vampire Laws, and every enemy type has a readable answer to at least one power.

### Progression
- The campaign changes **how you play, not your numbers**:
  - **Act I:** avoid and pick off.
  - **Act II:** isolate and feed.
  - **Act III:** manipulate and control a block.
  - **Act IV:** break posts and terrify.
- Your tree choice (Predator, Shadow, Puppeteer, Blood Mage) changes **how you solve the same checkpoint**.

### Enemies
- The Watch is many and fragile, and **breaks** late in the game.
- The Vigil is few and dangerous:
  - they **look up**;
  - they **carry light**;
  - they **track blood**;
  - they **ward** doors and minds.
- What they bring is visible before each mission (the Dossier), and each piece has a counter.

### Levels
- **Rings, not corridors.** Every lit hub has at least two exits into a dark loop, and every hunt space has a vertical escape.
- Roofs are the vampire's road, and the Vigil's hunters watch them.
- Running water and thresholds are the gates. Each gate has two or more answers.

### Vampire fantasy
What makes Ilse different from a human stealth character:
1. **Light hurts and dark heals.** Her health is literally restored by darkness, paid in blood.
2. **She feeds.** Every takedown is also a meal, a decision and a body.
3. **She bends minds.** Guards walk to her.
4. **She is bound by old laws.** Water, thresholds and holy light.
5. **She becomes the thing the city fears.** Late in the game, the Watch runs.

---

## 9. Direct control changes player expectations

| Expectation | Does the build meet it today? | Change needed (section) |
|---|---|---|
| **Responsiveness** | Mostly. Accel 28 m/s² and decel 40 m/s² give a 0.12 s start and a 0.085 s stop at walk speed [Code MoveMath.Ease]. Turning is 900°/s, effectively instant | Keep. Add a sneak-start tweak (§10) |
| **Agency** | **No.** Feed, use and ability keys move the character on their own if the player isn't pressing keys (D114) | Remove all auto-walk (§13) |
| **Immediate feedback** | Partly. Step rings show noise (D119), and the light eye shows light. The near band, the touch zone, the shout radius, the alarm's reach and the *cause* of a detection are invisible | Near sector and touch circle; shout wind-up ring; Hunt outline; the Spotted Explanation (STEALTH READABILITY, §20) |
| **Readability at speed** (Revision 2) | **No.**<br>• Cones need Alt held.<br>• Rendered light is about 1.5× wider than the exposure it causes.<br>• The light read is a corner eye.<br>A WASD player can't hold Alt, steer and look at a corner at once | Contextual cones (SR.6), truthful light pools and rims (SR.4), Ilse's ground disc and step preview (SR.7–8) |
| **Movement mastery** | Little to master. Three gaits and push-to-climb; no movement skill that matters under pressure | Shadow Dash with charges and darkness recharge; drag; drop-feed (§11) |
| **Improvisation** | Low before M04 (no manipulative verbs); mid-game improvisation is cut short by death in 10 s | Baseline Dash and Beckon; the Hunt (§19–21) |
| **Reactive ability use** | **No.** Point and NPC abilities need a hover aim mode, and two-stage aims for Beckon and False Orders | Tap-to-cast soft target; hold to preview (§13) |
| **Escape options** | Before Shade/Mist: only running and darkness. With bug A, escape is moot | The Hunt plus Dash plus vertical escapes in every hunt space (§20, §30) |
| **Fluidity** | Broken by: shut doors that stop her (G to open); a feed channel that pins her; aim mode | Push-to-open doors; drag while feeding; casting while moving (§10, §28) |
| **Faster tactical decisions** | The game rewards waiting (patrol windows) and reloading over deciding | Opportunity-creating verbs; detection that becomes a situation, not a reload (§19–20) |

**Verdict:** the *locomotion* meets direct-control expectations. **Everything layered on top of it still behaves like a click-to-move game.** That mismatch is the biggest WASD problem in the build, and it is fixable without touching the movement code's core.

---
## 10. Movement system review

**Is moving the vampire itself enjoyable?**
- **Locomotion: yes, probably.** The ease curves are crisp, she faces where she goes, she slides around humans, and climbing by walking into a pipe is the right convention (D116).
- **Moving *near enemies*: not yet.** The boundaries she must steer around aren't drawn. Doors stop her. Feeding pins her in place. Nothing rewards closing distance with skill.

| Element | Now [Code] | Proposed [Proposal] | Why |
|---|---|---|---|
| **Sneak** (hold Ctrl) | 1.75 m/s, silent | **2.2 m/s**, silent (1 m noise on gravel, glass and water) | *Corrected in Revision 2:* guards walk at **1.6 m/s** (1.76 Wary) [Code `Npc.WalkSpeed`], so 1.75 already keeps pace, but with a 0.15 m/s margin it takes 20 s to close 3 m. At 2.2 the player closes 3 m in 5 s. Sneak must be the gait you *hunt* with, not the one you creep with |
| **Shadowing** (new) | — | **No speed-match** (cut in Revision 2; see RE-EVALUATED MECHANICS). Instead, **footfall masking**: walk-gait steps are silent within 3 m behind a walking, unaware human. His touch circle and near sector are drawn, and the step preview warns if he stops or turns | Tailing is the vampire fantasy, and it must be the *player's* skill. A speed-match is a second movement authority (R3) and hides the stop-and-turn risk |
| **Walk** | 3.4 m/s, 3.2 m noise | Unchanged | The 3.2 m radius is the "near people, slow down" rule players learned in D119 |
| **Run** (Shift) | 6.8 m/s, 8 m noise | Unchanged speed; **lit running counts ×2 toward detection** (already in the GDD) | Running must stay a loud commitment |
| **Gorged** | — | **Cut in Revision 2.** No speed or noise penalty: over-max blood becomes Overflow (§29) | A speed tax on success damages the WASD feel |
| **Acceleration** | 28 m/s² | 28 (walk/run); sneak reaches speed in 0.08 s | Stalking needs instant creep starts |
| **Deceleration** | 40 m/s² | Unchanged | Stopping dead at a cone edge is a WASD skill; keep it crisp |
| **Turning** | 900°/s, faces the move direction | Unchanged while moving. **While an ability key is held she faces the aim direction and moves freely (strafes)** | Lets her keep a target in front while side-stepping, without a general strafe mode |
| **Strafing** | None | Only while aiming (above) or dragging (she faces the victim and backs into the dark) | General strafing adds nothing to a top-down view; contextual strafing does |
| **Collision** | `Agent.Move` on the navmesh; 0.62 m separation slide around humans | Keep. **Add corner slip:** if movement into a wall is within 35° of parallel, deflect along it at 90% speed | A top-down WASD character that sticks on door frames and wall corners is the most common direct-control complaint. Separation already exists for humans; extend it to geometry |
| **Doors** | `Door.ClampStep` stops her at a shut door; G opens | **Push to open** after 0.15 s (like climbing): silent at sneak, a creak (4 m) at walk, a bang (8 m) at run. Locked and threshold doors stay shut and show why | Removes a stop-and-press on every interior route; door noise becomes a gait decision |
| **Interaction ranges** | FeedRange 1.35 m, UseSlack 0.35 m | **Grab 1.8 m plus a 0.25 s lunge from up to 2.6 m** within 60° of facing; Use 1.6 m with a highlight on the nearest target in facing | Direct-control players overshoot by half a metre; magnetism converts near-misses into the intended action |
| **Movement near enemies** | A near **sector** of 4–7 m sees her in any light, and a 1.4 m touch zone sees her from any side. Neither is drawn | **Contextual cones** (SR.6): any guard whose detecting region is within 4 m of Ilse or of her projected path, or within 2.5 s, shows his cone. Its near sector is solid and the touch circle is shown. The **step preview** (SR.8) shows what the next 0.6 s of input will do. **Cone-edge grace:** the outer 10° and outer 1 m fill at ×0.5, drawn dashed; the first 0.25 s of exposure doesn't count | WASD players steer by feel. Mimimi's hard cone edges work because units click to exact points; we can't, so the edge must be forgiving and the rule visible *before* she crosses it |
| **Traversal** | Push into a link 0.18 s (0.3 s drop); Space takes it at once; Climb, Ladder, Leap and Mist links | Keep, plus Dash (§11) and baseline **drop-feed** | Already good |
| **Camera-relative movement** | `CameraRelative(v, Cam.Yaw)` every frame | **Lock the frame while input is held.** If the camera rotates mid-move, keep the old yaw until the input direction changes or is released | Otherwise rotating with Z/X while holding W swings her path into a cone |
| **Animation restrictions** | Procedural bob and lean, no skeletal animation (KNOWN_ISSUES) | Gameplay timings must not wait on animation: lunge 0.25 s, grab instant, drop-feed on landing | When rigged animation arrives, it must fit these windows, not stretch them |

**Feeding and movement.**
- Today any key cancels the feed channel [Code VampireMovement.cs:78], so a feed is 1.6 or 3.2 s of standing still wherever the victim was.
- **Proposed:** moving during a feed **drags** the victim at 1.4 m/s and pauses the feed timer. See §28.

---

## 11. Supernatural movement

**Decision: supernatural traversal *is* a pillar, but as the vertical predator's road, not a teleport network.**
- Roofs, ledges and drops are where the vampire lives, and the Vigil's "looks up" hunters are the counter.
- Teleport-anywhere is the single most-punished design in the genre's reviews [Research: Dishonored Blink, Aragami, Ereban].

| Mechanic | Decision | Notes |
|---|---|---|
| **Short shadow dash** | **ADD, baseline. It replaces Shadowstep** | See below |
| Shadow Step (current: 12 m LOS teleport) | **REPLACE** with Shadow Dash | Cursor-aimed while the keys steer; bypasses walls and fences in line of sight; topology-deleting by design |
| Wall climbing | **KEEP** (marked climb points from M01; Wallcrawler at A4 for any brick or stone) | Already in code and on-theme |
| Ceiling movement | **NO** | At 52° pitch, interiors are cut-away boxes. A player can't read a ceiling path, and it would need a second navmesh |
| Mantling | **KEEP as is**: push-to-traverse with the verb shown on the HUD | Already right |
| Rapid ledge climbing | **Fold into Dash**: dashing into a climbable wall in darkness carries her up one storey (up to 4 m) | One button, chosen by direction. See "Rise" below |
| Mist | **KEEP and refocus it as the escape and infiltration form** (§24) | Under doors, through vents and bars; breaks contact |
| Bat movement | **NO** | Deletes verticality as a puzzle, needs flight nav, and flies over every Vampire Law. High cost, negative value |
| Supernatural leap | **KEEP** (A3 gaps; the Bound range moves to baseline at A5) | Roof-to-roof routes are the vampire's road |
| Window entry | **KEEP**, tied to the Threshold law: windows into non-home buildings are climb links; homes need an invitation | Makes thresholds a route decision rather than a wall |
| Fast descent | **KEEP**: drops of any height are silent [Code] | — |
| **Vertical ambush (drop-feed)** | **Move from A7 to baseline A2**. Dropping onto an unaware human within 1.5 m of the landing point starts a grab | The signature predator move should arrive in Act I, not Act III |
| Teleport between dark spaces | **NO** (see Shadowstep) | — |

### Shadow Dash (new baseline)
**Function**
- A 0.2 s burst of **6 m in the current move direction** (facing, if standing). It moves through the navmesh (`Agent.Move` in steps), so it **can't cross water, walls, fences or thresholds**.
- Silent.
- She is **unseen during the dash** in darkness. In light she is seen as a blur: detection +0.3 on any observer.

**Charges**
- 2 at the start (3 with a Shade upgrade).
- Each recharges in **5 s, only while her light is below 0.35**. No blood cost.
- **Light is the cooldown.** This keeps Aragami 1's light tension, the thing players punished Aragami 2 for removing [Research].

**Key:** Q (rebindable); gamepad B / Circle.

**Contextual Rise**
- Dashing into a climbable wall (a climb point, or any brick or stone at A4+) while in darkness carries her up to the ledge above, if the ledge is within 4 m.
- This is "rapid ledge climbing" without a new button.

**Why this, not a teleport**
1. **Direction comes from WASD**, so there is no aim.
2. **Topology holds by construction**, because a navmesh-bound burst can't skip a wall.
3. **It's an escape and a gap-closer**, both of which WASD wants.
4. **It scales by charges and darkness**, not by range.

**Shade upgrades**
- **Umbral Step:** +1 charge; she is unseen for 1 s after a dash ends in darkness.
- **Between Bars:** dashing into bars, fences or a window passes through.
- **Pounce** (Predator) uses the dash: see §24.

---

## 12. Camera and WASD

**Current camera** [Code TacticalCamera.cs]:
- yaw 45, pitch 52, distance 24 m (9–46 m);
- Z/X rotate at 110°/s;
- look-ahead peek up to 22 m on the arrow keys or MMB;
- follows the controlled character (D118).

**It was designed for click-to-move and adapted.** That shows in four places:

1. **No occlusion handling.** Grep finds no cutaway or fade system, only an x-ray overlay material. A click player could aim at what they can see. A WASD player walks *into* what they can't see: behind a building, inside a room under a roof.
2. **The look-ahead is manual** (arrows or MMB). With hands on WASD, the right hand is on the mouse, not the arrows. The camera should lead on its own.
3. **Rotation re-maps WASD in the middle of a movement**, because `CameraRelative` reads the yaw every frame.
4. **Off-screen enemies are invisible.** At 24 m distance and 52° pitch the visible area is about 40 × 25 m. A cone entering from off-screen is the classic WASD death.

**Changes:**

| Topic | Proposal | Size |
|---|---|---|
| **Occlusion** | **Cutaway cylinder** from the camera to Ilse (radius 2.5 m): walls and roofs inside it fade to 15% with an outline. **Interior mode:** when Ilse is under a roof, that building's roof and its upper floors above her are hidden. Any human within 10 m of Ilse who is occluded is drawn as a silhouette with his cone | Large (needs building and roof tagging at map build; the map text format already knows interiors) |
| **Tracking** | **Velocity lead:** the target point is Ilse + velocity × 0.6 s (about 2 m at walk, 4 m at run), eased at 0.4 s. No lead while sneaking near enemies | Small |
| **Look ahead** | Keep the manual peek (arrows / MMB / right stick) for scouting; it eases back when she moves (D118) | — |
| **Rotation** | **Tap Z/X = 45° snap in 0.2 s; hold = smooth.** The WASD frame is locked during rotation (§10). Optional "auto-align": while running, the camera slowly turns toward the direction of travel (off by default) | Small |
| **Zoom** | Default 20 m (from 24). Three presets on the mouse wheel's detents: 14 / 20 / 30 m; free zoom in settings | Small |
| **Pitch** | 52° default. **+6° when she's above 3 m height** (on a roof) so the street below reads | Small |
| **Enemy visibility** | **Off-screen threat markers:** a pip at the screen edge for any human within 25 m who is Suspicious or worse, or whose cone will cross her projected path within 2.5 s. Coloured by state, with a small cone wedge showing his facing and his meter on Ilse filling inside the pip (SR.9, SR.11) | Medium |
| **Vertical spaces** | Height shading: the walkable surface Ilse is on is drawn at full value, and other levels are dimmed 20% | Medium |
| **Ability targeting** | The aim direction comes from the mouse cursor relative to Ilse, or the right stick, independent of the camera. Range rings are drawn on the ground at her height | Small |
| **Enemy vision information** | **Contextual cones in normal play** (SR.6): no Alt needed to survive. Hover a guard to focus him; **middle-click pins** a cone and route (up to 3). Alt remains the Tactical view of everything (SR.12). The Hunt outline and LKP ghost (§20). All of it is ground ink, so it **must survive the cutaway**: ink draws above the faded walls and roofs | Small–Medium |

**Does it need substantial redesign?**
- Not of the rig: the high three-quarter view is the right one for reading cones and lamps.
- **It needs the occlusion and lead layers that direct control assumes.** Occlusion is the one large item, and it sits on the critical path for every interior mission (M05, M06, M11, M12).

---

## 13. Ability control

**Current** [Code VampireAbilities.cs]:
- Pressing an ability key enters aim mode.
- The target comes from the mouse hover (`HoverNpc`, `HoverLight` or `HoverPoint`) and is confirmed by left click.
- Beckon and False Orders need two clicks (target, then point).
- If the target is out of range, she **walks into range on her own** (D114).
- There are 6 slots.

**What goes wrong under WASD:**
- The right hand moves to precise cursor work while the left hand is steering a body near a cone.
- During two-stage aims she stands still or walks on her own.
- Out-of-range casts turn the character into a click-to-move unit.

**Decision: quick-cast with a soft target, hold to preview, no auto-walk.**

| Approach | Verdict | Why |
|---|---|---|
| Mouse aiming | **Keep, as the aim direction**, not as a pixel target | The cursor's direction from Ilse chooses the soft target. No pixel hunting |
| Directional abilities | **Yes** for Dash and Pounce (move direction) | Fits the keys |
| **Quick-cast** | **Yes, the default.** Tap = cast on the highlighted soft target | Abilities never stop movement |
| Target lock | **No** | A combat-game crutch; soft target plus pinned cones cover it |
| **Soft target** | **Yes.** The valid target nearest the aim ray within a 35° half-angle and inside range is outlined continuously while an ability is selected. The aim ray comes from the cursor if the mouse moved in the last 1.5 s, otherwise from the move direction; gamepad uses the right stick, then the left | Smooth, predictable, pad-friendly |
| Ability wheel | **No** | With 4 slots, a wheel only adds time |
| **Hotkeys** | **Yes. 4 active slots (1–4)**, plus Dash (Q), Feed (F/R), Blood Sense (V hold), Mist (toggle on its slot) | Four keys sit next to WASD; six don't. 4 also forces build choices (§25) |
| **Hold-to-preview** | **Yes.** Hold the key to show range (a dotted crimson ring, SR.11), target, outcome ("Frozen 10 s", "Walks to you", "Lamp dark 60 s": Smother previews the shrunken light rim), cost and noise ring; release to cast; RMB cancels. She keeps moving at full speed and faces the aim | Mark of the Ninja's "predict before acting" without leaving real time [Research] |
| Slow-time targeting | **Accessibility option only** (on by default on Merciful): holding an ability key runs the world at 50% | A default slow-aim makes Hunter trivial; see §14 |
| Tactical pause | **Keep P as inspect-only** (D115) | Orders in pause were tried and cut |
| **Context targeting** | **Yes:** Smother targets lights, Mesmerize humans, Beckon humans; Gloom and Snare target ground (reticle at the cursor, clamped to max range, never walking) | One rule per ability, shown in the preview |

**Point abilities** (Gloom, Snare, Beckon's optional destination):
- While held, the reticle sits at the cursor clamped to range, or along the right stick at a distance set by stick tilt.
- On release, it casts there.

**Beckon becomes one-stage.**
- Tap: the target walks to **where Ilse stood when she cast** ("follow the voice"). This is the WASD-native lure: call them to you, then move away.
- Hold, then release at a point: the target walks to that point (the old behaviour).

**Out of range** means the target is shown in grey with the distance. **She never walks on her own.**

**Casting doesn't interrupt movement.** All casts are instant except:
- Enthrall (1.0 s channel; moving cancels it);
- Snare placement (0.4 s; moving cancels it).

**Thrall commands** move to the same scheme: soft target from the thrall, tap to give the command.

---

## 14. Tactical pause, slow motion, planning, queuing and marking

Each candidate tested against a real problem:

| Candidate | Real problem it might solve | Verdict |
|---|---|---|
| **Planning mode (Shadow Mode style)** | Coordinated kills with thralls | **NO.** It was built (the Nightplan) and **removed after play-test** because it "was not understood and did not feel like it worked", and it doubled every control path (D115). A second input mode fights direct control |
| **Tactical pause with orders** | Picking a target under pressure | **NO.** That is the Nightplan again. Soft-target quick-cast solves picking under pressure |
| **Inspect pause (P)** | Reading cones and routes | **KEEP** as is (D115) |
| **Reaction Window (detection slow-mo)** | **"Spotted = reload."** The moment of detection is the moment the player most needs a decision and has the least time | **YES.** See below |
| **Aim slow-mo** | Pad and motor accessibility | **Option only**, default on for Merciful |
| **Ability queuing** | — | **NO.** Queued actions make the character act without the player's hands, the R3 problem again |
| **Target marking (Signal)** | Coordinated thrall strikes today need rapid portrait-switching (D117) | **YES, narrowly.** See Thrall Signal below |
| **Pinned cones** | Remembering 3 patrols while steering | **YES.** Middle-click pins a guard's cone and route (max 3). Pins are the Focus tier of the readability hierarchy (SR.12); contextual cones cover the moment-to-moment need |

### Reaction Window
**Trigger**
- A human's detection meter reaches 1.0 on Ilse.
- He is the first in his group to do so in 20 s.

**What happens**
1. The world drops to **30% speed for 0.5 s of real time**, then eases back to 100% over 0.3 s. Ilse moves at full speed throughout.
2. A low heartbeat sting plays.
3. The guard begins a **1.0 s game-time shout wind-up**: a red ring grows from his head to his shout radius, and a bark ("There! By the—").
4. **The Spotted Explanation** (SR.10, Revision 2) appears for the same 0.5 s:
   - the spotter is outlined;
   - a sight-line runs from his eye;
   - the boundary she crossed flashes (near arc, light rim plus the lamp, touch circle, searchlight);
   - one caption says why, for example "Near band: 4.1 m (his limit is 6 m)".

   The player knows *what* to fix while they have the second to fix it.

**What the player can do in the wind-up:** anything that silences him finishes his alarm before it starts:
- Pounce or grab him;
- Mesmerize him;
- Rend him (loud, but a kill is not a shout);
- break line of sight *and* get out of his near band (he becomes Investigating at your last position, not Hunting).

**Limits**
- Once per group per 20 s. The second spotter in a group shouts at once.
- Vigil hunters have a 0.4 s wind-up.
- Apex difficulty: no slow-mo, wind-up only. The explanation caption stays on every difficulty.

**Why this solves a real problem**
- Shadow Tactics uses an audio cue plus slow motion at detection so you can see "exactly when and why you were spotted" [Research: Mimimi detection deep dive].
- Here it does more: it turns the reload moment into a **skill moment**, and only direct control can deliver that.

**Implementation:** `Game.SlowMo` already exists (used at death [Code MissionController.cs:506]). The wind-up is a new state between Alerted and Shout.

### Thrall Signal (replaces the planning-mode use case)
**How it works**
- With a thrall controlled, *hold* a command key (Distract, False Orders, Puppet Strike) on a target. Instead of executing, the thrall **readies**: a violet line to the target, and the thrall waits in position.
- Control returns to Ilse. Readied thralls fire when Ilse **starts a grab, a Pounce or a Rend**, or when she presses **T + Space** ("Signal").

**Limits**
- One readied order per thrall.
- A readied thrall seen by an officer is exposed as usual.

**Why:** it gives the M11 crescendo-style synchronised kill in real time, with no planning mode and no second input path.

---

# STEALTH READABILITY (§SR, Revision 2)

**Added in Revision 2.** This section defines **one visual language** for every piece of stealth information. Other sections refer to it as §SR.n instead of designing their own circles.

**The standard:**

> **At walking speed, without pausing, the player can tell whether the step they are about to take will expose them, and to whom.** If the game judges Ilse by a rule, that rule is drawn on the ground before she crosses it, and it is drawn from the same numbers the AI uses.

## SR.1 What the build shows today, and why "that looked dark enough" happens

Everything below was checked in code [Code]. Fill times come from `DetectionMath.Rate` for a relaxed watchman on Hunter (near 6 m, far 15 m, `NearRate` 1.6, `FarRate` 0.9).

| # | Gap | Evidence | Consequence |
|---|---|---|---|
| 1 | **Rendered light is about 1.5× bigger than gameplay light.** A gas lamp exposes Ilse within about **5.5 m** of its base. Its Unity spot lights the ground out to about **8.3 m**, so **about 56% of the visible pool is actually safe**. Nothing marks where the safe part begins | The `LightAt` threshold of 0.35 versus the `GameLight` spot setup: angle `atan2(R,h)·2 + 10°`, range `√(R²+h²)·1.15`. See the table in SR.4 | Players either fear the whole pool or learn the real edge by dying. Neither is a decision |
| 2 | **Rendered light passes through walls that gameplay light respects.** This applies to every lamp outside the real-time shadow budget | `BudgetShadows` gives shadows only to the lamps nearest the camera. `LightAt` always Linecasts against `LightBlockMask` | An alley behind a wall can *look* lit. The look changes as the camera moves and the budget shifts |
| 3 | **Exposure comes from the brightest single source, not a sum.** Two overlapping pools are no brighter than one | `LightAt` takes `max` | Players read an overlap as doubly dangerous |
| 4 | **Cones are hidden by default.** They are drawn only while Alt is held, or for inspected guards | `ConeRenderer` | A WASD player can't hold Alt and steer, so most play happens without cones |
| 5 | **The far band's light tint samples only 2 points per ray** (near + 0.3 m and reach − 0.2 m) | `ConeRenderer` far-band tint | A lit patch in the middle of a cone is drawn dark, and the reverse |
| 6 | **The near band is a sector, not a ring.** `Classify` checks the angle before the near range. Only the 1.4 m peripheral zone is all-round. **The audit's and the first redesign's "near-band ring" was the wrong shape** | `DetectionMath.Classify`, lines 40–45 | A ring would lie: it would mark the ground behind a guard as dangerous and teach the wrong rule |
| 7 | **The 1.4 m peripheral "touch" zone is never drawn** | — | Brushing past a guard's shoulder in the dark spots her in **0.25 s**, with no visible cause |
| 8 | **The two bands behave completely differently but look alike.** | **Near band in darkness:** fills in **0.76 s** at its edge and **0.44 s** at 3 m.<br>**Lit far band:** about **3.8 s** at threshold light (0.36, 7 m); about **1.6 s** at light 0.8 and 8 m.<br>Wary ×1.25; Alerted or Searching ×1.6 | The near band is a **wall** you must see *before* you enter it. The far band is a **slope** you can read *while* you are on it. Nothing teaches this |
| 9 | **Guard state changes the cone's shape, but the drawing only changes its tint** | **Wary:** near ×1.15.<br>**Alerted or Searching:** near ×1.2 and +10° half-angle.<br>**Bell-ringing:** +35° | His near band can grow by a metre toward her, and nothing shows it |
| 10 | **Ilse's light read is a 3-state HUD eye** (DARK below 0.175, SHADOW below 0.35, LIT). The game has **one** darkness threshold: `Vampire.InDark` (regen) and the far band's `LitThreshold` both use 0.35 | `UIHud`, `Vampire.InDark` | "SHADOW" is a tier that changes nothing. The eye sits in a corner, not where a WASD player looks |
| 11 | **Detection meters are drawn only for guards on screen** | `UIHud.TickMarkers` | A meter filling off-screen is invisible until the shout |
| 12 | **Nothing explains a detection.** At the moment of Spotted, `Perceive` knows the band, distance, light, light source and modifiers. It shows none of them | `Npc.Perceive` | The only possible reaction is "How did he see me?" |
| 13 | **Height rules are invisible.** The near band uses flat distance and ignores height. The far band ignores targets more than 2.5 m above, unless the guard `LooksUp` | `Classify` | Roof safety is guesswork |

**Root cause (added to §6.2 as R6): the game shows a different world from the one it simulates.**
- It renders light by Unity's rules and judges by its own.
- It judges by cones it hides.
- It never explains a detection.

Every fix below draws the picture from the same numbers the AI uses.

## SR.2 The one rule the player learns

> **If a guard's colour is on the ground under you, he can see you there.**
> **The more solid the colour, the faster he sees you.**

The rest of this section exists to make that sentence literally true.

| Reading | Rule in code | What is drawn |
|---|---|---|
| **Near band:** he sees you whatever the darkness | `Band.Near`: inside his angle, within `NearRange` | The cone's **inner sector, filled solid** in his state colour |
| **Touch zone:** right beside him, any direction | `Band.Peripheral`: within 1.4 m, \|dh\| < 1.5 | A small **solid circle** at his feet, shown when Ilse is within 4 m |
| **Far band, lit ground:** he sees you because you are lit | `Band.Far`: within `FarRange` and light ≥ 0.35 | The outer sector, **filled only where the ground is exposed**. His colour appears exactly where his gaze and the light overlap |
| **Far band, dark ground:** he is looking but can't see you | `Band.None` | **Outline plus faint hatching**: "he's looking this way, but you're in the dark" |
| **Grace fringe** (§10): the outer 10° and outer 1 m fill at ×0.5 | New | The fringe is **dashed** and half-strength. While she stands in it, her watcher tick (SR.7) is dashed too |
| **Light coverage:** this area exposes you | `LightAt ≥ 0.35`, with occlusion | The light's own pool, **re-rendered to end at the threshold**, plus a thin rim (SR.4) |
| **Line of sight** | Linecast to feet + 1.0 m and feet + 1.6 m | Cones stay clipped by walls (the existing 36 raycasts) at the same heights |

So the three readings the player needs read together, at a glance:
- a **solid** sector means seen in any light;
- a **filled** patch where cone meets lit ground means seen because lit;
- an **outlined** cone over dark ground means looked at, not seen.

There is no mental arithmetic. **The game draws "cone AND light" for the player.**

## SR.3 The Exposure Field: one source of truth

Every light read, every cone fill, the step preview and the detection explanation come from **one structure**, computed with the gameplay calculation itself.

**What it is.** A 0.5 m ground grid per walkable level. Each cell stores `LightAt(cell)` and the index of its brightest source.

- **Static lights are baked once at map load.** The bake uses everything `LightSystem.LightAt` uses:
  - the falloff `Intensity · (1 − (d/R)²)`;
  - the 1.0 m sample height;
  - the `LightBlockMask` Linecast;
  - the max-of-sources rule.
- **Events re-bake only the cells a light reaches:** Smother, relighting, gas valves, lamp groups, and doors that block light.
- **Moving lights** (lanterns, flares, searchlights, carried candles) are evaluated analytically every frame within their small radius, then max-combined on top.
- **Global modifiers apply when the field is read:**
  - `GlobalScale`: Eclipse drops it to 0.25, and every pool visibly shrinks;
  - the Gloom `DarkZone` (`Ambient × 0.5`);
  - foliage (×0.25).

**Cost.**
- A 120 × 120 m level is 57,600 cells.
- A lamp touches at most about 1,000 cells, so a full bake is about 40,000 Linecasts, once.
- A Smother re-bakes about 1,000.
- Reads are array lookups.

**Who reads it:**

| Consumer | Use |
|---|---|
| Light rims and pools (SR.4) | Where exposure ends |
| Cones (SR.5) | The far-band fill, clipped to lit cells |
| Ilse's ground disc (SR.7) | Her exposure state. It equals `Vampire.Light` by construction (unit test) |
| Step preview (SR.8) | "Will my next step expose me?" |
| Spotted Explanation (SR.10) | Which light lit her, and how much |
| Level lint (§30) | Dark-route widths; safe gaps between lights |
| Bot harness | Asserts every step that what is drawn equals what is judged |

**The test that keeps it honest.**
- For 10,000 random points per map, the field's value equals `LightAt` within 0.02.
- At every vertex, the cone fill agrees with `Classify` plus line of sight.
- Any mismatch fails the build.

## SR.4 Light coverage: making the pool tell the truth

The options investigated, judged against "matches the actual gameplay light calculation, including geometry blocking":

| Option | Verdict | Why |
|---|---|---|
| **Giant circles around every light** | **No** | They clutter every street, lie where walls clip the light, and become unreadable when six of them overlap |
| **Ground projection of the exposure contour** (a rim at 0.35, clipped by walls) | **Yes, as the boundary** | It *is* the rule. `GameLight.BuildBurnRing` already draws a wall-clipped floor ring for burning lights; the same code draws this rim |
| **Stronger falloff with a visible knee** (re-authored Unity lights) | **Yes, as the base** | The best boundary is the light itself ending where exposure ends. Most of the time it needs no UI |
| **Contextual radius** | **Yes** | Rims appear within about 10 m of Ilse or her projected path. Elsewhere, only the re-authored pools show |
| **Subtle threshold boundary** | **Yes** (this is the rim) | A thin beaded line in the light's own hue, not a UI colour |
| **Inspect overlay** | **Yes, in Tactical (Alt)** | All rims, plus a 0.7 "blazing" line, inside which far-band detection is about twice as fast |
| **Clarity that increases on approach** | **Yes** | The rim's opacity rises from 0 at 10 m to full at 2 m, and it brightens once when Ilse crosses it |

**Decision: make the render obey the gameplay model, then add a rim where precision matters.**

1. **Re-author every `GameLight`'s Unity light from its exposure contour**, not from `Radius`:
   - the visible ground radius is about the 0.35 contour plus a 0.3 m feather, instead of about 1.5× it;
   - a custom attenuation ramp with a **visible knee** at the threshold: flat and bright inside, a short fall-off, then near-ambient;
   - lights outside the real-time shadow budget get an **occlusion cookie baked from the Exposure Field**, so they never spill through walls.
2. **The exposure rim** is clipped by walls (BuildBurnRing's method, 56 segments) and drawn in the light's hue:
   - warm for lamps;
   - cool for moonbeams;
   - a doubled pale gold for lights that burn.
   - The existing burn ring at `R × 0.85` becomes the inner line of the same family.
3. **Moving lights always show their rim within 12 m of Ilse** (lanterns, flares, searchlights): their boundary moves, and memory can't track it.
   - A lantern carrier's own light (3.7 m) sits inside his own near band (6 m). His lantern matters to *other* guards' eyes, and the rim shows where it does.
4. **Overlapping pools draw one merged contour**, to match the max-not-sum rule.

**Gameplay radii versus rendered radii today** (ambient 0.06, `GlobalScale` 1, flat distance from the light's base) [Code, computed]:

| Light | `Radius` | Exposes you within | "Blazing" (≥ 0.7) within | Rendered light reaches about |
|---|---|---|---|---|
| Gas lamp | 7 | **5.5 m** | 3.6 m | 8.3 m |
| Wall lamp | 6 | **4.8 m** | 3.2 m | 7.1 m |
| Brazier | 6.5 | **5.6 m** | 4.2 m | 8.0 m |
| Hand lantern | 4.5 | **3.7 m** | 2.4 m | 5.5 m |
| Candle | 3.5 | **2.8 m** | 1.6 m | 4.2 m |
| Chandelier | 9 | **6.7 m** | 4.1 m | 10.7 m |
| Window | 4 | **2.8 m** | never | 4.8 m |
| Fire | 8 | **7.0 m** | 5.5 m | 9.9 m |
| Sunstone (also burns) | 8 | **6.6 m** | 5.2 m | 9.4 m |
| Holy light (also burns) | 5 | **4.2 m** | 3.0 m | 6.1 m |
| Searchlight (its pool rule is separate: flat distance < `R × 0.72`) | 4.6 | **3.9 m** | 3.2 m | 5.2 m |

**Searchlights** detect through their own pool rule (rate 1.6), so their pool keeps a hard edge in the same rim style. A dotted arc shows where the pool will sweep in the next second.

**Why not just a better light meter?** A meter answers "am I lit *now*?". WASD needs "*where* does it stop?", and needs it before the step. The meter stays, on the disc (SR.7), as confirmation.

## SR.5 Cones: always there when they matter

**Geometry.** Keep the current mesh: 36 rays, clipped by walls, rebuilt at 20 Hz. Change what is drawn on it:

| Part | Drawing |
|---|---|
| **Origin** | A short arc at the guard's feet in his state colour. It says whose cone this is |
| **Near sector** (0 to `NearRange`) | Solid fill. Its opacity rises toward the guard, because the fill rate rises as she closes in |
| **Near edge** | A solid arc: *the* line not to cross |
| **Far sector** (`NearRange` to `FarRange`) | An outline plus faint hatching over dark ground. **A solid fill only on cells the Exposure Field marks lit**, sampled every 0.5 m (not 2 points per ray) |
| **End** | A thin arc at `FarRange`, clipped by walls |
| **Grace fringe** | The outer 10° and the outer 1 m, dashed, at half strength |
| **Touch zone** | A 1.4 m solid circle, shown when Ilse is within 4 m |
| **Height** | The near sector projects onto any surface he has line of sight to, because the rule ignores height. The far sector stays on his own level, except for guards who `LooksUp` (hunters, sentries), whose cones also draw on roofs |

**State changes.** The geometry comes from the live `Npc.Vision`, so when he turns Wary or Alerted the cone visibly widens and the near edge visibly steps outward.

| State | Colour (existing palette) | Line | Extra |
|---|---|---|---|
| Unaware | Bone `#d8dce6` | Solid edges | — |
| Wary | Bone, warmed 35% toward amber | Solid | The near edge sits 15% further out; a small ◦ at his head |
| Suspicious | Amber `#f2b233` | Solid, with a slow pulse | "?" at his head; the cone swings toward the cause |
| Investigating | Amber | Solid | A dotted amber path to the point he is walking to |
| Spotted (shout wind-up) | Red `#e8324a` | Solid | The shout ring grows (§14) |
| Hunt / Searching | Red | Solid. The cone is 10° wider and the near edge 20% further out | The Hunt outline (SR.11) |
| Lost / sweeping | Red at 60% | **Dashed** | A dotted path to his next search point |
| Mesmerised / Dominion | Violet `#9a6bff` | Outline only | He can't detect; drawn for clarity |
| Blinded | Grey | Dashed | Both ranges drawn at ×0.8 |

**Ownership when cones overlap:**
- The cone of the guard whose meter is rising on Ilse draws on top at full strength; the others drop to 50%.
- Hovering the cursor over the ground outlines every guard whose cone covers that point.

**The near band, integrated** (this replaces the "near-band ring" of P10):
- The near sector is part of every cone, and it is the last part to disappear.
- If the contextual rules (SR.6) hide a cone, but Ilse is within `NearRange` + 3 m of an unaware guard, **his near sector and touch circle alone** fade in.
- The near edge is **always solid, never dashed**. It is the one boundary that leaves no time to react (0.4–0.8 s).
- **"How close can I get in darkness?"** To the solid arc. The grace fringe lets her dip about 1 m inside for about half a second.

## SR.6 When cones appear: the contextual display (no holding Alt)

A guard's cone shows during normal play when **any** of these is true:

1. **Near danger.** Ilse, or the point 1.5 s ahead along her current input, is either:
   - within **4 m** of his *detecting* region (the near sector, or the lit part of his far sector); or
   - within 2 m of the cone's outline.
2. **Time to contact.** His cone will reach her within **2.5 s** at current velocities. This includes where he will be facing in 1 s, taken from his patrol and turn data.
3. **He is aware of her.** His meter on her is above 0, or he is Suspicious or worse anywhere within 25 m.
4. **The player is focusing on him.** He is pinned or hovered (SR.12).

**Rules:**
- **Hysteresis.** A cone fades in over 0.25 s and stays at least 1.5 s after the conditions end, so nothing flickers at walking speed.
- **Budget.** At most **4 contextual cones**, ranked by time to contact.
  - Aware guards always count toward the 4.
  - Unaware guards beyond the 4 drop to their near sector only.
- **Off-screen.** A qualifying guard who is off-screen gets an edge marker with a small cone wedge showing which way he faces (SR.11).
- **Not shown in normal play:**
  - patrol routes;
  - cones that can't reach her soon;
  - dark-far hatching more than 8 m from her.

## SR.7 Ilse's own state: the ground disc

WASD players watch Ilse, not the corner of the screen. **The stealth HUD moves under her feet.**

| Element | Meaning |
|---|---|
| **Disc fill** | **Hidden** (a hollow ring) when her light is below 0.35. **Exposed** (filled warm) at 0.35 or above. This is the one threshold the far band, regen and Dash all use. The HUD eye's DARK/SHADOW split goes |
| **Watcher ticks** | One tick on the rim for each human who has her in a band with line of sight. It points at him, in his state colour, and its length is his meter. It is dashed while she is in his grace fringe |
| **Near warning** | The rim takes a guard's colour when she is within 1 m of his near edge |
| **Toe** (SR.8) | A short wedge at the front of the disc: the result of her next step |
| **Masked / Mist / Gloom** | The disc turns the power's colour (violet / grey / Shade blue), meaning "this overrides light" |
| **Burning** | A double pale-gold rim (holy light, sunstone, sunbeam), plus the burn tick |
| **Warm blood** (§29) | A faint crimson pulse |

The corner eye stays as a redundant read (for accessibility and screenshots), with two states plus "MASKED".

## SR.8 "Will my next step expose me?"

The **toe** looks **0.6 s ahead along the input direction**. It uses input, not velocity, so it works from a standstill the moment a key is pressed.

| Toe | Meaning |
|---|---|
| None | Nothing changes |
| Warm | You will step into exposed light, but no cone covers it |
| A guard's colour, solid | You will enter ground where *that* guard can see you |
| A guard's colour, flashing | You will enter a near sector or a touch zone: no time to react |

On a pad, the controller rumbles once on the same events. An optional "breath" audio cue serves players who don't look down.

It is evaluated against `Classify` for every contextual guard and against the Exposure Field: a few lookups per frame.

## SR.9 Detection build-up

- **The meter lives in two places:**
  - over his head (as now);
  - as his watcher tick on her disc, which is what a moving player actually sees.
- **The meter's fill shows the band:**
  - solid for near or touch;
  - striped for far and lit;
  - waves for heard;
  - dots for smelled.

  The player learns *why* while it fills.
- **The boundary that is feeding it glints.** When a meter starts (at 0.05, where the "huh" cue already plays), the edge she is past brightens for 0.3 s: the near arc, the light rim or the touch circle.
- **Off-screen meters** fill inside the edge marker. This fixes "meters only on screen".
- **Decay is visible.** After the 1.2 s hold, the tick shrinks at the decay rate: "he's losing interest".

## SR.10 The Spotted Explanation

When a meter reaches 1.0, the Reaction Window's 0.5 s of real time (§14) freezes the *picture*, not the play.

1. **Who:** the spotter is outlined and the others are dimmed.
2. **From where:** a sight-line from his eye to Ilse.
3. **Which cone:** his cone is drawn at full strength, whatever the contextual rules said.
4. **What she crossed:** the dominant band from `Classify`, drawn as one of:
   - the near arc;
   - the light rim, with the lamp that lit her outlined;
   - the touch circle;
   - the searchlight pool;
   - the noise ripple that turned him.
5. **A one-line caption at the guard.** It is built from data `Perceive` already holds. Examples:
   - "Near band: 4.1 m (his limit is 6 m)."
   - "Lit by the gas lamp: light 0.52 (exposed above 0.35). Running doubled it."
   - "Touch range: 1.2 m."
   - "Heard you: running on cobbles, 8 m."
   - "Searchlight."

   Modifiers appear only when they decided it (running ×2, carrying ×1.2, Wary ×1.25).

**After the mission,** the debrief gets a **Detections** page:
- a top-down snapshot for each Spotted event: position, cone, light rim and caption;
- what started each Hunt, for example "Shout by watchman Brandt, heard by 4 within 15 m".

**On Apex** the caption stays: knowledge is not a difficulty setting. Only the slow-mo goes.

The target reaction: **"Yep, I pushed too far."** The boundary she pushed past is on the screen when she says it.

## SR.11 The full vocabulary

**Four primitives cover everything:**

| Primitive | Means | Style |
|---|---|---|
| **Fill** | "This rule applies here" | Translucent ground ink |
| **Edge** | "The rule starts here" | **Solid:** a hard rule, now.<br>**Dashed:** a soft rule (grace, decay, memory).<br>**Dotted:** a prediction or plan (paths, previews) |
| **Ripple** | "Something was heard this far away" | A ring that expands once to its radius and fades |
| **Mark** | A person, a point, a place | Glyphs, pips, ghosts |

**Colour says *whose* it is:**

| Colour | Belongs to |
|---|---|
| The light's own hue (warm, moon-blue, holy gold) | The world's light. **Light has no UI colour**: it is the pool itself plus its rim |
| Bone | Unaware humans |
| Amber | Suspicion |
| Red | A human who knows, or who is hunting |
| Violet | Dominion and thralls |
| Shade blue | Darkness Ilse made (Smother, Gloom, Eclipse) |
| Crimson | Ilse herself: her powers' ranges, previews and her noise |
| Teal | Interactables (the existing `Interact` colour) |

**Every stealth element in that one grammar** (the columns are the tiers in SR.12):

| Element | Primitive and colour | Normal | Focus | Tactical (Alt) |
|---|---|---|---|---|
| Unaware cone | Fill and edge, bone | Contextual (SR.6) | Pinned or hovered | All |
| Suspicious or investigating cone | Fill and edge, amber; dotted path | Always, within 25 m | ✓ | ✓ |
| Hunt or search cone | Fill and edge, red; dashed when Lost | Always, within 25 m | ✓ | ✓ |
| Near sector and touch circle | Solid fill, the owner's colour | Within `NearRange` + 3 m | ✓ | ✓ |
| Light coverage | Re-authored pool plus rim | Pools always; rims within 10 m | Hovered light: rim plus "Smother: dark 60 s" | All rims plus the 0.7 line |
| Ilse's illumination | Disc | Always | — | — |
| Ilse's noise | Ripple, crimson | Every noisy step (exists, D119) | — | — |
| Guard shout | Ripple, red, growing through the wind-up | Always | — | — |
| Other noises (bells, doors, falling bodies) | Ripple, white | Within 20 m | — | All |
| Ability range and target | Dotted crimson ring plus target outline | While the key is held | — | — |
| Hunt boundary | Dashed red outline that shrinks | During a Hunt | — | — |
| Last-known position | A red dotted ghost of Ilse | During a Hunt | — | — |
| Search points | Small red pins | When a sweeper heads to one within 15 m | — | All |
| Patrol routes | A dotted bone line with turn ticks | — | Pinned guards | All within 30 m |
| Off-screen threats | An edge pip in the state colour, with cone wedge and meter fill | Aware within 25 m, or a cone reaching her within 2.5 s | Pinned | All within 40 m |
| Burn zones | Double pale-gold rim | Within 10 m | ✓ | ✓ |
| Darkness Ilse made | Shade-blue edge | While active | — | ✓ |

## SR.12 The information hierarchy

| Tier | How | Shows | Never shows |
|---|---|---|---|
| **1. Normal** (the default, while moving) | Nothing held | What can hurt Ilse in the next 2.5 s or so:<br>• contextual cones (at most 4);<br>• near sectors close to her;<br>• exposure rims within 10 m;<br>• the disc, with ticks and toe;<br>• aware guards;<br>• shout and noise ripples;<br>• the Hunt outline;<br>• off-screen threats | Routes, distant cones, numbers, and any text except the Spotted caption |
| **2. Focus** (hover, pin, inspect) | Hover a guard or a light to see its full read.<br>**Middle-click pins** it (up to 3 guards; lights can be pinned too) | • Pinned cones always, with routes and turn timing.<br>• A hovered light's rim and its Smother outcome.<br>• A hovered guard's archetype rules ("looks up", "smells", "carries a lantern") | Other guards' information |
| **3. Tactical** (hold **Alt**, or toggle it in options; the inspect pause **P** shows it too) | Held or toggled | Within range (20 m on Apex, as now):<br>• every cone, near sector and route;<br>• all rims, plus the 0.7 line;<br>• the noise radii of nearby surfaces (gravel, glass, water);<br>• search points;<br>• a tint of the Exposure Field | — |

**Rules:**
- **Normal must be enough to play a whole mission without Alt. Alt is for planning, not for survival.**
- **Accessibility:**
  - an "always show all cones" option;
  - Alt as hold or toggle;
  - the existing high-contrast mode doubles edge widths;
  - a colour-blind mode in which line style alone carries meaning (solid, dashed, dotted; hatch versus fill), so colour is never the only channel.
- **Difficulty changes ranges, never rules:**
  - **Merciful** widens the contextual distance to 6 m and widens the grace.
  - **Apex** limits Normal to near sectors, aware cones and rims within 6 m.

## SR.13 Risks

| Risk | Mitigation |
|---|---|
| **Clutter** ("it's a board game now") | The ink is on the ground only, low alpha and contextual. One rim style. At most 4 cones. No text in Normal |
| **Atmosphere lost to UI** | The light itself is fixed in the render, not covered by an overlay. Most of the time the pool's edge *is* the information |
| **Full information makes stealth easy** | The information is truthful but local: the player still reads patrols, finds routes and scouts. In Shadow Tactics and Mark of the Ninja, full information makes stealth fair, not easy [Genre precedent] |
| **Performance** | The field is baked once. Cone fills are lookups. Rims are 56-segment strips, as BuildBurnRing already draws |
| **The cost of re-rendering light** | Phase it: rims and cone clipping first; re-authored ranges next (numbers only); occlusion cookies last |

## SR.14 Build order

| Step | Content | Size |
|---|---|---|
| 1 | Exposure Field, plus the equality test against `LightAt` | Medium |
| 2 | Re-authored light ranges and knee (numbers in the `GameLight` Unity-light setup) | Small |
| 3 | Exposure rims (reusing BuildBurnRing's code) | Small |
| 4 | Cone redraw:<br>• near sector and touch circle;<br>• far fill clipped by the field;<br>• grace fringe;<br>• live state geometry | Medium |
| 5 | Contextual display and pins | Small–Medium |
| 6 | Ilse's disc, ticks and toe | Medium |
| 7 | The Spotted Explanation and the debrief page | Medium |
| 8 | Off-screen markers with meters (shared with §12) | Small |
| 9 | Occlusion cookies for unshadowed lamps | Medium |

---

## 15. Fixing the biggest problems first

These are ordered by player impact. They include the audit's Critical and Major issues (as corrected in §7) and five new problems that the WASD lens exposes (P12–P16; P15 and P16 were added in Revision 2).

### P1. Shouts and screams reach the whole map (I-01)
- **Current behaviour:**
  - `Shout` uses `15f * ShoutRadius` (225 m on Hunter), and `HearNoise` multiplies scream and voice radii by ShoutRadius (270 m).
  - One sighting wakes every NPC within a frame: 16/16 in M02 and 48/48 in M07 [Played].
- **Root cause:** a unit bug. ShoutRadius was already in metres. Nothing measured the actual radius (R1).
- **Desired experience:** a shout reaches its group (10/15/22 m by difficulty), and you can see how far.
- **Recommended change:**
  - `r = ShoutRadius` in `Shout`, and `× ShoutRadius / 15f` in `HearNoise`.
  - Draw the shout ring.
  - Add a play-mode regression test: an NPC at r+1 m doesn't hear.
- **Size:** Small.
- **Why it's worth it:** every downstream system starts working: local escalation, the Hunt, emergence, the Dossier and Terror.
- **WASD implications:** a direct-control player can outrun a 15 m shout. They can't outrun a 225 m one.
- **Risks:** missions tuned around global alarms become too easy. M07, M08 and M13 may rely on everyone converging.
- **Mitigation:** retune per mission with the Hunt (P3). Bells stay map-wide by design.

### P2. Dazed victims never wake and report every frame (I-02)
- **Current behaviour:**
  - `WakeUp(true)` runs every frame from the Dazed tick. `EnterPanic` and `EnterSearching` refuse `Incapacitated`, so the NPC stays Dazed.
  - `ReportWitness` fires about 140×/s: WitnessReports went 0 → 9,982 in ~70 s, and Dossier Sightings 10,782 → 20,764 [Played].
  - The alarm is pinned at 1.
- **Root cause:** a state-transition guard in the wrong order.
- **Desired experience:** a sipped victim wakes groggy after 45 s, reports once, and the area turns Wary.
- **Recommended change:**
  - Leave Dazed (set the state to a new `Groggy` or `Relaxed`) **before** routing to Panic/Search.
  - Report once.
  - Add a regression test.
  - Reset dev Dossiers.
- **Size:** Small.
- **Why it's worth it:** sip versus drain becomes a choice, the Dossier becomes honest, and alarms decay again.
- **WASD implications:** none directly. Grab-and-drag (§28) depends on sip being a real choice.
- **Risks:** players used to sipping freely suddenly meet waking witnesses.
- **Mitigation:** show the daze timer over sipped victims (a fading crimson ring). Lethe stays the Dominion answer.

### P3. Detection has no survivable middle ground (I-03)
- **Current behaviour:**
  - **Act I:** spotted 7×, still won faster than par [Played M02].
  - **Mid-game:** spotted once, all 48 NPCs respond, dead in about 10 s, twice [Played M07].
- **Root cause:** R1. The alarm is a global integer. Shouts have no locality (P1). No player-facing state exists between "unseen" and "map alarm", and no rule says when a hunt ends.
- **Desired experience:** a mistake creates a **local hunt** you can escape, fight or exploit. It leaves lasting marks but no reload.
- **Recommended change:** **The Hunt** (full design in §20–21).
  - Reaction Window plus shout wind-up.
  - A Hunt group with a visible last-known position (LKP).
  - Break contact: out of sight for 4 s, ≥10 m from the LKP, in darkness or Mist.
  - Pursuers fan out in pairs, and stragglers can be taken.
  - Lockdown only from bells or bodies.
- **Size:** Large.
- **Why it's worth it:** it turns the most frequent negative review theme in the genre ("detected = reload" [Research: Styx, Shadow Tactics]) into the game's second-best activity. It also makes Act I alarms matter without making them fatal.
- **WASD implications:** this is the payoff of direct control. Steering an escape under pressure is what a click-to-move game can't do.
- **Risks:**
  1. Run-hide-reset with no weight (the Aragami 2 complaint).
  2. Hunts that never end.
- **Mitigation:**
  - Every escaped Hunt leaves the area Wary, relights its lamps and adds Dossier weight.
  - A second Hunt in the same quarter escalates to Lockdown.
  - Hunts time out at 60 s.
  - The Hunt outline shrinks visibly as it decays.

### P4. No vampire powers for about 40 minutes (I-05)
- **Current behaviour:** `ArtsOpen => MissionIndex >= 3`. Act I is walk, feed, carry and snuff. The hub nags about unspent Marks while the tab is disabled [Code UIHub.cs:90, :195].
- **Root cause:** the campaign arc was planned from the story's "fledgling" beat (R2).
- **Desired experience:** the first minute shows a vampire body. The first chosen power arrives at about minute 12.
- **Recommended change:**
  - Baseline Shadow Dash and grab-drag from M01, and drop-feed from A2.
  - At M01's end, the Abbess offers **one of three first gifts**: Smother, Mesmerize or Pounce.
  - Arts open after M01.
  - Beckon becomes baseline at M02 (the M03 story grant moves earlier).
- **Size:** Medium. Dash and grab are new systems; the gate changes are hours.
- **Why it's worth it:** the demo and the refund window show the fantasy. Mark of the Ninja, Dishonored and Aragami all hand over the signature verb in the first level [Research].
- **WASD implications:** Dash and grab are body verbs, which WASD players value more than menu verbs.
- **Risks:** early power makes M01–M03 trivial.
- **Mitigation:**
  - Dash charges recharge only in the dark.
  - Act I maps are lamp-heavy.
  - The first gift is one verb, not a tree.

### P5. Act I detection costs nothing (I-06)
- **Current behaviour:** spotted, then walk to the objective. The target's door works as a safe zone. "Unbroken" and "Before the Bell" were awarded after 2 alarms [Played].
- **Root cause:** R1 plus R5. Watchmen stop hitting once you're indoors. Rewards ignore alarms.
- **Desired experience:** being seen in Act I starts a Hunt with lanterns that changes the objective, never one that refuses the player's hands. Clean play is visibly worth more.
- **Recommended change** (revised in Revision 2; the refusal is cut):
  - **Consequences by objective type** (see RE-EVALUATED MECHANICS):
    - a target **flees to a safe room** with an escort;
    - theft in view **locks down that quarter's exits** for 60 s;
    - **pursuers follow her indoors**, so doors stop being Hunt-proof.
  - "Unbroken" means no Hunts.
  - Challenges pay (P8). Staying unseen keeps routines, notables and Preparations intact (§20.4).
- **Size:** Small to Medium.
- **Why it's worth it:** the right lesson comes early, so Act II feels like a step up, not a betrayal.
- **WASD implications:** escaping a small Act I Hunt teaches the escape skills the later acts need.
- **Risks:**
  1. A fleeing target escapes the map.
  2. Pursuers indoors break interior navigation.
- **Mitigation:**
  - Safe rooms are authored per target and inside the map.
  - Indoor pursuit uses the same navmesh, and the 60 s Hunt timeout applies.

### P6. The mouse-aimed teleport is the wrong control and too broad a tool (I-04, revised)
- **Current behaviour:**
  - 12 m line-of-sight teleport to a dark point, aimed with the cursor; 12 blood, 1 s cooldown.
  - The water rule holds through the aim (§7), but `Cast` doesn't re-check it.
  - It combines with Gloom and Smother into the Aragami loop: make shadow, then jump into it [Research].
- **Root cause:** R3 (cursor aim while the keys steer) and R4 (power without a counter-rule).
- **Desired experience:** a movement verb you steer, which escapes, closes gaps and goes up, but can't delete a wall.
- **Recommended change:** **replace Shadowstep with Shadow Dash** (§11). Move the water check into `Cast` for every movement ability.
- **Size:** Medium.
- **Why it's worth it:** fixes the control conflict, keeps level topology intact by construction, and makes light the cooldown.
- **WASD implications:** the direction comes from the keys. No aim mode.
- **Risks:**
  1. Players who loved the teleport feel the power fantasy shrink.
  2. Dash spam.
- **Mitigation:**
  - Umbral Step and Between Bars restore the "slip through" fantasy.
  - Two charges recharging only in the dark stop the spam.
  - A run in light empties them.

### P7. Two movement authorities: auto-walk into range (I-08, revised)
- **Current behaviour:** Feed, use and ability keys path Ilse to range when the player isn't pressing keys (D114). Any key push cancels the walk.
- **Root cause:** R3. The click-era action model was kept under direct locomotion.
- **Desired experience:** she moves only when the player moves her.
- **Recommended change:**
  - Remove every pending walk for the player character (scripts and thralls keep `Act`).
  - Out-of-range targets are greyed out with the distance.
  - Grab gets a 0.25 s lunge from up to 2.6 m.
  - Use gets a 1.6 m radius with a highlight.
- **Size:** Small to Medium.
- **Why it's worth it:** no surprise deaths from a body that walked itself into a cone.
- **WASD implications:** this is the WASD contract.
- **Risks:** more "just out of range" frustration.
- **Mitigation:** magnetism (lunge), range rings while an ability is held, and the grey-out with distance.

### P8. Rewards ignore how you play (I-07, I-09)
- **Current behaviour:**
  - Challenges are records.
  - Predator reaches A10 at M10 with 71 Marks for a 66-Mark tree; Ghost reaches A8 at M13 [Sim].
- **Root cause:** R5.
- **Desired experience:** clean play, bold play and replays all pay. Every style reaches its capstone at about M10.
- **Recommended change:**
  - Each challenge pays **+1 Mark the first time** (3–4 per mission).
  - Drain Vitae is flattened (notables still give Marks).
  - Clean optionals give Vitae.
  - **Preparations** (Revision 2: replaces mastery unlocks). Challenges unlock diegetic, one-use options chosen at the briefing, such as a bribed servant, a contact's key, a compromised guard or a sabotaged gas valve (RE-EVALUATED MECHANICS).
  - Target A10 at M12 for every style.
- **Size:** Medium (code is hours; tuning is days).
- **Why it's worth it:** Hitman-style unlocks are preferred to cosmetic badges [Research], and every style gets a reason to replay.
- **WASD implications:** none.
- **Risks:** Mark inflation.
- **Mitigation:** the tree shrinks to about 32 nodes (P9). Re-run `CampaignEconomy.Simulate` after the change.

### P9. Toolkit overlap and bloat (I-10)
- **Current behaviour:** 42 nodes and 20 abilities. Six ways to remove an unaware human; five to neutralise vision.
- **Root cause:** R4. Breadth grew without one-verb-per-job discipline.
- **Desired experience:** each tree has a distinct way of solving the same checkpoint (§25).
- **Recommended change:** the classification in §24: about 32 nodes, a 4-slot loadout, a shared baseline kit.
- **Size:** Medium (data, UI, mission hints, save migration).
- **Why it's worth it:** "depth without complexity" [Research: Mimimi postmortem], and balance becomes tractable.
- **WASD implications:** 4 slots fit next to WASD.
- **Risks:** players with existing saves lose nodes.
- **Mitigation:** refund Marks on load (save-version migration), with a one-time "The Abbess re-teaches you" screen.

### P10. The near band is invisible (I-11; rewritten in Revision 2)
- **Current behaviour:** spotted at 4–5 m in light 0.05–0.19, and at 1–2 m in darkness [Played].
  - The near band is a **sector** inside the cone (4–7 m by type).
  - A 1.4 m **touch zone** sees her from any side.
  - Neither is drawn outside Alt.
  - At its edge in darkness the near band fills in about **0.8 s**; at 3 m, in 0.4 s [Code `DetectionMath`].
- **Root cause:** a correct rule that isn't shown (R6). Revision 1's "ring" would have drawn the wrong shape.
- **Desired experience:** the player knows where "seen in any light" begins *before* stepping into it, and can judge "how close can I get in the dark?" at walking speed.
- **Recommended change:** SR.5–SR.6.
  - The near sector is a solid fill with a solid edge, part of every cone.
  - It shows by itself whenever Ilse is within `NearRange` + 3 m of a guard, even if his full cone is hidden.
  - The touch circle shows within 4 m.
  - Cone-edge grace is drawn dashed.
  - The step preview flashes before she enters either zone.
- **Size:** Small for the drawing (ConeRenderer already builds the near band; it lacks the touch circle and contextual display). Small for the grace.
- **Why it's worth it:** it removes the most frequent "unfair" death.
- **WASD implications:** essential. A 0.8 s rule can't be read *after* entering it; it must be seen in time to steer away.
- **Risks:** clutter near crowds.
- **Mitigation:**
  - Only the 4 most relevant guards show full cones.
  - Beyond that, near sectors only.
  - Low-alpha ground ink, no text.

### P11. Save UX invites the savescum loop (I-12)
- **Current behaviour:** F5 any time, one slot, no age cue.
- **Root cause:** an industry-standard feature never added.
- **Desired experience:** save freely without saving into a lost position, and know how long ago you saved.
- **Recommended change:**
  - A save-age timer by the portrait.
  - 3 rotating quicksave slots.
  - Quicksave disabled while a Hunt has line of sight to Ilse.
  - An opt-in **Ironblood** challenge (no loads) paying +1 Mark.
- **Size:** Small to Medium.
- **Why it's worth it:** it meets Mimimi's standard [Research].
- **WASD implications:** none.
- **Risks:** blocking saves during Hunts feels harsh.
- **Mitigation:** the block applies only while she is *seen*, not for the whole Hunt.

### P12. The camera hides Ilse and threats (new)
- **Current behaviour:** no occlusion or cutaway system found [Code]. Look-ahead is manual.
- **Root cause:** R3. A tactical camera was adapted to direct control.
- **Desired experience:** you always see her, the people near her and their cones.
- **Recommended change:** §12: cutaway cylinder, interior roof hiding, silhouettes, velocity lead, threat markers.
- **Size:** Large.
- **Why it's worth it:** "the camera loses the character" is a top direct-control failure in the research [Research].
- **WASD implications:** central.
- **Risks:** cutaways reveal too much (the shape of rooms ahead).
- **Mitigation:** cut away only around Ilse and existing threats; never pre-reveal rooms she hasn't entered.

### P13. Feeding pins her in place and the controls cancel it (new)
- **Current behaviour:** 1.6/3.2 s stationary channel; any key cancels it [Code VampireMovement.cs:78].
- **Root cause:** R3.
- **Desired experience:** grab, drag to the dark, feed. Interruptible by choice, not by accident.
- **Recommended change:** §28: grab-drag-feed, with darkness speeding it up and light exposing it.
- **Size:** Medium.
- **Why it's worth it:** feeding becomes the signature act rather than a refill button.
- **WASD implications:** a direct-control-native verb.
- **Risks:** dragging becomes a safe "move anyone anywhere" tool.
- **Mitigation:**
  - Drag at 1.4 m/s.
  - A 6 s struggle limit (after which they break free and shout).
  - No climbing or dashing while dragging.
  - Seen in light from 16 m.

### P14. No early answer when a human is aware of you (new)
- **Current behaviour:**
  - Before Rend (Predator tier 3) or Mesmerize, an aware human can only be fled.
  - In melee, watchmen hit hard (HP 49 → 2 in M02 [Played]).
- **Root cause:** R2. Combat was designed as a late Predator unlock.
- **Desired experience:** early on, a scary scramble you can survive by breaking free and running. Later, a choice of answers.
- **Recommended change:**
  - Baseline **Shove** (F on an aware human within 1.5 m): he staggers for 1.2 s; 6 m noise; no blood; 4 s cooldown.
  - **Dash dodges** musket shots during their 0.8 s aim telegraph (shown as a line).
  - Rend moves to Predator tier 2.
- **Size:** Medium.
- **Why it's worth it:** detection becomes a scramble rather than a reload, and builds toward the fight-back fantasy.
- **WASD implications:** dodging a telegraphed shot with Dash is a pure direct-control skill moment.
- **Risks:** Shove-and-feed becomes the new "kill everything".
- **Mitigation:** a shoved human is *aware* (can't be fed on), shouts at once, and the 4 s cooldown means two watchmen beat you.

### P15. Rendered light is not gameplay light (new, Revision 2)
- **Current behaviour:**
  - A gas lamp exposes Ilse within 5.5 m, but its Unity spot lights the ground to about 8.3 m.
  - Across all light types, the visible pool reaches **1.4–1.6×** the 0.35 exposure contour.
  - Lamps beyond the real-time shadow budget render through walls, while gameplay light is occluded by Linecast [Code `GameLight`, `LightSystem.LightAt`, `BudgetShadows`].
  - Exposure takes the brightest single source, so overlaps look worse than they are.
- **Root cause:** R6. Unity lights were set up for looks (`range = √(R²+h²)·1.15`), not from the exposure model.
- **Desired experience:** "where does this lamp stop exposing me?" is answered by the lamp itself, and "which route between these lights is safe?" by the floor.
- **Recommended change:** SR.3–SR.4.
  - The **Exposure Field**: a 0.5 m grid of `LightAt`, with the same occlusion.
  - Unity lights re-authored to end at the contour with a visible knee.
  - Occlusion cookies for lamps outside the shadow budget.
  - A wall-clipped **exposure rim** in the light's hue, within 10 m of Ilse (reusing `BuildBurnRing`'s method).
  - A level lint for dark-route widths.
- **Size:** Medium (field, rims, ranges). Medium (cookies).
- **Why it's worth it:** light is the core read (DO NOT BREAK #3). Right now it lies by about 50% of its area.
- **WASD implications:** a WASD player reads the floor, not a meter. The floor must be true.
- **Risks:**
  1. A darker-looking city.
  2. Mood lost to a "UI-looking" rim.
- **Mitigation:**
  - Keep ambient fill and moon colour.
  - The rim is thin, in the light's own colour, and contextual.
  - An art pass on the knee.

### P16. Vision is hidden behind Alt, and detection doesn't explain itself (new, Revision 2)
- **Current behaviour:**
  - Cones show only while Alt is held, or for inspected guards.
  - Meters show only for on-screen humans.
  - At Spotted, the game knows the band, distance, light, light source and modifiers [Code `Npc.Perceive`], and shows none of them.
  - The light read is a 3-state corner eye, but only one threshold (0.35) matters.
- **Root cause:** R6, and click-era assumptions (a click player can hold Alt while waiting; a WASD player is steering).
- **Desired experience:** normal play shows what can hurt her in the next couple of seconds. Every detection ends in "Yep, I pushed too far."
- **Recommended change:** contextual cones (SR.6), the hierarchy (SR.12), the ground disc and step preview (SR.7–8), off-screen meters (SR.9), and the Spotted Explanation with a debrief Detections page (SR.10).
- **Size:** Medium.
- **Why it's worth it:** it is the precondition for judging the Hunt and the Reaction Window. Players must be detected for *mistakes*, not for confusion.
- **WASD implications:** central. Alt plus steering plus a corner meter is three places at once.
- **Risks:** a board-game look; stealth made trivial.
- **Mitigation:**
  - Ground-only, low-alpha, capped, contextual ink.
  - Truthful but local information.
  - On Apex, a smaller Normal tier (SR.12).

---
## 16. Alternative solutions for the four hardest problems

### 16.1 Detection without a middle (P3)

| Option | What it is | For | Against |
|---|---|---|---|
| **A: Tune only** | Fix the bugs; local shouts; nothing else | Hours; uses existing states | Alarms still end in "survive by luck or reload"; no player verbs during an alarm |
| **B: Staged alarm with a break-contact timer** | Suspicion → local Hunt → Lockdown; out of sight N s ends the Hunt | Days; readable | The Aragami 2 trap: run, hide, reset, weightless |
| **C: The Hunt as a pillar** | B, plus the Reaction Window and wind-up, a visible LKP, pursuers in pairs who can be split and ambushed, persistent marks (Wary, relit lamps, Dossier), and Lockdown as a survivable map state | Failure becomes play; uses WASD's strengths; feeds the Dossier honestly | Large; needs AI search tuning and hand-play |
| **D: Eriksholm model** | Detection = instant fail with dense checkpoints | Simple, tense | Kills improvisation [Research: Eriksholm criticised as linear]; wrong for a power-fantasy arc |

**Choice: C.** B is the minimum and C is what makes the game. A and D fail the brief's "stealth failure ≠ automatic reload".

### 16.2 Excessive waiting (R2)

| Option | What it is | For | Against |
|---|---|---|---|
| **A: Enemy manipulation** | Baseline Beckon from M02 (tap = come to my voice); first gift Mesmerize; thrall False Orders later | On-theme; creates openings | Mind tools only; early levels still wait if Beckon is on cooldown |
| **B: Patrol redesign** | Interlocking routes with natural 6–10 s gaps; distractible routine stops (smoke breaks, privies) | Free openings; observation pays | Pure level work; still waiting, just shorter |
| **C: Mobility** | Dash plus roofs to bypass | WASD fun | Bypass is avoidance, not predation; risks the Aragami "sprint through" |
| **D: Hybrid** | Beckon baseline (A); routines with **stops that can be extended or exploited** (B: a guard at a privy, a lamplighter's route); Dash for the last 6 m (C); the blood clock (§29) pushes you to act | Each part is moderate | Needs level passes |

**Choice: D.** The baseline lure is the high-leverage piece. Levels add 1–2 exploitable routine stops per area. Starving makes waiting audible; Warm blood (Revision 2) makes acting after a feed pay.

### 16.3 The early game (P4)

| Option | What it is | Verdict |
|---|---|---|
| A: Open the Arts after M01 | Hours | Necessary but not sufficient |
| B: Free Smother at M01 end (audit) | Hours | A menu verb; no body change |
| **C: Baseline body kit from M01** (Dash, grab-drag, drop-feed at A2) **plus a first-gift choice of 3** at M01 end | Medium | **Chosen.** The body sells the vampire, and the choice starts the build identity in the first 15 min |
| D: Shrink Act I to a 10-min prologue | Large rework of M01–M03 | Not needed if C lands. M02/M03 are good levels |

### 16.4 Save behaviour (P11)

| Option | Verdict |
|---|---|
| A: Keep free quicksave | Fails the genre standard |
| **B: Mimimi standard** (timer, 3 slots), plus no save while seen, plus an opt-in Ironblood challenge | **Chosen** |
| C: Invisible Inc rewinds (5/3/1) | Elegant, but players of this genre expect quicksave [Research]; a fit for a future NG+ |
| D: Coffins (in-world save points) | On-theme, but checkpoints punish long maps (48 NPCs in M07) |

---

## 17. High-leverage systems

Ranked by how many problems one change fixes:

| System | Fixes | Leverage |
|---|---|---|
| **The Hunt** (local, staged, escapable pursuit) | P1, P3, P5, P11 (quickload pressure), emergence, Act I lesson, mid-game death wall, Dossier honesty, WASD agency | **Highest** |
| **Shadow Dash** (baseline, dark-recharging) | P4 (early verb), P6 (teleport), escape in the Hunt, light tension, WASD mastery, level contract | Very high |
| **Grab-drag-feed** | P13, the fantasy, sip/drain decisions, evidence decisions, darkness value, Predator identity | High |
| **Soft-target quick-cast** | P7, aim conflict, pad support, ability use rate, fluidity | High |
| **Vampire Laws as the level contract** | P6, level design, folklore, Dossier counters, build identity (Silverblood, Lethe…) | High |
| **Paid playstyles** (challenges, mastery, flattened Vitae) | P8, replay, Act I recklessness, Ghost trailing | High |
| **Baseline Beckon** (tap = follow my voice) | Waiting, early agency, Puppeteer foundation, isolation play | Medium-high |
| Camera occlusion | P12, interiors, WASD trust | Medium-high (necessary rather than differentiating) |

**The two to build first are the Hunt and Shadow Dash.** Together they make failure playable and give the body something to master.

---

## 18. The core loop: as played versus redesigned

**The real loop today** [Played, M02 and M07]:
```
Act I:   observe cones → wait for the turn → walk in shadow → feed from behind (stand 3 s)
         → carry body to canal → (seen? keep walking; nothing happens)
Mid:     observe → Shadowstep past the problem → (seen? whole map converges → dead in 10 s → quickload)
Late:    fill blood → Apex → Rend the post → lockdown → walk out
```

**The redesigned loop:**
```
READ        light, cones, routines, the Dossier's countermeasures for tonight
  → CHOOSE  the person (blood type, notable, isolation) and the place (where is dark?)
  → SHAPE   create the opening: Beckon to your voice / Smother the lamp / thrall False Orders / Snare
  → CLOSE   sneak-shadow behind them, dash the last 6 m, or drop from a roof
  → TAKE    grab → drag into the dark → sip or drain (or Mesmerize and pass)
  → HANDLE  the evidence: body to canal or hide spot, dazed sipper will wake in 45 s, stain, witnesses
  → (MISTAKE → wind-up → silence him, or HUNT → break contact / split them and take one / mist under a door)
  → REPOSITION  roofs, dark loops; dash recharges in the dark; heal in the dark (paid in blood)
  → EXPLOIT     the new state: Wary guards walk in pairs and leave posts empty; a relit lamp means a lamplighter is walking
```

**What changed:**
- **SHAPE** replaces *wait*.
- **TAKE** is a steered act.
- **MISTAKE** branches into play instead of a reload.
- **EXPLOIT** feeds consequences back as new opportunities. A Hunt pulls guards off posts, which is an opening elsewhere.

---

## 19. Reducing passive waiting: "create an opportunity"

**Target:** in any 60 s of careful play, the player should have **at least one action that creates an opening** available, without waiting for a patrol cycle.

| Tool | Available | How it creates an opening |
|---|---|---|
| **Beckon** (baseline M02) | Every build | Tap: the target walks to where you stood. You step aside into the dark and take him there. 6 blood. Draws one, never a pair (a paired guard checks with his partner first) |
| **Shadowing** (baseline) | Every build | Tail a walking guard by hand (sneak 2.2 outpaces his 1.6). Footfall masking lets her close the last 3 m at walk speed silently. Take him at the dark stretch of his route; no waiting at the end of the loop |
| **Door noise / a thrown stone** | **Baseline** "Rattle": G on a shutter, crate, bell-pull or chain makes a 10 m noise at that object | Pulls the nearest relaxed human to investigate. Levels place 2–4 per area. Environmental, readable, free; overuse makes the area Wary |
| **Smother** (first gift / Shade) | — | A dark lamp pulls a lamplighter or the nearest guard to relight it: a predictable walker on a known path |
| **Blood scent** (Snare plus False Trail, Blood Mage) | — | Hounds and trackers go to the rune |
| **Fear** (Terror, Predator) | — | A witness who panics flees *away* (Herding folded in), clearing a post |
| **False Orders** (Puppeteer) | — | Send a guard elsewhere for 30 s |
| **Mimicked voice** (Familiar Voice mod) | — | Beckoned humans don't turn Wary: you can reuse the same lure |
| **Gas valves / bells / doors** (level) | Every build | Kill a lamp group; ring a decoy bell in another quarter (brings a squad there, away from you) |

**Level rule:** every guarded objective area has
- at least one **routine stop** (a guard who goes somewhere alone on a timer: privy, smoke, lamp check), and
- at least one **Rattle object** within 15 m of a post.

**Blood rule** (revised in Revision 2: no passive drain):
- Starting at 50%, with abilities costing blood, means a feed is needed early.
- Starving makes waiting audible (already true).
- **Warm blood** after a feed rewards *acting* rather than taxing waiting.

---

## 20. Making failure interesting: layered detection

**Yes, failed stealth can be one of the most exciting parts of the game.** Direct control is why: escape is steered, not ordered.

### 20.1 States
| # | State | Trigger | What the AI does | What the player sees | Player options |
|---|---|---|---|---|---|
| 0 | **Unaware** | — | Routine | A bone cone when contextual (SR.6); a solid near sector and touch circle when close | Everything |
| 1 | **Suspicious** | Meter ≥ 0.35; a step heard; a dark lamp; a door creak | Stops, turns, looks (exists) | An amber cone, pulsing, swung toward the cause; "?"; always shown within 25 m | Freeze in darkness; back out; Beckon elsewhere |
| 2 | **Investigating** | Meter ≥ 0.6 or a noise source | Walks to the point, searches 10 s, then Wary (exists) | An amber cone plus a dotted amber path to the point | Ambush at the point; avoid |
| 3 | **Spotted** (new) | Meter = 1.0 | **Reaction Window plus a 1.0 s shout wind-up** | A red cone; a red ring growing at his head; slow-mo once; **the Spotted Explanation** (sight-line, flashed boundary, caption) | **Silence him** (grab, Pounce, Mesmerize, Rend), or break LOS and leave his near band → back to Investigating |
| 4 | **Hunt** (new, local) | The shout completes | His group plus anyone within the shout radius (10/15/22 m) joins. **LKP marker** placed. Lanterns raised. Hunters throw flares at the LKP. Pursuers move in **pairs** | Red cones, visibly wider (+10°) with their near edge 20% further out; a dashed red Hunt outline; a dotted red LKP ghost; edge pips for pursuers; the shout as a red ripple showing who heard | Break contact; split them; ambush a straggler; go up; Mist; drop into water-free sewers; threshold a home |
| 5 | **Lost** (new) | Out of sight ≥ 4 s and ≥ 10 m from the LKP, in darkness (or Mist) | Pairs sweep 3 search points around the LKP for 30–45 s, then return Wary | Red cones turn **dashed**; the outline fades and shrinks; red "?" pins at search points within 15 m; "They've lost you" bark | Re-hide; pick off sweepers (they're spread out) |
| 6 | **Lockdown** (map) | A bell is rung; 3 bodies found (2 with cm_paired); a second Hunt in the same quarter within 120 s | Exists: everyone Wary, gates closed, lamps relit, reinforcements, the Vigil squad | Map-wide red vignette; bell icon | Survive it: dark routes, Mist, thresholds, or fight (late Predator). The mission continues; challenges are lost |

### 20.2 Costs that persist (so escaping isn't free)
- The area stays **Wary**, which already exists and is persistent.
- **Lamps relit** inside the Hunt area.
- **Dossier weight** for the habit that caused it.
- **"Unbroken" lost.**
- The Watch posts pairs instead of singles in that quarter for the rest of the mission.

### 20.3 What makes it exciting rather than tedious
- **Pursuers in pairs** spread across 3 search points mean someone is always *alone for a moment*: the predator's opening.
- **The LKP ghost** shows where they think you are. Moving away from it is the obvious, readable escape. Ambushing near it is the bold play.
- **Hunts end.** A 60 s cap, plus "Lost" being reachable in 4 s with a good break, keeps the rhythm short.

**Witness management:** a Hunt formed by a *civilian* (a scream) brings the nearest 2 watchmen, not a whole group. Civilians who saw a feed and fled can be caught before they reach a watchman. That is the chase as a sub-game.

### 20.4 Keeping the Hunt in its place (Revision 2)

The Hunt is a **recovery layer, not the game.** A stealth game in which a Hunt starts every five minutes has become an action game with stealth intervals. These rules keep stealth the main mode.

**Budget (a design target, checked by telemetry):**
- **A careful player: 0–1 Hunts per mission.** A typical player: 1–2.
- **Any mission averaging more than one Hunt per 10 minutes** across testers is flagged. That is **a level or readability bug**, not a tuning question, and the Readability Test (§44) comes first.
- The middle states absorb small mistakes before a Hunt:
  - Suspicious and Investigating;
  - the cone grace;
  - the Reaction Window (§14).

**Staying unseen must pay.** These are advantages, not just the absence of a penalty:

| Unseen | After a Hunt |
|---|---|
| **The map stays calm.** Routine stops (privy, smoke, lamp check) continue, which are the openings §19 relies on | Routine stops are **cancelled** in that quarter, and posts are doubled into pairs |
| Notable targets keep their routines | Notables in that quarter **hole up** in a safe room with an escort |
| Feeds are in the dark: fast, unseen, and Warm (§RE) | Lamps are relit, and hand lanterns make dark feeds rarer |
| No Dossier weight; clean missions **decay** countermeasures faster | +1 Dossier weight toward the habit that caused it |
| Challenges and Preparations (§RE) are earned | "Unbroken" is lost, and so is one challenge |

**The Hunt costs, during and after:**
- Dash charges spent in light **don't refill**.
- Mist costs 3 blood a second.
- Time passes (routines elsewhere change).
- Lanterns visibly grow the Exposure Field (SR.3).
- A body taken in a Hunt is still a body.
- **No Marks or Vitae are earned from Hunt kills**, so the Hunt is never a farm.

**Escalation:**
- A second Hunt in the same quarter within 120 s means **Lockdown** (as in §20.1).
- A third Hunt in a mission sends the **Vigil squad**, from Act II on.

**Exciting, recoverable, systemic, and occasionally exploitable:**
- **Exciting:**
  - pairs split up;
  - the last-known position ghost (LKP) gives a readable target to run from or ambush near;
  - escape is steered.
- **Recoverable:** Lost is reachable in 4 s with a good break, and the 60 s cap applies.
- **Systemic:** pursuers use the same cones, light and noise. Every boundary the player reads while sneaking still applies while fleeing.
- **Occasionally exploitable:**
  - A Hunt pulls guards **off their posts**, so a deliberate Hunt can empty an objective.
  - The cost of that is Wary areas and a "Bold" Dossier entry, which posts watchers at the objective on the *next* mission.
  - It is a real, expensive option, not a trick.


---

## 21. Pursuit as a pillar

**Recommendation: yes, make pursuit and escape a proper system** (the Hunt above), with these verbs and level supports:

| Escape verb | Mechanic | Level support |
|---|---|---|
| Breaking sightlines | Corners, roofs, doorways; LOS-based (exists) | Every lit hub has 2+ exits into a dark loop |
| Climbing | Push-to-climb (exists); Rise with Dash | A climb point within 10 m of every hub |
| Shadow dashing | 2 charges, dark recharge | Gaps and alley mouths 4–6 m wide |
| Windows | Climb links into non-home buildings; Between Bars passes through | 1–2 per block |
| Creating darkness | Smother the lantern a pursuer carries? **No**: hand lanterns are the counter. Smother a wall lamp to make a dark pocket; Gloom (vision-blocking) | Lamps on escape routes, not on dead ends |
| Transforming | Mist: unseen in the dark, under doors; breaks contact in 1 s out of LOS | Doors with gaps; vents |
| Misleading | Beckon a pursuer away; drop a Snare at a corner; False Trail draws hounds | — |
| Splitting pursuers | Pairs separate at search points; a Rattle object draws one of the pair | Search points placed around corners from each other |
| Ambushing an isolated hunter | Grab from the dark; drop-feed from a ledge | Ledges over alleys |
| Alternate routes | Rings; sewers (no running water: still); roofs | Loops of 30–60 m |
| Thresholds | **Homes** stop her uninvited, and stop the Watch too: they won't enter a citizen's home in a hunt without an officer | Faithful homes (Rumour) left open = escape hatches |

**Pursuers' tools** (so the chase stays tense):
- **Lanterns:** carried light; can't be Smothered; drop them by killing the carrier.
- **Flares** (Vigil): 20 s of light at the LKP.
- **Hounds:** smell within 5 m through darkness; follow blood trails.
- **Whistles:** call one more pair.
- **Muskets:** a 0.8 s aim telegraph, dodgeable with Dash.

---

## 22. Combat

| Question | Answer |
|---|---|
| Should combat be viable? | **Yes, briefly and expensively.** It is a stealth game in which the vampire is physically superior and *politically outnumbered* |
| Dangerous? | Always. Watchmen hit for about 10% max HP; muskets about 30%; silver stops regen for 10 s (exists) |
| Primarily recovery? | **Yes early and mid** (Shove, silence a witness, break a grab). **Late Predators can choose it**, at the price of blood and Lockdown |
| Bursts of overwhelming violence? | **Yes, late, as the Predator capstone** (Apex): the A9 test showed it works and feels earned [Played: 6 kills in 25 s, HP 111→29, blood 140→20] |
| Significant blood? | Yes. Rend 15; Apex 35; Shove free but loud |
| Large groups dangerous? | **Yes, always.** Three or more watchmen in melee beat a Predator without Apex. A Vigil squad beats Apex if she stays |
| Hunters able to fight her? | **Yes**: silver blades, flares, nets (Act III Vigil; see §27) |

**Combat verbs** (no parry, no combos; the Styx lesson [Research]):

| Verb | Who | Effect |
|---|---|---|
| Shove (baseline) | All | 1.2 s stagger, 6 m noise, 4 s cooldown. Breaks a grab on her |
| Dash (baseline) | All | Dodge a telegraphed shot or blade swing (0.8 s / 0.5 s tells) |
| Rend (Predator T2) | Predator | Instant kill of an aware human in melee. 15 blood. Loud 8 m. **Witnessing watchmen make a morale check**: alone → flee, in a pair → hold (Terror upgrades this) |
| Apex Hunt (Predator capstone) | Predator | 6 s, others at 35% speed, feeds instant, kills refund 10 blood (max 3 refunds) |
| Mesmerize (gift / Dominion) | Puppeteer | Freezes one aware human (but not Vigil with wards) |
| Hemorrhage (Blood Mage) | Blood Mage | Ranged silent kill of an *unaware* target only |

**Not added:** health pickups, weapons, a block button or a stamina bar. Healing stays in darkness, paid in blood.

---

## 23. Dominant strategies: why they work and how to change the incentive

| Strategy | Why it works | Why players use it | Why it gets repetitive | Fix the reason, not the behaviour |
|---|---|---|---|---|
| **Quicksave before every guard** | Detection = death mid-game (R1); free F5 | It is the only rational answer to a binary failure | Every encounter is played twice | **The Hunt** makes detection survivable, so the reason to save-scum shrinks. Then a timer, 3 slots, no saving while seen, and a paid Ironblood challenge |
| **Sip everything** | Bug B: they never wake | No body, no wake | Every takedown is the same | Fix the bug. Sip then means a witness in 45 s; drain means a body. Each fits different spots (§28) |
| **One movement power for everything** (Shadowstep) | Cheap, long, cursor-aimed, low cooldown | It solves movement, escape and gaps | Blink-execute (Aragami's top complaint [Research]) | **Dash**: short, navmesh-bound, dark-recharged. Gates use the Vampire Laws, so no single power answers all of them |
| **Apex → Rend refund chains** | Refund 15 per kill, uncapped | Free kills | Every post is solved the same way | Refunds capped at 3; Apex 35 blood with a 60 s cooldown; **the Vigil doesn't slow** in Act IV (Silverblood mirror: "Vigil sacraments") |
| **Walk through Act I alarms** | No consequences (R1, R5) | Fastest, best-paid run | Teaches the wrong lesson | Consequences, not refusals: targets flee to safe rooms, pursuers follow her indoors, exits lock after a witnessed theft; "Unbroken" means no Hunts; challenges pay; the unseen keep routines and Preparations (§20.4) |
| **Darkness camping** (new risk, from cone-edge grace plus the Hunt) | Darkness hides outside his near sector; Lost after 4 s | It's safe | Waiting again | Hunters' flares at the LKP; hounds smell through darkness; Lost sweeps pass within 3 m of LKP-adjacent dark spots |
| **Beckon-chain** (new risk) | Tap Beckon, step aside, grab, repeat | Cheap and safe | One verb for everything | Paired guards don't come alone; each Beckoned guard who returns makes the area Wary (unless Familiar Voice); bodies still need hiding |
| **Drag everyone into one dark corner** (new risk) | Drag at 1.4 m/s | Clean | Same corner every time | Bodies found together count as a massacre (Lockdown at 2); struggle noise if dragged over 6 s |
| **Mist everywhere** (new risk) | Mist breaks contact | Safe escape | — | 3 blood/s; can't act; seen as fog in light; hounds smell it; censer smoke dispels it |

**Levels favouring one approach.** M07's crossings are already multi-answer: bridge, roofs, mill, ferry and thrall. The rule (§30) is that **every gate has 2+ answers from different trees, plus one baseline answer.**

---
## 24. Every ability: KEEP / REDESIGN / REPLACE / REMOVE / COMBINE / BASELINE

**Current:** 42 nodes, 20 actives, 6 slots [Code Data/Skills.cs]. **Target:** about 32 nodes, about 14 actives, 4 slots, plus the baseline kit.

### 24.1 The baseline kit (every build, no slot)
| Verb | Key | From | Source |
|---|---|---|---|
| Sneak / Walk / Run | Ctrl / – / Shift | M01 | Exists |
| Shadowing (footfall masking) | (walk within 3 m behind a walking, unaware human) | M01 | New; no speed-match (Revision 2) |
| **Shadow Dash** (2 charges, dark recharge) | Q | M01 | Replaces Shadowstep |
| **Grab → Drag → Sip/Drain** | F (grab, sip) / R (drain) | M01 | Reworks Feed |
| Shove | F on an aware human | M01 | New |
| Carry / Hide body | G | M01 | Exists |
| Snuff lamp (in reach) | G | M01 | Exists |
| Rattle | G on a marked object | M01 | New |
| Blood Sense | V (hold) | M01 | Exists (sense, 1 blood/s); stays baseline |
| **Beckon** (one-stage) | Slot-free: B | M02 | Was Dominion T0; story grant at M03 → M02 |
| Drop-feed | (drop onto a human) | A2 | Was a Predator skill (pounce_silent/Soft Landing area) and A7 |
| Leap / Wallcrawler / Bound | traversal links | A3 / A4 / A5 | Exists (Awakening) |

### 24.2 Classification table

| Tree | Node | Verdict | One-line reason |
|---|---|---|---|
| Predator | Stalker | **KEEP (sharpened)** | Baseline Shadowing is footfall masking. Stalker removes his touch zone *behind* him (exists: `predator.stalker`, d < 4, > 100°), gives +50% feed speed, and victims struggle 2 s longer. The touch circle is drawn with its rear arc gone |
| Predator | Pounce | **REDESIGN** | Dash-into-grab (below) |
| Predator | Soft Landing (pounce_silent) | **COMBINE → Pounce** | A noise rule, not a choice |
| Predator | Gorge | KEEP | +25% max blood: more room before Overflow |
| Predator | Scent | **COMBINE → Blood Sense** upgrade (Hunter's Pulse) | Two sense nodes is one too many |
| Predator | Rend | **REDESIGN** | T2, 15 blood, loud, morale check (below) |
| Predator | Terror | KEEP + absorbs **Herding** | One fear node with two effects |
| Predator | Herding | **COMBINE → Terror** | — |
| Predator | Bound | **BASELINE (A5)** | Traversal belongs to everyone |
| Predator | Apex | **REDESIGN** (numbers) | 35 blood, 6 s, refund 10 × max 3, cd 60 |
| Predator | Dread Feast | KEEP | Capstone: feeding in sight causes panic, not alarm |
| Shade | Smother | KEEP | The core shadow verb; first-gift option |
| Shade | Lingering Dark | KEEP | Duration |
| Shade | Black Main | KEEP | Chain to the gas group |
| Shade | Shadowstep | **REPLACE → Shadow Dash (baseline)** | §11 |
| Shade | Between Bars | KEEP as a **Dash mod** | — |
| Shade | (new) Umbral Step | **ADD** (Dash mod) | +1 charge, 1 s unseen after a dark dash |
| Shade | Nightblood | KEEP | Darkness regen |
| Shade | Gloom | **REDESIGN** | Radius 4, 10 s; a guard who sees it turns Suspicious |
| Shade | Mist | **REDESIGN** | The escape form (below) |
| Shade | Eclipse | KEEP (tune) | Radius 18 (from 25), 40 blood |
| Shade | Shroud | KEEP | — |
| Dominion | Beckon | **BASELINE** (one-stage) | Opportunity creation for every build (§19) |
| Dominion | Familiar Voice | KEEP | Beckoned humans don't turn Wary |
| Dominion | Mesmerize | KEEP | First-gift option |
| Dominion | Lethe | KEEP | Mesmerized forget; also wipes a sip-victim's memory |
| Dominion | Enthrall | KEEP | — |
| Dominion | Second Puppet | KEEP | — |
| Dominion | False Orders | KEEP | — |
| Dominion | Puppet Strike | KEEP + **Signal** (§14) | — |
| Dominion | Court | **COMBINE → Mesmerize capstone "Rapture"** | Mass Mesmerize at radius 8 for 6 s |
| Dominion | Living Lie | **REMOVE** | A passive for a single social case; complexity without decisions |
| Sanguis | Blood Sense | BASELINE (exists) | — |
| Sanguis | Sense Free / Hunter's Pulse | KEEP (absorbs Scent) | No cost while still; shows blood types |
| Sanguis | Bloodmend | **REMOVE** | A heal button removes the darkness-regen decision; Nightblood and feeding cover it |
| Sanguis | Clean | KEEP | Evidence verb |
| Sanguis | Snare | KEEP | Ground rune, 15 blood, max 2 |
| Sanguis | Hemorrhage | **KEEP + tweak** | A found pool causes panic within 12 m and lays a trail hounds follow |
| Sanguis | Corpse Puppet | **REMOVE** | Overlaps Enthrall and False Orders; uncanny but rarely the best tool [Played: unused in all bot runs] |
| Sanguis | False Trail | KEEP | — |
| Sanguis | Silverblood | KEEP | The Law-breaker capstone (§33) |
| Sanguis | Communion | **REDESIGN** | Also erases the evidence of bodies in radius (absorbs the job of Corpse Puppet's "hide by walking") |
| Sanguis | Vessel | KEEP | Interacts with Overflow (Revision 2) |

**Result:** 31 nodes. Each tree solves a checkpoint in a distinct way (§25).

### 24.3 Redesigned abilities in full

**Pounce (Predator T1)**
- **Function:** a Dash that ends in a grab. If a dash ends within 1.5 m of a human's back (or of any Dazed / Mesmerized human), it converts to a grab with no lunge window.
- **Targeting:** none. It is a Dash in the move direction with a 30° magnet cone to the nearest valid back.
- **Blood cost:** 10. It uses a Dash charge.
- **Limitations:** unaware humans, or aware humans who are not facing her (±60°). Vigil hunters with "nets" (cm_caged in Act III) break free in 0.5 s.
- **Tactical role:** crosses the last 6 m of light. It is the Predator's answer to a guard at a post facing a lamp.
- **WASD interaction:** pure direct control. You aim with your body.
- **Use when stealth fails:** silences a spotter in the wind-up from 6 m.
- **Upgrades:** *Stalker* (struggle +2 s); *Apex* (instant feed after Pounce).
- **Synergies:** Gloom (dash through a vision block); Terror (a Pounce in view triggers the morale check).
- **Counterplay:** paired guards (the second shouts); lamps behind posts (dashing in light shows a blur); hounds.

**Rend (Predator T2)**
- **Function:** kill an aware or unaware human in melee instantly.
- **Targeting:** soft target, 2.2 m.
- **Blood cost:** 15.
- **Limitations:** loud (8 m). Leaves a bloody body (found = treated as 2 bodies toward Lockdown). Vigil hunters with silver mail: 2 Rends.
- **Tactical role:** fight-back in Hunts; the late-game "break the post" verb.
- **WASD interaction:** close with Dash, Rend, Dash out.
- **Use when stealth fails:** the primary Predator recovery.
- **Upgrades:** Terror (witnesses alone flee, pairs hold, and flee if the other dies); Apex.
- **Synergies:** Dread Feast; Hemorrhage pool fear.
- **Counterplay:** groups of 3 (the third shoots); muskets with telegraphs; bells.

**Gloom (Shade T2)**
- **Function:** a 4 m radius sphere that blocks vision through it and darkens light inside to 0 for 10 s.
- **Targeting:** ground reticle at the cursor or right stick, max 14 m.
- **Blood cost:** 18.
- **Limitations:** a guard who sees the cloud form or sees it from outside turns **Suspicious** and walks toward it (it is unnatural). Censers (cm_censer) dispel it in 3 s.
- **Tactical role:** cover a crossing for one move. A lure as well (they come to look).
- **WASD interaction:** cast while running across.
- **Use when stealth fails:** a break-contact tool. A Hunt pursuer entering it loses her.
- **Upgrades:** Lingering Dark (+5 s).
- **Synergies:** Pounce through it; Snare inside it (they come to look and step on it).
- **Counterplay:** censers; lanterns inside light it to 0.4.

**Mist (Shade T2)**
- **Function:** become fog. Silent, passes under doors, through vents and bars; **out of LOS for 1 s in Mist = Lost** (§20). Can't act (no feed, no ability, no carry).
- **Targeting:** toggle.
- **Blood cost:** 6 to enter, then 3/s.
- **Limitations:** visible as fog in light (any light > 0.35: detection +50% speed). **Hounds smell it.** Censer smoke dispels it and drops her (Silverblood resists it).
- **Tactical role:** the Shade escape and interior infiltration.
- **WASD interaction:** steer the fog. Speed = walk.
- **Use when stealth fails:** the primary Shade recovery.
- **Upgrades:** Shroud (Mist through a Gloom costs nothing).
- **Synergies:** Gloom (fog inside the cloud is invisible); Between Bars (no longer needed for bars, so it is a choice).
- **Counterplay:** hounds, censers, flares (light reveals the fog).

**Apex Hunt (Predator capstone): numbers only**
- 35 blood (from 45), 6 s (from 8), others at 35% speed (unchanged), cd 60.
- Kills refund 10 blood (from 15), **max 3 per use**.
- Vigil hunters in Act IV are **unaffected by the slow** ("sacraments").
- Upgrades: Dread Feast; Rend; Terror.

**Communion (Sanguis capstone)**
- **Function:** radius 12. Every corpse in radius is drained to dust. Bodies vanish, stains remain; bloody bodies leave a dust stain that hunters read as evidence of a vampire (+Dossier) but not as a Lockdown body.
- **Blood:** +10 per body (from the dust).
- **Limitations:** 25 blood to cast; 3 s channel, stationary.
- **Tactical role:** the "evidence" capstone: lets the Blood Mage kill freely, then tidy.
- **Counterplay:** cm_inquest reads dust stains as a body.

**Beckon (baseline): see §13 for targeting.** 6 blood, 20 m, one human. A paired guard checks with his partner first (2 s), then comes with him. Wary guards come cautiously, lantern raised.

**Rapture (Dominion capstone, from Court):** 40 blood, radius 8, every human Mesmerized for 6 s; Vigil with wards are immune.

---

## 25. Builds: the same encounter, four ways

**Encounter: M07 Leat Bridge checkpoint.**
- **Approach:** a lit 14 m stone bridge over the leat (running water: can't Dash or Mist across the water; must use the bridge, the leat-house roofs, the mill wheel or a thrall).
- **On the bridge:** a sergeant and 2 watchmen at the north end, under a brazier pair (light 0.8).
- **Island end:** a Vigil look-up hunter on the island end (looks at roofs every 12 s).
- **Off the bridge:** the leat-house roof (climb point via downpipe) runs parallel 6 m east, with a 5 m gap to the mill roof. The mill has a door onto the north bank (locked) and a vent.
- **Civilians:** a lamplighter walks the north bank every 90 s.

| Build | Loadout (4 slots) | How it plays |
|---|---|---|
| **Predator** | Pounce, Rend, Terror (passive), Apex | Sneak-shadow the lamplighter onto the bridge; when he relights the north brazier the guards turn to look; **Pounce** the watchman at the rail (dash from the bridge's dark centre), drag him over the parapet, drain. The sergeant turns: **Reaction Window**, Rend in the wind-up. The second watchman alone: Terror check → flees north, into the town (Hunt starts, but it's *his* fear, not a shout). Hunter looks up, sees nothing on the roof: she's on the bridge. About 40 s, loud, Hunt on the north bank she must now break |
| **Shade** | Smother, Gloom, Mist, Between Bars (mod) | Smother the north braziers (lamplighter now comes, the guards move to cover him: they bunch). Leat-house roof via downpipe, **Rise** to the ridge. Hunter's look-up is on a 12 s cycle: wait for it, **Dash** the 5 m gap to the mill in the dark. Mist through the mill vent, out the north door gap. No contact, no kill, about 70 s |
| **Puppeteer** | Mesmerize, Enthrall, False Orders, Familiar Voice (mod) | **Beckon** the lamplighter (Familiar Voice: no Wary) into the dark under the leat-house, **Enthrall** (1 s). Thrall walks onto the bridge with lantern: guards accept him. Ready **False Orders** on the sergeant ("Captain wants you at the mill"); he leaves with one watchman. Ilse walks the bridge behind her thrall; **Mesmerize** the remaining watchman as she passes. About 60 s; one thrall committed |
| **Blood Mage** | Snare, Hemorrhage, False Trail, Sense upgrade | **Hunter's Pulse** shows the sergeant is choleric (strong blood). **Snare** at the bridge's dark centre, **Beckon** the nearer watchman onto it (rooted, silent). Hemorrhage the hunter on the island (unaware, 10 m): his pool is found by nobody since he stood alone. Sergeant hears nothing; she drains the rooted watchman, **Clean** the stain, walks past the remaining watchman's back. About 50 s; one kill, one feed, no evidence |

**Each loadout uses a different law answer:** Predator uses the bridge itself (force), Shade uses roofs and the mill (route), Puppeteer uses people (social), Blood Mage uses rules on bodies (evidence). That's the test every major checkpoint must pass.

---

## 26. Progression: the arc from prey to predator

| Stage | Missions | New capabilities | New decisions | How enemies respond | Level evolution | WASD feel | Player confidence |
|---|---|---|---|---|---|---|---|
| **Early** (prey) | M01–M03 | Baseline kit: Dash (2), grab-drag, Shove, Beckon (M02), drop-feed (A2); **first gift** (1 of Smother / Mesmerize / Pounce) | Sip or drain; drag where; Dash now or save it for the escape | Watchmen alone or in pairs; lanterns; Hunts that end quickly; no Vigil | Lit streets, dark alleys, simple roof routes, 1 law (thresholds in M03) | Moving through light under pressure; learning cone edges and the near band | "I can get out of this" |
| **Early-Mid** (hunter) | M04–M06 | Arts open; 1–2 tier-2 nodes; Leap (A3), Wallcrawler (A4) | Which tree to commit to; which law to break vs. respect | Dossier from M03 (max 2 visible counters); Vigil hunters appear at M05 (look up, flares) | Running water (M04), interiors with occlusion (M05, M06), first gas network | Rooftop routes and drops; Dash chains | "I choose how" |
| **Mid** (predator) | M07–M09 | Tier 3 in one tree; second slot from another; Bound (A5); thralls (Dominion) | Combining two trees; Hunt as a tactic (pull guards off posts) | Pairs at posts the player abused; hounds; the M08 Hunt mission | Multi-route districts (M07), forced Hunt (M08), the bell network | Fluid: Dash-Rise-Leap-drop; escapes are routes she knows | "They should fear me" |
| **Late-Mid** (terror) | M10–M11 | Capstone in the main tree; Signal; Silverblood or Apex or Eclipse or Rapture | Which capstone to rely on; when to go loud | Vigil squads with nets/censers/wards; **the Watch breaks**: Terror morale applies to every watchman in sight of a kill | Gas network, M11 synchronised strike | Commanding: she moves through the city; the city moves out of her way | "I decide who lives" |
| **Late** (legend) | M12–M13 | No new tiers; Preparations (bribed contacts, alternate starts); full 4-slot builds | The final hunt: escape or fight the Vigil | Watchmen flee; the Vigil hunts *her* (Inquisitor squads); **Dawn clock in M13 only** | Lockdown-from-start escape (M13), the cathedral | Precise and fast; failure is now a fight she might win | "Only the Vigil can stop me — and barely" |

**The inversion beat** (§33.5): from M10, watchmen who witness a kill take a morale check. By M12 most break. Only the Vigil holds. The player *feels* the arc in the AI, not the stats.

---

## 27. Enemy evolution (not more HP)

| Stage | Enemy addition | What it does | Player counter |
|---|---|---|---|
| Early | **Watchman** | Cone, lantern on Wary, pairs on posts after a Hunt | Everything |
| Early | **Civilian** | Screams (local), flees to the nearest watchman | Catch before he arrives; Beckon; Mesmerize |
| Early-Mid | **Lamplighter** | Relights snuffed lamps on a route | Exploit as a walking lure; kill him (lamps stay dark, but his absence is noticed at 3 min) |
| Early-Mid | **Vigil hunter** | **Looks up** (checks roofs every 12 s); throws flares at an LKP; silver blade | Time the look-up; stay on the ground near him; take him in a Hunt split |
| Mid | **Hound + handler** | Smells within 5 m (darkness doesn't hide); follows blood trails and Mist | Water (break trail at running water: the Law works *for* her); False Trail; kill the handler (the hound panics) |
| Mid | **Sergeant** | Calls a Hunt without wind-up; pairs within 15 m report to him | Kill or Mesmerize him first; his absence makes pairs wander |
| Late-Mid | **Inquisitor** (cm_inquest) | Reads evidence (stains, dust) as bodies; Mesmerize-immune with a ward | Clean; Communion; avoid; Silverblood ignores his ward |
| Late-Mid | **Censer-bearer** (cm_censer) | Dispels Gloom/Mist within 6 m | Kill him first; pull him off with Beckon |
| Late | **Vigil squad** | 3 hunters: net, flare, musket; not slowed by Apex; doesn't break | Avoid; split; Mist; it's the Hunt's boss |
| Late | **Salt lines / wards** (cm_salt, cm_ward) | Thresholds she can't cross / powers blocked in an area | Route around; Silverblood; thralls |

**Dossier rules** (existing system, new limits):
- From M03 (not M06). Maximum 2 countermeasures active per mission, **always shown on the briefing** with their source ("You fed in the light 9 times").
- A countermeasure decays if the habit isn't repeated for 2 missions.
- **Never more than one countermeasure per tree**, so no single build is shut down.

---

## 28. Feeding: rebuilt as the signature act

**Current:** stand behind, F, 1.6 s (sip) / 3.2 s (drain) stationary channel; any key cancels [Code].

**New feed sequence:**
1. **Grab** (F within 1.8 m of an unaware back, or 2.6 m with a 0.25 s lunge). Instant silence: no shout, no scream. *Seen grabbing in light from 16 m*.
2. **Drag** (move while holding). 1.4 m/s; she faces the victim and backs; **6 s struggle clock** (12 s with Stalker), then the victim breaks free and screams. No climbing, no Dash.
3. **Feed** (release to stop, or **F** = sip, **R** = drain):
   - **Sip:** 1.2 s in darkness / 2.0 s in light. +12 blood. Victim Dazed 45 s, then wakes **Groggy**: reports once, area Wary. With Lethe: wakes with no report.
   - **Drain:** 2.4 s in darkness / 4.0 s in light. +30 blood (blood type modifies). Body left. Notables give Marks.
4. **Interrupt:** any movement key during the feed **pauses** it and resumes dragging. Only Shove (or being shot) breaks it.

| Choice | Why it matters |
|---|---|
| Feed here vs. drag to dark | Light makes the feed 1.7× slower and visible from 16 m; dragging costs struggle time |
| Sip vs. drain | Witness in 45 s vs. a body to hide |
| Who | Blood types (humours) give buffs [Code Archetypes.cs]: choleric (+Dash recharge), sanguine (+regen), melancholic (+sense range), phlegmatic (+max blood). Visible with Hunter's Pulse |
| Overflow | Beyond max, blood heals, then extends Warm. The preview shows `+12 → 8 blood, 4 overflow`: drain now for Warm time, or leave him unharmed? (§29) |
| Who sees it | The disc's watcher ticks show every guard who has the feed in a band, **before** she commits (SR.7) |
| Notables | Marks; their absence is noticed at 2 min (a search starts) |

**Drop-feed** (A2): drop on a human from ≥2 m, landing within 1.5 m: instant grab, the victim knocked down (no struggle for 2 s).

**Darkness rule:** the feed in darkness is faster, unseen outside every near sector (4–7 m, drawn), and silent. The best feed is always in the dark, which is what makes Smother and drag matter.

---

## 29. Blood economy

| Element | Rule |
|---|---|
| **Max** | 100 (125 with Gorge) |
| **Start of mission** | 50 (from full). Abilities cost blood, so the first feed comes early |
| **Income** | Sip 12, drain 30 (±blood type), Communion 10/body, Apex refunds |
| **Passive drain** | **None** (cut in Revision 2). Waiting is not taxed |
| **Warm** (new) | For 90 s after a drain (45 s after a sip): dark regen ×2, Dash recharge ×1.5. A crimson pulse on the disc. The moment to push |
| **Spending** | Abilities 6–40; Mist 3/s; Sense free when still (upgrade) |
| **Emergency reserve** | Below 15: **Starving** (exists: heartbeat audible to humans within 3 m, speed −10%); *new:* Dash recharges 2× faster (desperation) |
| **Overflow** (replaces Gorged) | Blood fed beyond max heals HP first, then extends Warm by 2 s per point (cap 3 min). No speed or noise penalty |
| **Hoarding** | Not penalised. A full tank was paid for in bodies and witnesses. The cap is max blood plus the opportunity cost of feeding |
| **Feeding frequency target** | 1 feed per 3–5 min of play |
| **Combat** | Rend 15, Apex 35: a fight burns 30–50 blood |
| **Utility** | Smother 8, Beckon 6, Mesmerize 12, Snare 15 |
| **Escape** | Dash free (light-limited), Mist 3/s, Gloom 18 |
| **Healing** | In darkness only: 1 HP per 1 blood per 0.5 s while still (Nightblood: while moving). No heal button |

**Design target:** the player is between 30 and 80 most of the time and Warm about a third of the time. Every feed is a resource, a risk and a window.

---

## 30. Levels that support WASD

| Element | Rule (measurable) |
|---|---|
| **Sightlines** | No lit area wider than 20 m without a dark pocket; at least one break in every 15 m of a guarded sightline |
| **Corners** | Corridor corners ≥ 2 m (two cells) so the camera can see round them; corner slip (§10) handles the rest |
| **Cover** | Dark pockets every ≤ 12 m on main routes (≤ 6 m in Act I) |
| **Vertical routes** | Every district has a roof route; climb points within 10 m of every lit hub |
| **Escape routes** | Every lit hub has ≥ 2 dark exits in different directions; a dark loop of 30–60 m near every objective |
| **Occlusion** | No dark pocket fully under a roof that the camera can't cut away; tag every building at map build |
| **Doorways** | 2 m minimum (one cell) for doors used in pursuit; doors push-open (§10) |
| **Movement precision** | No required route narrower than 1 cell; no required jump with < 0.5 m margin |
| **Pursuit spaces** | Each district has one place where a Hunt naturally splits (a 3-way junction, a market) |
| **Ambush points** | Ledges over alleys every 30 m; drop-feed spots marked by a ledge highlight |
| **Loops** | Every guarded objective has a loop she can run while pursued and re-enter the dark |
| **Shortcuts** | Unlocked during play: a gate opened from the inside, a ladder kicked down; persist after death/reload |
| **Readability** | Lights are the only bright elements; climbables show a highlight within 4 m; law gates (water, threshold, salt) have a consistent visual language |
| **Truthful light** (Revision 2) | Every mechanically important light's rendered ground edge lies within 0.5 m of its 0.35 exposure contour. No lamp lights ground that its gameplay light can't reach (occlusion). Checked automatically against the Exposure Field (SR.3) |
| **Light lint** (Revision 2) | Every intended dark route between two lights is ≥ 1.5 m wide at the threshold. No guarded post is unreachable except through a near sector. Lights within 2 m of each other are merged visually. The lint reports each map's dark-route widths |

---
## 31. Encounter archetypes

Twelve reusable archetypes. Each must offer ≥2 tree answers plus a baseline answer, a feed opportunity, and a survivable failure.

### 31.1 Lamplit Checkpoint
- **Composition:** 2 watchmen at a gate + sergeant; lamplighter on a 90 s route.
- **Geometry:** a 6 m gate in a wall; alleys either side; a roof with a downpipe 8 m away.
- **Light:** 2 braziers (0.8) at the gate, dark beyond 6 m.
- **Traversal:** roof route with a 5 m Dash gap.
- **Feeding:** the lamplighter on his dark stretch; a guard drawn off by Beckon.
- **Choices:** through (force), over (route), past (social).
- **Abilities:** Smother draws the lamplighter; Mesmerize; Pounce.
- **Failure:** a Hunt that pours out of the gate. The alleys loop back.
- **Escape:** roofs, the dark loop.
- **Alternatives:** a sewer grate 20 m off (still water).

### 31.2 Patrol Courtyard
- **Composition:** 4 watchmen on 2 interlocking loops with a 6–10 s gap; 1 static.
- **Geometry:** 30 × 30 m courtyard; colonnade; well; 3 doors.
- **Light:** lanterns on columns; dark colonnade.
- **Traversal:** colonnade roof (climb).
- **Feeding:** shadowing a walker into the colonnade's dark end.
- **Choices:** follow a loop and take one; cross in a gap; split them with Rattle.
- **Abilities:** Shadowing, Dash, Snare at the crossing.
- **Failure:** courtyard Hunt; the 3 doors split pursuers.
- **Escape:** colonnade roof; doors push-open.
- **Alternatives:** wait (not rewarded: the blood clock).

### 31.3 Crowd (masquerade, opera, market)
- **Composition:** 20+ civilians, 4 watchmen, 1 Vigil hunter disguised.
- **Geometry:** an open hall with alcoves and a balcony.
- **Light:** bright; alcoves dark.
- **Traversal:** balcony via a servant stair.
- **Feeding:** Beckon a guest to an alcove; sip (no body).
- **Choices:** feed on the notable (Marks, search at 2 min) vs. commoners.
- **Abilities:** Mesmerize, Familiar Voice, Lethe; Rapture.
- **Failure:** a scream is local, panic causes a stampede (cover to escape).
- **Escape:** servant stair, balcony window.
- **Alternatives:** thrall a servant.

### 31.4 Hound Yard
- **Composition:** 2 handlers + 2 hounds; yard keeper.
- **Geometry:** fenced yard, kennels, a millrace on one side (running water).
- **Light:** dim yard, kennel lamp.
- **Traversal:** fence (Between Bars), roof of kennels.
- **Feeding:** a handler on his round.
- **Choices:** kill the handler (hound panics) vs. use the millrace to break scent.
- **Abilities:** False Trail, Snare, Pounce.
- **Failure:** hounds track through darkness: the water is the escape.
- **Escape:** cross at the footbridge then the water breaks scent.
- **Alternatives:** Mist (hounds smell it: wrong tool).

### 31.5 Rooftop Sentry Line
- **Composition:** 3 Vigil look-up hunters on street corners; rooftop crossbow sentry.
- **Geometry:** a roof route with 4–6 m gaps.
- **Light:** street lamps below; moonlit roofs (0.3).
- **Traversal:** Dash, Leap, Rise.
- **Feeding:** drop-feed on a hunter whose look-up cycle you've read.
- **Choices:** roof (fast, watched) vs. street (slow, many people).
- **Abilities:** Dash chains; Gloom on a gap.
- **Failure:** flares on the roof; drop to the street to break.
- **Escape:** downpipes every 20 m.
- **Alternatives:** sewers.

### 31.6 Sleeping House (threshold)
- **Composition:** a family asleep; 1 servant awake; a watchman outside.
- **Geometry:** 2-storey house; front door, back door, an upstairs window.
- **Light:** a single candle; the rest dark.
- **Traversal:** window (only with invitation).
- **Feeding:** sleepers (sip: no wake if Lethe).
- **Choices:** get an invitation (Beckon the servant to the door; Mesmerize "invite me"; a thrall); or never enter (the objective may be outside).
- **Abilities:** Mesmerize, Enthrall, Silverblood (ignores).
- **Failure:** a scream wakes the house; the watchman comes; thresholds keep the Watch out without an officer.
- **Escape:** back door.
- **Alternatives:** draw the objective out (Beckon the target).

### 31.7 Crossing (running water)
- **Composition:** bridge guards; ferryman; a mill.
- **Geometry:** a leat or river; 1 bridge, 1 ferry, mill wheel, roofs.
- **Light:** lit bridge; dark water.
- **Traversal:** no Dash/Mist over water.
- **Feeding:** the ferryman (alone).
- **Choices:** bridge (force), roofs+mill (route), thrall the ferryman (social).
- **Abilities:** see §25.
- **Failure:** the water blocks *her* escape too: Hunts here are dangerous by design.
- **Escape:** back the way she came.
- **Alternatives:** wait for the ferry's schedule.

### 31.8 Inquisitor's Cordon
- **Composition:** inquisitor + censer-bearer + 3 Vigil; salt lines.
- **Geometry:** a plaza with a salt circle around an objective.
- **Light:** censer braziers.
- **Traversal:** salt blocks her; thralls can scatter it.
- **Feeding:** none inside; a Vigil straggler outside.
- **Choices:** break the salt (thrall, Silverblood) vs. bring the objective out.
- **Abilities:** Enthrall, Silverblood, Hemorrhage on the censer-bearer.
- **Failure:** the Vigil squad (§27).
- **Escape:** Mist is dispelled: run.
- **Alternatives:** a bell decoy in another quarter pulls 2 Vigil.

### 31.9 The Hunt (M08)
- **Composition:** the Watch looking for her; pairs sweeping; hounds.
- **Geometry:** a district with loops.
- **Light:** lanterns moving.
- **Traversal:** roofs, sewers.
- **Feeding:** stragglers from pairs.
- **Choices:** hide out the clock vs. thin the search.
- **Abilities:** everything.
- **Failure:** cornered: fight or Mist.
- **Escape:** the objective is the exit.
- **Alternatives:** —

### 31.10 Lockdown Escape (M13)
- **Composition:** every guard Wary; the Vigil; the Dawn clock.
- **Geometry:** a long route to the crypt.
- **Light:** everything relit.
- **Traversal:** all.
- **Feeding:** necessary, because start blood is 30.
- **Choices:** fast loud route vs. slow clean route under the clock.
- **Abilities:** capstones.
- **Failure:** dawn.
- **Escape:** the mission is an escape.
- **Alternatives:** —

### 31.11 Gas Network (M10)
- **Composition:** gas lamps on 3 valve groups; lamplighters.
- **Geometry:** a district with a gasworks.
- **Light:** bright gas light.
- **Traversal:** valve rooms (interiors).
- **Feeding:** the valve keeper.
- **Choices:** kill a whole group (Black Main, valve) vs. snuff singly.
- **Abilities:** Smother chain.
- **Failure:** a dark group makes the Watch Wary across it.
- **Escape:** darkened streets.
- **Alternatives:** —

### 31.12 Lair Ambush
- **Composition:** Vigil hunters entering her refuge.
- **Geometry:** a crypt with pillars and alcoves.
- **Light:** they bring it (lanterns, flares).
- **Traversal:** ceiling-free; ledges.
- **Feeding:** them.
- **Choices:** pick them off vs. flee.
- **Abilities:** all.
- **Failure:** nets.
- **Escape:** secret passage.
- **Alternatives:** —

---

## 32. Creative interactions (system × system)

| A × B | Interaction |
|---|---|
| Blood × hounds | Blood trails from wounds or drags are followed by hounds; Clean erases them |
| Feeding × light | Faster in dark; visible from 16 m in light |
| Light × lamplighters | Snuffed lamps call a lamplighter, a walking, predictable victim |
| Bodies × AI | A found body starts a search; 3 bodies (2 with cm_paired) start Lockdown; a Rended body counts double |
| Bodies × water | A body dropped in running water is carried away (no evidence) |
| Domination × thresholds | A Mesmerized resident can invite her in |
| Domination × Hunts | A thrall in a Hunt "points the wrong way": Hunt LKP moves to his point |
| Traversal × AI | Vigil look-up cycles; watchmen never look up |
| Environment × noise | Rattle objects; bell-pulls; gravel/glass/water make Sneak audible |
| Civilians × alarms | A screaming civilian runs to the nearest watchman; catching him first prevents the Hunt |
| Hunters × Dossier | Hunters bring the countermeasures for the player's habits |
| Doors × noise | Push-open: sneak = silent, walk = creak, run = bang |
| Windows × thresholds | Windows into homes need an invitation; others are climb links |
| Darkness × Dash | Dash recharges only in darkness |
| Terror × groups | A lone watchman who sees a kill flees; a pair holds; a sergeant steadies them |
| Overflow × darkness | Overflow extends Warm, and Warm doubles dark regen: a full predator heals fast in the dark (Revision 2; replaces Gorged × noise) |
| Snare × Beckon | Lure onto a rune |
| Gloom × curiosity | Guards walk into the Gloom to look |
| Hemorrhage × panic | A found pool makes witnesses within 12 m panic, not alarm |
| Hunts × posts | A Hunt pulls guards off posts: a deliberate distraction for the bold |
| Running water × hounds | Crossing water breaks scent |

**Rule:** each interaction must be **shown once** in a controlled spot before the player needs it (§30 readability).

---

## 33. Signature systems

### 33.1 The Hunt
- **Why special:** failure is play. Few stealth games do pursuit well; most go binary [Research].
- **WASD fit:** escape is steering, which only direct control delivers.
- **Frequency:** 0–1 per mission for careful players, 1–2 typical. More than one per 10 min is a level or readability bug (§20.4).
- **Progression:** Act I Hunts end fast; Act III adds hounds, flares and the Vigil; late Predators can *win* Hunts.
- **Enemy response:** pairs on posts, relit lamps, Dossier weight.
- **Level showcase:** M08 (built around a Hunt).

### 33.2 Light is Hunger
- **Why special:** darkness is the *cooldown* (Dash), the *feed speed*, the *healing*, and the *hiding*. One resource ties all four.
- **WASD fit:** reading the floor as you steer. **Revision 2:** the floor must be true. The Exposure Field (SR.3) drives the rendered pools, rims, cone fills and the disc, from the same numbers as `LightAt`.
- **Frequency:** constant.
- **Progression:** Smother → Black Main → Eclipse (every pool visibly shrinks); enemies with lanterns, flares, censers (moving rims).
- **Enemy response:** lamplighters, cm_censer.
- **Level showcase:** M10 (gas network).

### 33.3 Vampire Laws as the level contract
- **Why special:** gates aren't locked doors; they're folklore rules (running water, thresholds, holy ground, salt) with multiple answers.
- **WASD fit:** routes are physical.
- **Frequency:** 1–2 law gates per mission.
- **Progression:** learn the law (M03 threshold, M04 water), then exploit it (water breaks hound scent; thresholds stop the Watch), then break it (Silverblood).
- **Enemy response:** cm_salt, cm_ward.
- **Level showcase:** M07 (crossings).

### 33.4 The Vigil Learns (the Dossier)
- **Why special:** the hunters adapt to *your* habits, visibly. MGSV's Revenge works when visible and capped [Research].
- **WASD fit:** neutral.
- **Frequency:** every mission from M03; max 2 active.
- **Progression:** countermeasures escalate per tier.
- **Enemy response:** it is the enemy response.
- **Level showcase:** the briefing screen ("You fed in the light 9 times: tonight, the Vigil carries flares").

### 33.5 Terror: the inversion
- **Why special:** the end of the arc is in the AI, not a stat screen. Watchmen flee her; only the Vigil holds.
- **WASD fit:** the player walks into a street and watches it clear.
- **Frequency:** M10+.
- **Progression:** Terror node early for Predators; the whole Watch from M10 as her legend grows (a story beat).
- **Enemy response:** the Vigil becomes the only real opposition.
- **Level showcase:** M12.

---

## 34. Don't just add features

Every addition in this document either replaces a weaker system or solves a named problem:

| Addition | Replaces / solves |
|---|---|
| The Hunt | Binary alarm (P1, P3, P5) |
| Shadow Dash | Shadowstep (P6) and the missing early verb (P4) |
| Grab-drag-feed | Stationary channel (P13) |
| Shove | No early recovery (P14) |
| Reaction Window | Instant shout (P3) |
| Soft-target quick-cast | Hover aim and auto-walk (P7) |
| Rattle | Waiting (§19) |
| Thrall Signal | The removed Nightplan's one real use (§14) |
| Warm blood / Overflow | Passive drain and Gorged (Revision 2) |
| Stealth Readability layer (Exposure Field, contextual cones, disc, step preview, Spotted Explanation) | Alt-only cones, the 3-state eye, the decorative light radius, unexplained detection (P10, P15, P16) |
| Preparations | Mastery unlocks; the pre-placed thrall (P8) |

**Removals that pay for them:**
- Shadowstep, Bloodmend, Corpse Puppet and Living Lie.
- Court, Scent, Herding and Soft Landing (merged).
- 2 ability slots, auto-walk and the M06 Dossier start.
- *Revision 2:* passive drain, Gorged penalties, the Shadowing speed-match, the objective lockout, the DARK/SHADOW split of the light eye, and the Alt-only cone display.

---

# RE-EVALUATED MECHANICS (Revision 2, §RE)

Five mechanics from Revision 1 were questioned. Each was tested against two things:
- the WASD contract (one movement authority, no taxes on moving well);
- whether it improves decisions or only adds rules.

Each one keeps its *purpose*. Most lose their original *mechanism*.

| Mechanic (Revision 1) | Verdict | Replacement |
|---|---|---|
| Passive blood drain above 50 | **Cut** | **Warm blood**: a reward for feeding, not a tax for waiting |
| Gorged: −10% speed and louder runs above 90 | **Cut the penalties; keep the idea that over-full blood does something** | **Overflow**: blood beyond max heals, then extends Warm |
| Shadowing auto speed-match | **Cut the speed-match; keep the verb** | **Footfall masking** plus readable stalking |
| Objective interactions refused during a Hunt | **Cut** | Consequences by objective type |
| Mastery unlocks (start points, pre-placed thralls) | **Reframed** | **Preparations**: diegetic, chosen at the briefing |

## Passive blood drain: CUT

| Question | Answer |
|---|---|
| What was it for? | Stop hoarding, and push the player to spend and feed |
| What does it actually do? | It taxes **waiting**, and the cautious player waits the most. Genre research puts that player at the core of the audience [Research]. They would lose blood for watching a patrol, which punishes the *reading* the game asks for |
| Is it readable? | No. One blood per 10 s is invisible until it shows up as a surprise |
| Does it create decisions? | No. It creates a clock, and the Hunt budget (§20.4) already needs the player's attention |
| What already does its job? | **Start at 50**. Abilities cost blood. Dark regen spends blood to heal. **Starving** at 15 stays (an audible heartbeat): the floor already pushes feeding |

**Replacement: Warm blood** (an opportunity after feeding)
- For **90 s after a feed**: dark regen ×2 and Dash recharge ×1.5.
- The disc shows a faint crimson pulse (SR.7).
- A drain gives the full 90 s; a sip gives 45 s.
- The decision moves from "spend before the drain eats it" to "**I'm Warm: this is the moment to push**". That is a predator's rhythm: feed, then act.

## Gorged penalties: CUT, replaced by Overflow

| Question | Answer |
|---|---|
| What was it for? | The top end of a "sweet zone", to make hoarding cost |
| What does it actually do? | **−10% speed taxes the WASD feel** precisely when the player has played well. The louder run and the "glow in light" are near-invisible rules |
| Does hoarding need a cost? | Without passive drain, hoarding means "I fed a lot". Every feed already cost a body or a witness. A full tank is earned |

**Replacement: Overflow**
- Blood fed beyond max first **heals HP**.
- Then it **extends Warm** by 2 s per point, up to 3 min.
- The feed preview shows `+12 → 8 blood, 4 overflow`.
- The decision becomes: **drain this one now for Warm time I may not need, or leave him and keep the body count down?**
- The Gorge node (+25% max blood) stays. It now means "more room before Overflow".

## Shadowing speed-match: CUT; Shadowing becomes assistance

| Question | Answer |
|---|---|
| What was it for? | Tailing a patrol, the vampire fantasy |
| What's wrong with it? | **Revision 1's premise was wrong.** It claimed sneak (1.75 m/s) couldn't keep up with a walking guard. Guards walk at **1.6 m/s** (1.76 Wary) [Code `Npc.WalkSpeed`]. Sneak already keeps up. More importantly, the speed-match is **a second movement authority**: the game sets her speed. That is R3 again. It also hides the real risk: if he stops, she runs into his touch zone |

**Replacement**
1. **Sneak at 2.2 m/s** (kept from §10). That gives a closing margin of 0.6 m/s on a walking guard and 0.4 on a Wary one, so the *player* sets the gap.
2. **Footfall masking.** Her walk-gait steps make no noise within 3 m behind a walking, unaware human: his own steps cover hers. A player can close the last metres at walk speed (3.4) without the 3.2 m noise ring. The ring is drawn *suppressed* (a faint crimson ring that doesn't expand), so the rule is visible.
3. **The rules of stalking are drawn, not automated:**
   - his touch circle (1.4 m);
   - his near sector (which never covers behind him);
   - the toe (SR.8), which flashes if he turns or stops and her next step enters it.
4. **Stalker** (Predator) **removes the touch zone behind him**, beyond 100° from his facing. It already does this in code (`predator.stalker`, d < 4). It also adds feed speed.

**Effect.** Tailing is a WASD skill: match the pace by hand, watch the gap, and close when the street goes dark. The game never moves her.

## Objective lockout during a Hunt: CUT

| Question | Answer |
|---|---|
| What was it for? | Stopping "spotted, then walk to the objective" in Act I. The target's door was a safe zone |
| What's wrong with it? | It is a **rule-based refusal**. "Not with them on your heels" stops the player's hands, which is the one thing a WASD game must not do. It is also arbitrary: why can't she steal a ledger with a guard 18 m away? |
| What was the real problem? | **Indoors was safe** (watchmen stopped at doors), and **alarms cost nothing** (rewards ignored them) |

**Replacement: consequences by type**

| Objective type | Hunt consequence |
|---|---|
| **Target (feed or kill)** | The target hears the commotion and **flees to a safe room** with an escort. The objective becomes a chase or an ambush, not a refusal |
| **Theft** | Taken in view of a hunter, it marks her: **exits in that quarter lock down** for 60 s, and she must go up or through |
| **Reach (an indoor point)** | **Pursuers follow her inside.** Doors stop being Hunt-proof. This fixes the "door = safe zone" bug at its root |
| **Any** | "Unbroken" and its challenge are lost; Dossier weight increases |

Only *natural* interruption remains. Being hit cancels a channel (feeding or Enthrall), as it does now.

## Mastery unlocks: reframed as Preparations

**Revision 1's version:** three challenges in a mission unlock an alternative start, or a pre-placed thrall, in that mission.

**The objection.** A thrall that simply "exists" when the level loads is a game convenience with no fiction. Ilse doesn't have thralls waiting around the city.

**Replacement: Preparations.**
- Preparations are **chosen at the briefing** (1 on a first play, 2 after mastery) and **shown on the briefing map**.
- Each one is **one-use and diegetic**.
- Completing challenges unlocks new Preparations for that mission. Some are bought with Vitae.

| Preparation | Fiction | Effect |
|---|---|---|
| **A bribed servant** | "The scullery maid owes you." | One service door starts unbarred |
| **A contact's key** | "Brother Anselm left it in the drainpipe." | One locked door, or an alternative start inside the walls |
| **A compromised guard** | "Sergeant Voss has debts." | He leaves his post once, on **Signal** (T + Space), for 30 s. This replaces the pre-placed thrall |
| **A forged invitation** | "You're on the guest list." | In crowd missions (M06, M11): one checkpoint passes her once |
| **A sabotaged gas valve** | "The lamplighter's apprentice was paid." | One lamp group starts dark (M10, M05) |
| **A paid ferryman** | "The ferry waits at the third bell." | One running-water crossing at a set time (M04, M07) |
| **A rumour planted** | "Word is, the Vigil hunts the docks tonight." | One Vigil patrol is moved to a named quarter |

**Why it is better:**
- It rewards mastery with **options**, which is the Hitman lesson [Research].
- It **fits the fiction** of a vampire with a mortal network.
- It makes the briefing a decision.
- It replaces a passive unlock with a choice that shapes the route before the mission.

---

# CUT CANDIDATES

| Candidate | Current purpose | Why it fails | Redesign or remove | Replacement |
|---|---|---|---|---|
| **Shadowstep (point teleport)** | Mobility; Shade identity | Cursor aim conflicts with WASD; deletes topology; the genre's #1 trivialiser [Research] | **Remove** | Shadow Dash (baseline) + Between Bars/Umbral Step |
| **Auto-walk into range** | Click-era convenience | Two movement authorities; walks her into cones | **Remove** | Grey-out + distance; grab lunge; magnetism |
| **Two-stage aims** | Precise lure points | Stops her; two clicks under pressure | **Redesign** | One-stage (tap = to me, hold = point) |
| **Bloodmend** | Heal | Removes the darkness-regen decision | **Remove** | Dark regen paid in blood; Nightblood |
| **Corpse Puppet** | Move a body uncannily | Unused; overlaps Enthrall and Carry | **Remove** | Communion (evidence), Enthrall |
| **Living Lie** | A social pass | One-case passive | **Remove** | Familiar Voice, Lethe |
| **Court** | Mass charm | Overlaps Mesmerize | **Combine** | Rapture (Mesmerize capstone) |
| **Scent / Herding / Soft Landing** | Small passives | Node padding | **Combine** | Into Hunter's Pulse / Terror / Pounce |
| **Slots 5–6** | More actives | Out of reach of WASD; no build choice | **Remove** | 4-slot loadout + baseline kit |
| **Arts locked until M03** | Story "fledgling" beat | 40 min without powers (P4) | **Remove** | First gift at M01 end |
| **Free quicksave in one slot** | Convenience | Savescum loop | **Redesign** | Timer, 3 slots, no save while seen, Ironblood |
| **Global integer alarm** | Escalation | No middle (R1) | **Redesign** | The Hunt |
| **Dossier from M06, unlimited** | Adaptation | Too late, then too much | **Redesign** | From M03, max 2, decays |
| **Passive drain** (Revision 1) | Anti-hoarding | Taxes the cautious player; invisible | **Remove** | Warm blood |
| **Gorged penalties** (Revision 1) | Anti-hoarding | A speed tax on success | **Remove** | Overflow |
| **Shadowing speed-match** (Revision 1) | Tailing | A second movement authority; the premise was wrong (guards walk 1.6) | **Remove** | Sneak 2.2 + footfall masking + drawn touch circle |
| **Objective lockout in a Hunt** (Revision 1) | Act I consequence | Refuses the player's hands | **Remove** | Targets flee, pursuers follow indoors, exits lock |
| **Alt-only cones** | Clean screen | A WASD player can't hold Alt and steer | **Redesign** | Contextual cones; Alt becomes Tactical (SR.12) |
| **3-state light eye** | Light read | DARK and SHADOW behave identically; it sits in a corner | **Redesign** | Two-state ground disc; the eye stays as backup (SR.7) |

---

# MAJOR GAMEPLAY CHANGES WORTH CONSIDERING

### The Hunt
- **What changes:** detection becomes Spotted (wind-up) → local Hunt → Lost/Lockdown; pursuers in pairs; persistent marks.
- **Why research or playtesting supports it:** "detected = reload" is the genre's most common complaint (Styx, Shadow Tactics reviews); Invisible Inc shows survivable escalation is loved; our own runs show both extremes (7 spottings free in M02, death in 10 s in M07) [Played, Research].
- **Player benefit:** mistakes become play; fewer reloads; more stories.
- **Downsides:** AI search tuning; risk of weightless escapes.
- **Implementation size:** Large.
- **Would you actually do it?** **Yes.**

### Shadow Dash replaces Shadowstep
- **What changes:** teleport out, navmesh dash in, baseline from M01.
- **Why:** Blink/teleport trivialisation (Dishonored, Aragami, Ereban); cursor aim conflicts with WASD; Aragami's light-cost tension was loved [Research].
- **Player benefit:** a body verb from minute one; topology holds.
- **Downsides:** loses the teleport fantasy; re-tunes Shade.
- **Size:** Medium.
- **Would you actually do it?** **Yes.**

### Grab-drag-feed
- **What changes:** feed becomes grab → drag → sip/drain.
- **Why:** Bloodlines' feeding had no risk [Research]; our feed is a stationary refill [Code].
- **Player benefit:** the signature act is a skill.
- **Downsides:** animation work; balance of drag.
- **Size:** Medium.
- **Would you actually do it?** **Yes.**

### Reaction Window
- **What changes:** slow-mo + wind-up on first detection.
- **Why:** Shadow Tactics' detection slow-mo is praised for readability [Research]; `Game.SlowMo` exists [Code].
- **Player benefit:** a chance to fix mistakes with skill.
- **Downsides:** may feel like a crutch.
- **Size:** Small–Medium.
- **Would you actually do it?** **Yes.**

### Stealth Readability layer (Revision 2)
- **What changes:**
  - The Exposure Field (SR.3).
  - Cones redrawn as near sector, light-clipped far fill, grace and touch circle.
  - Contextual cones in normal play.
  - Ilse's ground disc with watcher ticks and a step preview.
  - The Spotted Explanation, and a Detections debrief.
- **Why research or playtesting supports it:**
  - Shadow Tactics draws a solid "seen" zone and a striped "seen if standing" zone in one cone [Genre precedent].
  - Mark of the Ninja draws sound and makes the character's own look show lit or unlit [Research].
  - Our code shows the rules are exact but hidden [Code].
  - Our runs show deaths at 1–5 m in darkness that players can't explain [Played].
- **Player benefit:** decisions instead of guesses; detections that teach.
- **Downsides:** clutter risk; art time; a risk of making stealth easier (mitigated by local, truthful information).
- **Implementation size:** Medium (spread over 9 steps, SR.14).
- **Would you actually do it?** **Yes, before tuning the Hunt.**

### Truthful light rendering (Revision 2)
- **What changes:**
  - Unity lights re-authored to end at the exposure contour, with a visible knee.
  - Occlusion cookies for unshadowed lamps.
  - Thin exposure rims within 10 m of Ilse.
- **Why:** the visible pool is about 1.5× the gameplay pool, and spills through walls [Code].
- **Player benefit:** "where does this lamp stop exposing me?" is answered by looking.
- **Downsides:** the city may look darker; art iteration.
- **Size:** Medium.
- **Would you actually do it?** **Yes.**

### Act I restructure (body kit + first gift)
- **What changes:** baseline Dash/grab/Beckon/drop-feed; a first-gift choice at M01 end.
- **Why:** players decide in the first hour (demo, refund window); the genre's best openings give the signature verb immediately [Research].
- **Player benefit:** the fantasy at minute one.
- **Downsides:** re-tune M01–M03.
- **Size:** Medium.
- **Would you actually do it?** **Yes.**

### Playstyle-paid progression
- **What changes:** challenges pay Marks; **Preparations** (diegetic one-use briefing options: bribed servants, keys, a compromised guard, a sabotaged gas valve) replace mastery unlocks; flattened Vitae.
- **Why:** Hitman unlocks [Research]; our sim shows Ghost trails Predator by 2 Awakenings [Sim]; Preparations fit the fiction where a pre-placed thrall didn't.
- **Player benefit:** every style progresses; replays matter.
- **Downsides:** economy retune.
- **Size:** Medium.
- **Would you actually do it?** **Yes.**

### Dawn clock (M13 only)
- **What changes:** a timer only in the escape mission.
- **Why:** timers everywhere increase frustration [Research: genre reviews]; one finale timer is drama.
- **Size:** Small.
- **Would you actually do it?** **Yes.**

### Planning mode (Shadow Mode)
- **What changes:** re-add a pause-and-queue mode.
- **Why:** Shadow Tactics' signature [Research].
- **Downsides:** built and cut after play-test (D115); fights direct control.
- **Size:** Large.
- **Would you actually do it?** **No.** Thrall Signal covers the one use case.

### Third-person camera
- **What changes:** over-the-shoulder.
- **Why:** direct control often pairs with it.
- **Downsides:** loses cone reading; rebuild of every level's readability.
- **Size:** Fundamental.
- **Would you actually do it?** **No.**

### Procedural patrols / systemic districts
- **What changes:** generated routes.
- **Why:** replay.
- **Downsides:** loses authored puzzles; huge.
- **Size:** Fundamental.
- **Would you actually do it?** **No.**

### Bat form
- **Downsides:** deletes verticality, needs flight nav, flies over every law.
- **Would you actually do it?** **No.**

### Parry/combo combat
- **Downsides:** Styx's combat was the most criticised part [Research]; wrong game.
- **Would you actually do it?** **No.**

### Cut missions (13 → 9)
- **Why:** scope.
- **Downsides:** the missions are good; the systems are the problem.
- **Would you actually do it?** **No.**

### Remove quicksave
- **Downsides:** genre players expect it [Research].
- **Would you actually do it?** **No** (opt-in Ironblood instead).

---
## 37. Industry expectations

## MUST MEET INDUSTRY EXPECTATIONS
| Expectation | Current | Action |
|---|---|---|
| Visible vision cones and inspect on demand | Partly: only on Alt or inspect; the near sector is not distinct; the touch zone is not drawn | Contextual cones, pins, Alt as Tactical (SR.5, SR.6, SR.12) |
| Clear detection feedback (meter, audio, slow-mo) | Meter on screen only; slow-mo no | Reaction Window; meters on the disc and off-screen pips (SR.9) |
| **Detection explains itself** | **No** | The Spotted Explanation and the debrief Detections page (SR.10) |
| **Light the player can read** (the light gem standard) | Corner eye with a cosmetic tier; the rendered pool is 1.5× the real one | Ground disc; truthful light; rims (SR.4, SR.7) |
| Local, readable alarms | **No** (bug) | P1 |
| Save timer and multiple quicksave slots | No | P11 |
| Camera that never loses the character | No occlusion | P12 |
| Rebindable keys, full pad support | Partial | Soft targets make pad possible |
| Difficulty settings that change perception, not HP | Yes (Merciful/Hunter/Apex) | Keep |
| Multiple solutions per objective | Mostly | Law-gate rule (§30) |
| Body hiding and discovery | Yes | Keep |
| Non-lethal option | Yes (sip, Mesmerize) | Keep; fix Bug B |
| Accessibility: hold/toggle options, slow aim | No | Aim slow-mo option |
| Demo with the core fantasy in 15 min | No (powers at 40 min) | P4 |

## SHOULD EXCEED INDUSTRY EXPECTATIONS
| Area | How |
|---|---|
| **Readable at WASD speed** | Every rule drawn from the AI's own numbers, contextually, under the player's eyes; a step preview; a one-line reason for every detection (STEALTH READABILITY) |
| **Failure as play** | The Hunt with Reaction Window, split pursuers, persistent marks, and a budget that keeps it rare (§20.4) |
| **Body-based stealth with direct control** | Shadowing, grab-drag, drop-feed, Dash, corner slip, push doors |
| **Feeding as the signature act** | Grab → drag → sip/drain with light, struggle and evidence |
| **Enemies that learn** | Dossier, visible and capped |
| **Folklore as level design** | Vampire Laws with multiple answers and late law-breaking |
| **The arc in the AI** | Terror inversion: the Watch breaks, the Vigil holds |

---

## 38. Example gameplay

### 38.1 Early (M02, minute 3)
> Ilse crouches (**Ctrl**) in the dark doorway of the tannery. A watchman walks past, lantern low. She holds **Ctrl+W** and slides in behind him; she gains on him, so she eases off by hand. His bone cone sweeps ahead of him. The touch circle at his heels is the only thing she has to stay out of. As the street goes dark, she lets go of Ctrl: **walk**, three quick steps, the noise ring faint and **masked** by his own footfalls. **F**: the grab, his cry choked. She holds **S** and drags him backwards three metres into the doorway, the struggle bar draining, and presses **F** again: a sip, 1.2 s in the dark. He slumps, dazed; a faint crimson ring shows 45 s. She **G**-carries him behind the barrels. A second watchman's lantern swings into the street. She has two Dash pips, both full. She doesn't need them yet.

### 38.2 Mid (M07, minute 18, Shade)
> Rooftop. The Vigil hunter below looks up on his 12 s cycle; his cone tilts, sweeps the ridge, drops. Ilse **Q**-dashes the 5 m gap to the mill roof. One pip empties; it refills only in the dark, and the mill roof is moonlit (0.3), so it does. The bridge braziers burn below. She taps **1** with the cursor on the north brazier: **Smother**, out. The sergeant swears; the lamplighter starts walking. She toggles **Mist** (slot 3), steers the fog down the wall and through the vent, crosses the dark mill floor, out under the north door. Three blood a second. Out. The pips refill.

### 38.3 Late (M12, Predator)
> The square is lit; four watchmen and a Vigil squad. Ilse walks in. The nearest watchman sees her, and the **Reaction Window** doesn't fire. He doesn't shout; he backs away, pale (**Terror**, the Watch breaks). His partner raises his pike. **Q-dash**, **F** grab, **R** drain in full light: 4 s, everyone sees. Two watchmen run. The Vigil squad doesn't. The net-thrower winds up; she Dashes left through the telegraph line, **4** for **Apex**. The world slows; the Vigil doesn't. She leaves.

### 38.4 Failed stealth (M05)
> She misjudges the cone edge: the meter fills. The world drops to 30%; a red ring grows at the guard's head; "There! By the—". The solid arc of his near sector flashes a step behind her, and a line of text hangs by him: *Near band: 4.1 m (his limit is 6 m).* She cut the corner by two metres. Too far to grab. She holds **D**, around the corner, out of his near band. The ring collapses: he's Investigating, not Hunting. He walks to where she was. She waits in the dark at the next corner. He arrives alone. **F.**
>
> Ten minutes later she isn't so lucky: two of them, the second shouts at once. **Hunt.** The red outline forms; the ghost marks her last position. She runs (**Shift**) for the dark loop behind the chapel, **Q** through the alley mouth, up the downpipe (push **W**). Pairs split to search points. 4 s out of sight, 10 m from the ghost, in darkness: "They've lost you." The street lamps relight; the chapel quarter is Wary. Later, a sweeper passes beneath her ledge, alone. She drops.

### 38.5 Two builds, one space (M07 bridge): see §25.

---

## 39. Mental playtests

| Player | What they'll do | Breaks? | Fix |
|---|---|---|---|
| **Cautious** | Watch every cycle; sip everything; never run | Waiting returns. *(Revision 1's passive drain would have punished this player for reading.)* | No blood tax for waiting. Starting at 50 and ability costs bring the first feed. Warm blood rewards acting after a feed. Routine stops and Rattle objects give openings; challenges and Preparations pay; contextual cones let them read *while* moving |
| **Aggressive** | Pounce/Rend everything; Hunt every room | Too strong if Rend is cheap; Lockdowns every mission | Rend 15 blood; 3 bodies = Lockdown; Vigil in Act III isn't scared; refunds capped |
| **Optimiser** | Find the cheapest loop: Beckon-step-grab | Beckon-chain dominance | Pairs don't come alone; returning Beckoned make Wary; Dossier counter "Familiar voice" |
| **Experimental** | Combine: Snare + Gloom + Beckon; thrall in a Hunt | Interactions they expect may not exist | §32 interaction table, each shown once |
| **Abuse: drag everything** | Pile bodies in one spot | Discovery = massacre | Bodies found together count ×2 |
| **Abuse: Mist everywhere** | Infinite safety | 3/s cost; hounds; censers | Already limited |
| **Abuse: dark camping in a Hunt** | Wait it out | — | Flares at the LKP; sweeps pass within 3 m |
| **Abuse: Hunt farming** | Trigger Hunts to pull guards off posts, then loot | A deliberate diversion | Allowed but expensive: routines cancel, notables hole up, Wary, Dossier "Bold" posts watchers at objectives next mission; no Marks from Hunt kills (§20.4) |
| **Information abuse** | Read every cone and rim, so stealth is "solved" | Too easy? | Information is truthful but **local** (Normal shows about 2.5 s ahead). Patrols, timing and execution are still the game. Apex trims Normal (SR.12) |
| **Abuse: Dash in light** | Escape anything | Pips don't recharge in light | By design |
| **Repetition (mission 6)** | Same opener every room | Feels samey | Enemy mix changes per act; Dossier adapts; law gates |
| **10 hours later** | Mastery: Dash-Rise-drop combos | Late-game too easy for Predators | Vigil immune to Apex slow; Vigil squads; Terror makes the Watch irrelevant *by design*, the Vigil is the game |
| **WASD at 10 hours** | Fluid routes, corner slip | Camera snaps disorient | Locked frame during rotation; 45° snaps |

---

# QUICK WINS
Each is ≤ 1 day, safe, and improves play immediately.

| # | Change | Why | Size |
|---|---|---|---|
| 1 | **Fix `Shout` radius** (`15f * ShoutRadius` → `ShoutRadius`) and HearNoise scaling | Bug A (P1) | Hours |
| 2 | **Fix the Dazed tick** (leave Dazed before routing; report once) | Bug B (P2) | Hours |
| 3 | **Add the water check to `Cast`** for every movement ability | Latent bug (P6) | Hours |
| 4 | **Open the Arts after M01**; grant Beckon at M02 | P4 | Hours |
| 5 | **Remove player auto-walk**; grey out-of-range targets with the distance | P7 | Hours–1 day |
| 6 | **Sneak 1.75 → 2.2 m/s** | A closing margin on walking guards (1.6 m/s) | Minutes |
| 7 | **Draw the shout ring, the near *sector* as a distinct solid fill, and the 1.4 m touch circle** | P1, P10 | 1 day |
| 8 | **Save timer + 3 rotating slots** | P11 | 1 day |
| 9 | **Challenges pay +1 Mark first time** | P8 | Hours |
| 10 | **"Unbroken" = no Hunt/alarm** (fix the award in Act I) | P5 | Hours |
| 11 | **Camera default distance 20; tap Z/X = 45° snap; lock WASD frame while held** | P12 | 1 day |
| 12 | **Velocity look-ahead** | P12 | Hours |
| 13 | **Dossier from M03, max 2, shown on briefing** | §27 | 1 day |
| 14 | **Reset dev Dossiers** after Bug B | Data hygiene | Minutes (ask first: saves are outside the project) |
| 15 | **Contextual cones** using the existing ConeRenderer: show a guard's cone when Ilse is within 4 m of it, or he is aware; hysteresis 1.5 s; max 4 | P16 | 1 day |
| 16 | **Exposure rim** at the 0.35 contour for lights within 10 m (copy `BuildBurnRing`; solve the radius per light from `Intensity`, `Ambient` and height) | P15 | 1 day |
| 17 | **Clamp Unity light ranges toward the contour** (numbers only in `GameLight`'s setup) | P15 | Hours (plus an art check) |
| 18 | **Spotted caption** from `Classify` data (band, distance, light, decisive modifier) | P16 | Hours–1 day |
| 19 | **Far-band light sampling** every 0.5 m instead of 2 points per ray | R6 | Hours |

---

# FUNDAMENTAL CHANGES

| Change | Why necessary | What it replaces | Player benefit | Systems affected | Migration | Priority |
|---|---|---|---|---|---|---|
| **Stealth Readability layer** (Revision 2) | The game judges by rules it doesn't show (R6) | Alt-only cones, 2-sample far tint, corner eye, unexplained Spotted | Decisions instead of guesses | New `ExposureField` (from `LightSystem`); ConeRenderer; GameLight (ranges, rims, cookies); UIHud (disc, ticks, toe, pips); Npc.Perceive (cause record); debrief UI; level lint; bot harness | No save impact; per-map lint pass; an art pass on the light knee | 1 |
| **The Hunt** | No middle between unseen and dead (R1) | Global integer alarm, instant shout | Failure is play | AIDirector, NPC states (Spotted, Hunt, Lost), Shout, HUD, Dossier, MissionController challenges, all mission tuning | Keep `Alarm` as the Lockdown driver; add a Hunt layer beneath; retune per mission | 2 |
| **Shadow Dash** | Teleport conflicts with WASD and topology | Shadowstep | Body verb from M01 | VampireAbilities, VampireMovement, Skills.cs, Shade tree, levels with Shadowstep-only routes, save data | Refund Shadowstep marks; scan maps for teleport-only routes (add climb/Dash routes) | 3 |
| **Grab-drag-feed** | The signature act is a stationary refill | Feed channel | Feeding as skill | VampireMovement (cancel rules), Feed, NPC (struggle), carry, animation | Feed keys keep F/R | 4 |
| **Input layer: soft target, no auto-walk, 4 slots** | Two movement authorities; aim conflict | Hover aim, `TickPendingAbility`, 6 slots | Abilities never stop movement | VampireAbilities, aim UI, thralls, hub loadout UI | Save: map the 6 slots to 4, prompt to choose | 5 |
| **Camera occlusion** | Interiors and blocks hide her | — | Trust | TacticalCamera, map builder (tag buildings/roofs), shaders; must keep ground ink visible through cutaways | Map build step | 6 |
| **Progression rebuild** | 40 min without powers; playstyle not paid; bloat | Skills.cs (42), ArtsOpen, Vitae table, mastery unlocks | Every style progresses; Preparations make briefings a decision | Skills.cs, UIHub, CampaignEconomy, missions' story grants, briefing UI | Refund all Marks on load with a re-teach screen | 7 |

---

## 42. Top 20 backlog (re-ranked in Revision 2)

**What changed.**
- Readability enters as a block of five items (#3–#7).
- The Reaction Window absorbs the Spotted Explanation.
- Terror and push-doors fall out of the top 20 (they are #21 and #22).
- **Gate:** no Hunt tuning and no mission tuning until the WASD Stealth Readability Test (§44.1) passes. Hunt frequency and Reaction Window results are meaningless while players are detected through confusion rather than mistakes.

| Priority | Change | Problem | Player Impact | Size | Dependencies |
|---|---|---|---|---|---|
| 1 | Fix Shout radius | Map-wide alarms | Critical | Small | — |
| 2 | Fix Dazed tick | Sip exploit, Dossier flood | Critical | Small | — |
| 3 | **Exposure Field + truthful light** (re-authored ranges, rims; SR.3–4) | "That looked dark enough" (P15) | Very high | Medium | — |
| 4 | **Cone redraw**: near sector, touch circle, light-clipped far fill, grace, live state geometry (SR.5) | Wrong or hidden shapes (P10) | Very high | Medium | 3 |
| 5 | **Contextual cones + pins + Alt as Tactical** (SR.6, SR.12) | Alt held while steering (P16) | Very high | Small–Medium | 4 |
| 6 | **Ilse's ground disc, watcher ticks, step preview** (SR.7–8) | Corner eye; "will this step expose me?" | High | Medium | 3, 4 |
| 7 | Remove player auto-walk | Two movement authorities | High | Small | — |
| 8 | **Reaction Window + shout wind-up + Spotted Explanation** (SR.10) | Instant fail; "how did he see me?" | Very high | Medium | 1, 4 |
| 9 | The Hunt (local pursuit, LKP, Lost, budget §20.4) | No middle | Very high | Large | 1, 2, 8 |
| 10 | Shadow Dash (baseline) + water check in `Cast` | Teleport + no early verb | Very high | Medium | — |
| 11 | Open Arts after M01 + Beckon M02 | No powers 40 min | High | Small | — |
| 12 | Grab-drag-feed (with Warm / Overflow) | Stationary feed | High | Medium | 2 |
| 13 | Soft-target quick-cast, 4 slots | Aim conflict | High | Medium | 7 |
| 14 | Camera: lead, snap, locked frame, off-screen pips with meters | Camera lost under WASD | High | Small–Medium | 5 |
| 15 | Camera occlusion (ink survives cutaway) | Interiors | High | Large | Map tags |
| 16 | Paid challenges + **Preparations** | Playstyle not paid | High | Medium | — |
| 17 | Shove + Dash dodges telegraphs | No early recovery | High | Medium | 10 |
| 18 | Save timer, 3 slots, no save while seen | Savescum | Medium | Small | 9 (for "seen") |
| 19 | Occlusion cookies for unshadowed lamps; light lint per map | Light through walls; unsafe gaps | Medium | Medium | 3 |
| 20 | Tree rebuild (31 nodes) + Dossier from M03 (max 2, decays) | Bloat; late then too much | Medium | Medium | 10, 13 |

Next in line: Terror inversion (#21; it needs the Hunt to exist first); push-to-open doors and corner slip (#22); the debrief Detections page (#23).

Added 2026-10-02: **The blood remembers** (#24, §48): feeding gives Ilse what the victim knew. After the Readability Test, because it adds information to the screen.

---

## 43. Acceptance criteria for the Top 10

| # | Change | Observable criteria |
|---|---|---|
| 1 | Alarm bugs (Shout + Dazed) | A sighting in M07 alerts ≤ 6 NPCs (not 48). An NPC standing at radius + 1 m stays Relaxed. A sipped watchman wakes in 45 ± 2 s, reports **once** (WitnessReports +1), and the area goes Wary. Regression tests pass |
| 2 | **Stealth Readability layer** | **Truth (automated):**<br>• The Exposure Field equals `LightAt` within 0.02 at 10,000 random points per map.<br>• Every light type's rendered ground edge is within 0.5 m of its 0.35 contour.<br>• Cone fill agrees with `Classify` plus line of sight at every vertex in a bot sweep.<br>• No lamp lights ground it can't reach.<br>**Timeliness (telemetry):** in ≥ 95% of entries into a detecting region, that guard's cone (or near sector) was visible ≥ 1.5 s before.<br>**Comprehension (testers):**<br>• ≥ 90% correct "seen / not seen" predictions at 10 marked spots.<br>• ≤ 1 in 10 detections rated "I don't know why".<br>• Alt held or toggled ≤ 10% of play time.<br>• Passes §44.1 |
| 3 | The Hunt | In M07, a single detection with no reaction becomes a Hunt that a tester escapes ≥ 60% of the time without reloading. Average Hunt 20–60 s. Afterwards the area is Wary and its lamps are relit. Careful testers average ≤ 1 Hunt per mission. No mission averages > 1 Hunt per 10 min |
| 4 | Reaction Window + Spotted Explanation | In 10 forced detections, testers silence the spotter ≥ 4 times. Asked "what caused it?", testers name the logged cause (band, light, touch, noise) ≥ 90% of the time. The caption appears on every Spotted on every difficulty |
| 5 | Shadow Dash | Testers use Dash ≥ 3× per mission in M01. No tester reaches an area by Dash that has no intended route (map scan: zero wall or water crossings). The water check holds in both aim and `Cast` |
| 6 | No auto-walk / soft target / 4 slots | With no keys held, Ilse never moves. F out of range shows "Too far (3.4 m)" and nothing else. Average move speed while casting is ≥ 80% of normal. Mis-targets ≤ 1 in 10 casts on pad |
| 7 | Grab-drag-feed | ≥ 70% of feeds in darkness. Drag used in ≥ 50% of drains. Warm is active ≥ 25% of play time. No tester reports a speed penalty after feeding |
| 8 | Act I body kit + first gift | A new player has their first chosen power before minute 15 (telemetry on 5 testers). "Wow" before minute 10 (self-report) |
| 9 | Camera | Ilse is never hidden > 1 s in M05 or M06. ≤ 1 death per hour from an off-screen guard. Every off-screen guard whose meter on her is rising shows a pip |
| 10 | Pay every playstyle (Preparations) | Every style reaches A10 by M12 ± 1 (sim). ≥ 50% of replays pick a Preparation. Testers describe Preparations in fiction terms ("I bribed the maid"), not as unlocks |

---

## 44. Playtest plan

| Test | Who | Setup | Measures | Pass |
|---|---|---|---|---|
| **WASD Stealth Readability Test** (Revision 2; **runs first**) | 6–8 players, KB/M and pad | Readability gym + M02 + M05 | See §44.1 | See §44.1 |
| **WASD Movement Test** | 5 new players, KB/M and pad | M02 + a corner-and-door gym | Wall-sticks per minute; unintended detections at cone edges; door stops; camera complaints | < 1 stick/min; ≥ 80% say movement "felt right" |
| **Failure Recovery Test** | 5 players | Forced detection in M05/M07 | Reloads per detection; Hunt escapes; time in Hunt; "fun" rating of the Hunt | Reloads per detection < 0.3; Hunt rated ≥ 4/5 |
| **Build Diversity Test** | 4 players, one per build | M07 bridge | Solution time; distinct routes; ability use mix | 4 distinct solutions; times within 2× of each other |
| **Opportunity Test** | 5 players | M03 courtyard | Seconds idle per minute (no movement, no action) | ≤ 15 s/min |
| **Feeding Test** | 5 players | M02–M04 | Feeds in dark vs. light; drag use; sip/drain ratio | ≥ 70% dark; ≥ 30% drains |
| **Act I Hook Test** | 8 first-time players | M01–M02 cold | Time to first "wow" (self-report); quit before 30 min | Wow < 10 min; no quits |
| **Camera Test** | 5 players | M05, M06 interiors | Times Ilse hidden > 1 s; deaths from off-screen | 0 hidden > 1 s; ≤ 1 off-screen death/hour |
| **Economy Test** | Sim + 3 players | Full campaign (sim) | Awakening per mission per style | All styles A10 by M12 ± 1 |
| **Ten-hour Test** | 2 players | Full campaign | Repetition complaints; dominant strategy observed | No single verb > 40% of takedowns |
| **Hunt Frequency Test** (Revision 2) | Telemetry + 5 players | M02–M07 | Hunts per mission; Hunts per 10 min; detections rated "I don't know why" | Careful players ≤ 1 Hunt per mission; no mission > 1 per 10 min on average |

### 44.1 WASD Stealth Readability Test (Revision 2)

**Purpose.** Before any Hunt or mission tuning, prove that a player moving at WASD speed has the information to decide. **This test gates the Hunt Frequency Test and the Failure Recovery Test.**

**Who:** 6–8 players.
- At least 3 first-time stealth players and 3 genre veterans.
- At least 2 on pad.

**Setup:**
- A **readability gym**:
  - three lamp types at different heights;
  - a doorway with wall occlusion;
  - a moving lantern carrier;
  - two overlapping cones;
  - one Wary and one Investigating guard;
  - a roof with a hunter who looks up.
- Then M02 (streets) and M05 (interiors).
- **No pausing is allowed. Alt use is logged but not forbidden.**
- The new readability layer is on at default settings (Hunter difficulty).

**Protocol.**
1. **Free play** (10 min per map) with telemetry: band entries, detections and causes, Alt time, stops longer than 2 s.
2. **Freeze probes.** At 12 scripted moments the game freezes **for 2 s, with the overlays as they were**, then hides the screen. The player answers out loud:

| # | Question | Correct answer comes from |
|---|---|---|
| Q1 | "Can this guard see you here?" | `Classify` + line of sight at that frame |
| Q2 | "Where does his cone end?" (point on screen) | `FarRange`, wall-clipped |
| Q3 | "How close can you get to him in darkness?" (point) | The near-sector edge, or the touch circle behind him |
| Q4 | "Where does this lamp stop exposing you?" (point) | The 0.35 contour from the Exposure Field |
| Q5 | "Which route between these lights is safe?" (trace) | Dark corridor width ≥ 0.5 m and outside every detecting region |
| Q6 | "Will your next step (in the direction you're pressing) expose you?" | Step-preview ground truth |
| Q7 | (After each real detection) "What just caused it?" | The logged cause (band, light source, touch, noise, searchlight) |

3. **Route choice.** Three times, the player picks one of two lit-street routes and walks it. Was the choice the actually safe one?
4. **Debrief interview:** "When did you feel you were guessing?"

**Measures:**
- accuracy per question;
- answer time;
- Alt time share;
- stops of 2 s or more near guards (hesitation);
- detections rated "I don't know why";
- point-to-boundary error, in metres, for Q2–Q4.

**Pass** (all of these):
- **Q1, Q6 and Q7 ≥ 90% correct**, answered within 2 s.
- **Q2–Q4 within 1 m** of the true boundary in ≥ 85% of answers.
- **Q5: the safe route chosen ≥ 90%** of the time.
- **Alt ≤ 10%** of play time.
- **≤ 1 in 10 detections** rated "I don't know why".
- No measurable difference between pad and KB/M players beyond 10 percentage points.

**If any line fails:** **"The stealth information is not readable enough yet."**
- Iterate on STEALTH READABILITY first: thresholds, contrast, contextual rules, rims or the light knee.
- Then re-test.
- Do not start Hunt tuning, and do not lower detection rates to compensate. **A rule the player can't read is a readability problem, not a balance problem.**

**What each failure points to:**

| Failing question | Likely fix |
|---|---|
| Q1 | The cone fill rule (SR.5): the far fill on lit cells, its contrast |
| Q2 | End arc visibility; wall clipping |
| Q3 | Near-sector contrast; touch-circle trigger distance |
| Q4 | Light re-authoring (the knee); rim visibility distance |
| Q5 | Level lint (gap width); rim merging |
| Q6 | Step-preview lookahead (0.6 s) or its visibility |
| Q7 | Spotted Explanation caption or flash |
| High Alt use | The contextual rules are too narrow (SR.6) |


---

# DO NOT BREAK THESE

1. **WASD direct control** with crisp easing (28/40 m/s², 900°/s), camera-relative movement.
2. **Push-to-traverse** for climbs and drops; Space to take at once (D116).
3. **Light as the core read**: darkness hides outside the near sector; one threshold (0.35) for exposure, regen and Dash. *Revision 2:* what is drawn must match what is judged.
4. **Hand-authored missions** with multiple routes: M02, M03, M07 especially.
5. **The Vampire Laws** (water, threshold) as rules in code, not scripts.
6. **The Dossier** as a concept: enemies learning the player's habits.
7. **Sip vs. drain** and the humours from blood types.
8. **Inspect pause (P)** and the full cone view on Alt (now the Tactical tier above contextual cones).
9. **Difficulty that changes perception and alarm, not HP sponge.**
10. **The A9 Apex moment**: overwhelming violence that costs.
11. **Thrall party control by portrait/T** (D117).
12. **Silent drops** and the vertical predator road.

---

# WHAT THE RESEARCH SUGGESTS WE SHOULD DO DIFFERENTLY

| Research observation | Implication | Proposed game change |
|---|---|---|
| "Detected = reload" is the genre's most common complaint (Styx, Shadow Tactics) | Binary failure drives save-scumming | The Hunt; Reaction Window |
| Shadow Tactics' detection slow-mo and audio cue are praised for readability | Players need to see why they were seen | Reaction Window with a visible wind-up ring and the Spotted Explanation |
| Shadow Tactics' cones draw a solid "sees you" zone and a striped "sees you if standing" zone in one shape [Genre precedent] | One shape can carry two rules if fill style distinguishes them | Near sector solid; far sector filled only on exposed ground; dashed grace (SR.5) |
| Mark of the Ninja makes sound radii visible and shows the character's lit or unlit state on the character [Research] | State lives on the avatar, rules on the ground | Ilse's ground disc; noise ripples; step preview (SR.7–8, SR.11) |
| Thief's light gem made "how visible am I" a single, trusted read [Genre precedent] | One trusted threshold, always visible | Two-state disc at the one gameplay threshold (0.35) |
| Dishonored's Blink and Aragami's teleport trivialised levels | Long-range teleports delete level design | Navmesh Dash, 6 m, dark recharge |
| Aragami 1's light-cost tension was loved; Aragami 2's removal was hated | Light must cost movement | Dash recharges only in darkness |
| Aragami 2's consequence-free detection was hated | Escape must leave marks | Persistent Wary, relit lamps, Dossier weight |
| Dishonored's chaos system penalised players' own toolkit | Don't punish using powers by narrative | Dossier counters habits, max 2, decays; no ending penalty for kills |
| Invisible Inc's escalating alarm makes detection survivable but costly | Escalate locally and visibly | Hunt → second Hunt → Lockdown |
| MGSV Revenge works when visible and capped | Show adaptations | Briefing shows countermeasures and why |
| Hitman's mastery unlocks drive replay | Reward mastery with options, not badges | **Preparations**: diegetic briefing options (bribed servant, key, compromised guard, gas valve) |
| Vampyr's merciful path crippled progression | Don't make the moral path weaker | Sip path earns via challenges; Marks not tied to kills |
| Bloodlines' feeding had no risk | Feeding must be a decision | Grab-drag-feed with light, struggle, evidence |
| Ereban's dominant shadow power | One power must not solve everything | One verb per job; law gates need different trees |
| Eriksholm's instant fail felt linear | Instant fail kills improvisation | No instant fail outside scripted moments |
| Mimimi: controller-first design gives depth without complexity | Fewer, clearer verbs | 4 slots, soft targets, 31 nodes |
| Genre ceiling 0.25–0.5M units; demo decisive | The first 15 minutes sell the game | Act I body kit + first gift at M01 end |

---

## 47. The game we'd design today (and what that reveals)

| Area | Designed today | Current build | Sunk cost exposed |
|---|---|---|---|
| **Core loop** | Read → shape → close → take → handle → (hunt) → exploit | Observe → wait → feed → (reload) | The *wait* and the *reload* are what the build does most |
| **Movement** | WASD + Dash + Rise + drop-feed from minute one | WASD + powers from minute 40 | Act I was designed for the old click-to-move fledgling |
| **Stealth** | Light, near band, cone grace, Reaction Window, **every rule drawn from its own numbers** | Light (rendered 1.5× its real reach), near band (invisible), cones on Alt, instant shout, no explanation | The near band and cones were kept behind Alt from click days; Unity lights were tuned for looks |
| **Abilities** | 4 slots, soft targets, one verb per job | 6 slots, hover aim, overlap | Click-era targeting retained |
| **Feeding** | Grab-drag-sip/drain | Stand-still channel | Inherited from click-to-move (target a unit, it plays out) |
| **Blood** | Start 50, Warm after feeding, Overflow, Starving | Start full, Starving | Feeding gave no window to act in |
| **Combat** | Shove, Dash dodge, Rend, Apex | Rend (late), Apex | Combat as late reward only |
| **Failure** | The Hunt | Map-wide alarm (bug) | The global integer was never questioned |
| **Progression** | Body kit + first gift + paid playstyle | Powers at M03, kills pay | Story beat drove the system |
| **Enemies** | Watch that breaks; Vigil that learns | Watch + Dossier late | The arc's end was in stats, not AI |
| **Level design** | Law gates with 2+ answers, loops near objectives | Good routes, some single-answer gates | Levels are the strongest asset; keep |
| **Mission structure** | 13 authored missions; Hunt mission; Dawn clock finale | Same | Keep |

**What this exposes:** the missions and laws are what we'd build again. **The layer between the player's hands and the world (targeting, feeding, failure) is what we'd build differently**, and it is the click-to-move inheritance. That's where the work should go.

---

# KEEP
- WASD locomotion, easing, camera-relative movement, push-to-traverse.
- The light model and the near band (made visible and truthful: STEALTH READABILITY).
- All 13 missions and their routes.
- Vampire Laws (water, thresholds) and the humours.
- The Dossier (rebounded and capped).
- Sip vs. drain, carry/hide, snuff.
- Smother, Mesmerize, Enthrall, False Orders, Snare, Hemorrhage, Silverblood, Apex (retuned), Terror, Nightblood, Eclipse.
- Thrall party control (D117), inspect pause, and Alt (as the Tactical view).

# CHANGE
- Shout/hear radii (bug), Dazed (bug), water check in `Cast`.
- Detection → the Hunt.
- Feed → grab-drag-feed.
- Ability input → soft-target quick-cast, 4 slots, no auto-walk; Beckon one-stage.
- Camera → lead, snap, locked frame, occlusion, threat markers.
- Progression → Arts after M01, first gift, paid challenges, Preparations, 31 nodes.
- Cones → contextual, near sector, light-clipped far fill, touch circle; light → re-rendered to its exposure contour.
- Sneak speed, doors, corner slip, cone-edge grace; Shadowing as footfall masking.
- Gloom, Mist, Rend, Pounce, Apex numbers, Communion.
- Save UX; Dossier timing and cap.

# CUT
- Shadowstep, auto-walk, two-stage aims, slots 5–6.
- Bloodmend, Corpse Puppet, Living Lie; Court/Scent/Herding/Soft Landing as separate nodes.
- The M03 Arts gate.
- *Revision 2:* passive drain, Gorged penalties, the Shadowing speed-match, the objective lockout, the cosmetic DARK/SHADOW split.

# ADD
| Addition | Purpose |
|---|---|
| The Hunt | Failure as play |
| Reaction Window | Readable, recoverable detection |
| Shadow Dash | Early body verb; escape; light tension |
| Grab-drag-feed | The signature act as skill |
| Shove | Early recovery |
| Rattle objects | Opportunity without powers |
| Thrall Signal | Synchronised kills without a planning mode |
| Warm blood / Overflow | A window to act after feeding |
| Stealth Readability layer | Every rule drawn; detections explained |
| Exposure Field | One source of truth for light |
| Terror inversion | The arc's ending in AI behaviour |
| Preparations | Replay, with the fiction intact |
| The blood remembers (§48) | Feeding becomes the intelligence verb; victim choice becomes tactical |

---
# THE 10 CHANGES I WOULD MAKE IF THIS WERE MY GAME

**Revision 2 changes this list materially:**
- **Stealth Readability enters at #2.** It is cheaper than the Hunt. Without it, every later test measures confusion rather than design.
- **The Reaction Window absorbs the Spotted Explanation** (#4).
- **Terror inversion drops to #11.** It is still right, but it depends on the Hunt and arrives in Act IV.
- **#10 is now paid playstyles with Preparations.**
- Everything else moves down one place.

### 1. Fix the two alarm bugs
- **Current problem:** a shout reaches 225–270 m; sipped victims report about 140×/s and never wake.
- **Why this solution:** it's a unit error and a guard order, not a design question.
- **Research/playtest evidence:** 16/16 and 48/48 NPCs alerted; WitnessReports 0 → 9,982 in 70 s [Played].
- **Current player experience:** alarms are either meaningless (Act I) or instantly fatal (mid-game); sip is free.
- **Improved player experience:** a shout is local; a sip is a timer.
- **WASD implications:** escapes become possible.
- **Systems affected:** NPC (Shout, HearNoise, Dazed tick), AIDirector, Dossier.
- **Implementation size:** Small.
- **Priority:** 1.

### 2. Stealth Readability: show the rules the game judges by (Revision 2)
- **Current problem:**
  - The rendered light reaches about 1.5× the distance at which it exposes Ilse, and spills through walls.
  - Cones exist only while Alt is held.
  - The near sector and the touch zone are undrawn.
  - The light read is a corner eye.
  - Detection never explains itself (R6, P10, P15, P16).
- **Why this solution:**
  - One source of truth: the Exposure Field.
  - One grammar: fill, edge, ripple, mark.
  - One rule: "if his colour is on the ground under you, he can see you there".
  - Information appears in three tiers, so normal play needs no Alt.
- **Research/playtest evidence:**
  - Shadow Tactics' solid and striped cone zones [Genre precedent].
  - Mark of the Ninja's visible sound and lit-state avatar [Research].
  - Thief's light gem [Genre precedent].
  - Our runs: deaths at 1–5 m in darkness [Played]. Our code: exact but hidden rules [Code].
- **Current player experience:** "That looked dark enough." "How did he see me?" Holding Alt while steering.
- **Improved player experience:** "His colour's on the ground a step ahead; I'll go round the lamp's rim."
- **WASD implications:** the precondition for deciding at the speed of the hands.
- **Systems affected:** LightSystem (new Exposure Field), GameLight, ConeRenderer, UIHud, Npc.Perceive, debrief, level lint, bot harness.
- **Implementation size:** Medium, in 9 steps (SR.14). Steps 1–5 take about two weeks.
- **Priority:** 2. **It gates Hunt tuning** (§44.1).

### 3. Build the Hunt
- **Current problem:** detection has no middle.
- **Why this solution:** it creates the most-wanted thing in the genre (survivable failure) with WASD as its engine. **Revision 2** adds a budget (0–1 Hunts per mission for careful players) and real advantages for staying unseen, so the Hunt never overwhelms stealth (§20.4).
- **Research/playtest evidence:** Styx/Shadow Tactics complaints; Invisible Inc; Aragami 2's lesson about weightless escapes [Research]; our M02/M07 runs [Played].
- **Current player experience:** walk past alarms or reload.
- **Improved player experience:** "I was seen, I ran, I lost them, then I took the one who strayed."
- **WASD implications:** the core payoff of direct control.
- **Systems affected:** NPC states, AIDirector, HUD, Dossier, challenges, mission tuning.
- **Implementation size:** Large.
- **Priority:** 3.

### 4. Add the Reaction Window with the Spotted Explanation
- **Current problem:** a sighting is an instant shout, and nothing says why it happened.
- **Why this solution:** a one-second chance to correct with skill, cheap with `Game.SlowMo`. The same half-second shows the spotter, his sight-line, the boundary she crossed and a one-line cause from `Classify` data (SR.10).
- **Research/playtest evidence:** Shadow Tactics' detection slow-mo [Research].
- **Current player experience:** "What saw me?"
- **Improved player experience:** "Near band, 4.1 m. Yep, I pushed too far." Then: "I saw it coming and silenced him."
- **WASD implications:** a reflex moment only direct control can offer, and an explanation that arrives while the hands can still act.
- **Systems affected:** NPC detection (cause record), Game time, VFX/audio, HUD, debrief.
- **Implementation size:** Medium.
- **Priority:** 4.

### 5. Replace Shadowstep with Shadow Dash, baseline from M01
- **Current problem:** a cursor-aimed teleport while the keys steer; topology-deleting; arrives late.
- **Why this solution:** direction from the keys; navmesh-bound; light as the cooldown.
- **Research/playtest evidence:** Dishonored, Aragami, Ereban [Research]; Aragami 1's light tension [Research].
- **Current player experience:** 40 min of walking, then a teleport that answers everything.
- **Improved player experience:** a vampire's body from minute one, mastered over the campaign.
- **WASD implications:** perfect fit.
- **Systems affected:** VampireAbilities, VampireMovement, Skills, maps.
- **Implementation size:** Medium.
- **Priority:** 5.

### 6. Remove auto-walk; soft-target quick-cast; 4 slots
- **Current problem:** two movement authorities; aim conflicts.
- **Why this solution:** abilities never stop movement; pad-ready.
- **Research/playtest evidence:** Mimimi controller-first [Research]; D114 notes [Code].
- **Current player experience:** "She walked into the cone by herself."
- **Improved player experience:** she does exactly what my hands say.
- **WASD implications:** the WASD contract.
- **Systems affected:** VampireAbilities, aim UI, loadout UI, thralls.
- **Implementation size:** Medium.
- **Priority:** 6.

### 7. Grab-drag-feed
- **Current problem:** feeding is a stationary refill that any key cancels.
- **Why this solution:** turns the signature act into skill with light, struggle and evidence.
- **Research/playtest evidence:** Bloodlines' riskless feeding [Research]; VampireMovement.cs:78 [Code].
- **Current player experience:** stand behind, press F, wait.
- **Improved player experience:** grab, haul into the dark, choose, and then I'm Warm: time to push.
- **WASD implications:** the drag is steered.
- **Systems affected:** feed, NPC struggle, carry, animation.
- **Implementation size:** Medium.
- **Priority:** 7.

### 8. Rebuild Act I around the body kit and a first gift
- **Current problem:** no powers for 40 minutes.
- **Why this solution:** demo and refund window; the fantasy at minute one.
- **Research/playtest evidence:** genre openings; the 0.25–0.5M ceiling with a demo [Research].
- **Current player experience:** "Am I a vampire yet?"
- **Improved player experience:** Dash, grab, drop on someone, then *choose* a power.
- **WASD implications:** body verbs.
- **Systems affected:** ArtsOpen, story grants, M01–M03 tuning, UIHub.
- **Implementation size:** Medium.
- **Priority:** 8.

### 9. Camera for direct control
- **Current problem:** no occlusion; manual look-ahead; rotation swings the path.
- **Why this solution:** the high three-quarter view stays; the layers WASD assumes are added.
- **Research/playtest evidence:** direct-control camera failure is a top complaint [Research]; no cutaway in code [Code].
- **Current player experience:** losing her under roofs; walking into off-screen cones.
- **Improved player experience:** always see her and what threatens her.
- **WASD implications:** central.
- **Systems affected:** TacticalCamera, map builder, shaders, HUD.
- **Implementation size:** Large (occlusion), Small (the rest).
- **Priority:** 9.

### 10. Pay every playstyle (Preparations)
- **Current problem:** kills pay; Ghosts trail; replays pay nothing.
- **Why this solution:** challenges pay Marks and unlock **Preparations** (diegetic briefing options); Vitae flattened.
- **Research/playtest evidence:** Hitman unlocks; Vampyr's crippled merciful path [Research]; sim [Sim].
- **Current player experience:** "Clean play isn't worth it."
- **Improved player experience:** every style reaches its capstone; a replay starts with "the maid owes me a door".
- **WASD implications:** none.
- **Systems affected:** MissionController challenges, CampaignEconomy, UIHub.
- **Implementation size:** Medium.
- **Priority:** 10.

### Dropped to #11: Terror inversion
- **Current problem:** the "prey to predator" arc lives in stat numbers.
- **Why this solution:** the AI shows it: the Watch breaks; only the Vigil holds.
- **Research/playtest evidence:** the A9 Apex moment felt earned [Played]; MGSV-style visible adaptation [Research].
- **Current player experience:** late game is the same guards with more damage.
- **Improved player experience:** a street clears when she walks into it.
- **WASD implications:** walking into a square is the moment.
- **Systems affected:** NPC morale, Terror node, story beat (M10), Vigil AI.
- **Implementation size:** Medium.
- **Priority:** 11 (was 10). Needs the Hunt in place, and only matters from M10.

---

## 48. The blood remembers (added 2026-10-02)

**The idea.** Feeding gives Ilse what the victim knew. Today feeding is a resource pump (Vitae, a humour, Marks) and
scouting is a separate job done by watching. This makes the sip the game's intelligence verb: one act that is both power
and knowledge, and the most vampiric thing she does. It mirrors the Dossier: the Vigil learns her between nights; she
learns them one mouthful at a time.

**Sip vs. drain.**
- **Sip (he lives):** a glimpse of his own next minute or two: his beat and where he is heading. Cheap and quiet; it
  never goes stale through her, because nobody goes missing.
- **Drain (he dies):** everything he knew about the people around him: his beat, his partner's and anyone he meets on
  the round; where and when he checks in and who notices if he doesn't; doors he holds keys to; lamps he keeps lit; for
  an officer, the search plan.
- **The catch:** he is gone. When his partner reaches the meeting and he isn't there, the partner's route becomes a
  search. The partner's remembered route is drawn up to the missed meeting and breaks off there, so the player knows
  exactly how long the knowledge holds. Draining becomes a chain of decisions (use the window, or be waiting at the
  meeting to drain the partner and learn *his* contacts).

**What each kind of person knows** (first pass, per archetype):

| Victim | Memory |
|---|---|
| Watchman | His beat and his partner's, with timings; the check-in point |
| Servant / household | Which doors of the house open, who is expected tonight; a home's invitation route |
| Lamplighter | His circuit and when each lamp goes dark or is relit |
| Officer | The search plan if an alarm goes up; where reinforcements come from |
| Hunter / squad member | The squad's sweep and its leader |
| Notable | Marks plus an authored secret: a password, a route, the objective's location (1–2 per mission) |

**Rules.**
1. **The memory is true when she drinks it.** It goes stale only through things the player can see: a missed meeting,
   an alarm or the Hunt, a Dossier change of routine. It never lies (SR.1).
2. **Drawn as ink.** Remembered routes use the intent-path dots (SR.5 dotted = prediction), dashed where they depend on
   someone who is now missing, and fade when stale. One memory at a time; gone at the end of the night.
3. **Partial by design.** A person knows his role, not the map. No memory replaces watching for the rest of the guards.
4. **A human line.** Each memory carries one line of the person ("Mara will be up waiting"), so draining costs
   something without extra cutscenes.
5. **Interactions.** Lethe (wipes a sip victim's memory) is the mirror: she can take memories and erase them. A notable
   drained starts the 2-minute absence search (§20.4); his memory is the richest and the most dangerous to take.

**Playstyles.** Ghosts sip for short, safe glimpses. Predators drain for the wide view, but each drain starts a clock
and pushes the night toward the Hunt.

**Build.** Data mostly exists (patrol routes, `paired=`/`follow=`, squads, lamplighter circuits, home doors, officers).
New: a per-archetype "what they know" table, a memory overlay on the `IntentPaths` ink, a staleness rule tied to the
partner's missed meeting, and authored notable secrets. Size: Medium.

**Acceptance.** Testers can say what a drained guard's partner will do next and when that stops being true; feeding
choices in telemetry follow what a victim knows, not only isolation; the Readability Test probes (Q1–Q5) don't drop when
memories are on screen.

**Order.** After the WASD Stealth Readability Test (§44.1): it adds information to the screen, and the test must
measure the current layer first. It works with the current alarm system; its staleness rules gain from the Hunt.

---
# FINAL VERDICT

## What gameplay direction should the game commit to?
**A direct-control vampire predator game where failure becomes a hunt.** The player reads light, shapes an opening, takes someone with their own hands, and when it goes wrong, escapes, fights or turns the Hunt into the next opening. Shadow Tactics' planning layer stays dead. The game is about the body in the dark, not the plan on pause.

## What does WASD allow this game to do especially well?
- **Escape:** steering through a Hunt is a skill a click game can't express.
- **Shadowing and closing:** following a guard at his pace, the last-metre lunge, the drop from a ledge.
- **Reflex correction:** the Reaction Window only matters when your hands can act in it.
- **The drag:** hauling someone into darkness is a physical act.

## What current systems have not adapted properly to WASD?
- **Ability targeting** (hover aim, two-stage aims, out-of-range auto-walk).
- **Feeding** (a stationary channel cancelled by the keys).
- **The camera** (no occlusion, manual look-ahead, rotation mid-move).
- **Failure** (designed for a quickload-per-mistake rhythm).
- **Shadowstep** (a cursor teleport).

## What is the game's biggest gameplay weakness?
**It judges the player by rules it doesn't show, then punishes with no middle.** (Revision 2 adds the first half.)
- The light the player sees is about 1.5× the light that exposes them.
- The cones, near sectors and touch zones are hidden behind Alt, and no detection explains itself.
- When a mistake *does* happen, there is no survivable middle between unseen and dead, made worse by the shout-radius bug.

Together these drive the waiting, the save-scumming and the mid-game death wall. **Fix the reading before the punishing.** A Hunt the player didn't understand starting is still unfair.

## What is its strongest existing idea?
**The Vampire Laws as level rules**, backed by authored missions (M02, M03, M07) that already offer multiple routes. Running water and thresholds are real code, not flavour, and they give every gate a folklore answer.

## What major change has the highest upside?
**The Hunt.** It fixes the biggest weakness, uses WASD best, feeds the Dossier, makes Act I lessons honest and gives the game a second pillar the genre lacks.

**But the Stealth Readability layer has the highest upside per hour of work, and it has to come first.** It makes every other change testable.

## What should be rebuilt even if substantial work is lost?
- **Shadowstep → Shadow Dash.** The teleport and its range upgrades go.
- **The feed channel → grab-drag-feed.**
- **The ability input layer** (hover aim, `TickPendingAbility`, 6 slots).
- **The skill tree** (42 → 31 nodes; refund on load).
- **The global alarm as the only failure layer.**

## What should absolutely not be changed?
WASD direct control and its feel; the light model; the 13 missions and their routes; the Vampire Laws; the Dossier concept; sip vs. drain; inspect pause; difficulty as perception. See **DO NOT BREAK THESE**.

## What would make players choose this over other stealth games?
**Being the predator, not the infiltrator.** Mark of the Ninja, Shadow Tactics and Aragami make you avoid people. This game makes people the resource, the threat and the tool, then makes failure a chase you can win, and ends with a city that runs from you and a Vigil that has learned how you hunt.

## What should development do next?
1. **This week:**
   - Quick Wins 1–6 and 9–10: both bugs, the `Cast` water check, Arts after M01, Beckon at M02, no auto-walk, sneak 2.2, paid challenges and an honest "Unbroken".
   - **Plus readability Quick Wins 7 and 15–19:** near sector and touch circle, contextual cones, exposure rims, clamped light ranges, the Spotted caption, and far-band sampling.
   - Re-run M02 and M07 with the bot to confirm local alarms.
2. **Weeks 1–2:**
   - The **Exposure Field**, re-authored lights and the cone redraw, in a **readability gym** (SR.14 steps 1–5).
   - Then the **WASD Stealth Readability Test** (§44.1).
   - **If it fails: "The stealth information is not readable enough yet."** Iterate before anything else is tuned.
3. **Next two weeks:**
   - The ground disc and step preview.
   - The Reaction Window with the Spotted Explanation.
   - A **vertical slice of the Hunt in M05**, with the §20.4 budget.
   - Then the Failure Recovery Test and the Hunt Frequency Test.
4. **In parallel:** Shadow Dash prototype in a gym map; WASD Movement Test.
5. **Then:** grab-drag-feed (with Warm / Overflow), soft-target input with 4 slots, camera lead, snap and off-screen pips.
6. **Then:** progression rebuild with Preparations, occlusion (ink must survive the cutaway), and Terror inversion.
7. **Gates:**
   - No Hunt tuning before the Readability Test passes.
   - Do not tune missions M08–M13 until the Hunt and Dash pass their tests. Their tuning depends on both.
