# Vespertine: Gameplay, Fun and Industry Benchmark Audit

*Audit date: 2026-10-02. Build: Unity 6000.6.0f1 editor build, the current working copy. Difficulty: Hunter unless stated.*

> **The fantasy under test:** "At the beginning, I hide from humans. By the end, humans hide from me."
>
> **Verdict in one line:** the systems needed to deliver that sentence are mostly built. Two one-line AI bugs, plus an alarm model with no middle ground, currently make the game deliver the opposite. Early on, humans can't hurt you even when you're seen. Later, one sighting wakes the whole map and kills you in about ten seconds.

---

> **Corrections (follow-up, same day; details in GAMEPLAY_REDESIGN.md §7.1):**
> - **I-04 was a harness artefact.** The bot called `Cast` directly and skipped `ValidateAim`, which already refuses a Shadowstep target when `CrossesWater` is true (confirmed true for all three M07 crossings). A player aiming normally cannot Shadowstep across running water. What remains: `Cast` doesn't re-check water (a latent bug), wall/fence skipping over line of sight is unverified, and a cursor-aimed teleport fits WASD badly.
> - **I-08 is overstated.** Any WASD input already cancels a pending action (D114; VampireMovement.cs:74–78). The chase ran because the bot pressed no keys. The real issue is two movement authorities: when no key is held, the action layer walks Ilse into range.

## 0. How this audit was done (read first)

Honest limits come first, because the brief says to judge the game players will actually play.

**What was played.** Every run was a real mission in the real game, driven through scripted in-editor "bots". Each bot issued the player's own actions (walk, sneak, run, feed, carry, cast) and logged every NPC state change, sighting, hit and blood change.

| Run | Purpose |
|---|---|
| **M01 "The Drowned Ward", fresh start** | Clean run in 4:06 |
| **M02 "Lantern Street", careless run** | Walked straight down the lit street |
| **M02, careful run** | Drained a posted guard, hid, waited, fed on a fleeing target |
| **M07 "The Toll Bridges" at an injected mid-game build** | Awakening 6, 84 HP, 14 nodes. Stealth attempt, then two deaths after detection |
| **M07 geometry test** | Detection set to 0, used only to measure what Shadowstep can bypass. Every other run used normal detection |
| **M07 at an injected late build** | Awakening 9, Apex, Rend, Pounce, Shadowstep, Hemorrhage. Fight-back test against a guard post during a global alarm. Blood was set to full by a debug command; noted where it matters |
| **M07, sip and wait** | Sipped one victim and watched them through their wake-up |

**Other sources:**
- the economy simulator (`CampaignEconomy.Simulate`) for all three play styles;
- source code for every system judged;
- the design documents, compared against what the code actually does.

**What this audit cannot claim:**
- **No hands-on WASD feel.** Bots don't feel input latency, camera drag, or what holding sneak through a cone is like. Every "feel" judgement below is marked *provisional*. A 20-minute session by a human on keyboard and pad is the first thing to do after reading this.
- **Bot pathing is threat-unaware.** Some detections came from the bot walking a straight route a human wouldn't take. I count only detections a reasonable human could also suffer, and the bugs below don't depend on bot behaviour.
- **Harness gaps.** There were 30–60 s pauses between scripted steps. That is idle time a real player wouldn't spend standing still, and it let the world act (bodies were found).
- **Missions not played:** M03–M06 and M08–M14. Judgements about them come from map files, scripts and design documents, and are labelled as such.

**Evidence tags used below:**

| Tag | Meaning |
|---|---|
| **[Played]** | Observed in a run |
| **[Code]** | Read in source, with file:line |
| **[Sim]** | Economy simulator |
| **[Doc]** | Design documents |
| **[Market]** | Comparables research (Steam and Metacritic pages, critic reviews, postmortems, fetched October 2026) |
| **[Inferred]** | My judgement, with the reasoning shown |

---

## 1. Market research: what the comparables teach

Full notes cover Shadow Tactics, Desperados III, Shadow Gambit, Mark of the Ninja, Styx 1/2, Dishonored 1/2, Aragami 1/2, Commandos 2/Origins, Gloomwood, plus Invisible Inc, MGSV, Hitman WoA, Vampyr, Bloodlines, Ereban, Eriksholm and Sumerian Six. Condensed:

| Game | Steam / MC | What players reward | What players punish |
|---|---|---|---|
| Shadow Tactics | 94% / 85 | Hard-edged readable cones; Shadow Mode turns waiting into setup; save-age timer and rotating slots; ~9 hidden badges per mission | "Puzzles with one solution"; quickload loops; waiting; a narrower second half |
| Desperados III | 95% / 86 | Showdown sequences; semi-sandbox maps; route replay celebrating the clean run; Baron's Challenges remix maps | Reload tedium; badges are cosmetic (no Hitman-style unlocks); guns useless versus the fantasy |
| Shadow Gambit | 90% / 85 (users 7.6) | Supernatural toolkit; mission freedom; in-world save fiction | **Too easy**: one character can solo; reused maps. About 260k copies sold, and the studio closed |
| Mark of the Ninja | 96% / 90 | **Perfect information**: binary light, visible noise rings, cue-driven guard states; NG+ that changes rules | Repetition after the early levels |
| Styx 1/2 | 81–83% / 72 | Verticality; clone lure | **Detection means a forced reload** (parry lock-in); AI swings between oblivious and psychic; broken invisibility combos |
| Dishonored | 97% / 88 | Blink and power combos; many valid styles | **Blink trivialises stealth**; chaos punishes the toolkit it hands you |
| Aragami 1 | 89% / 71 | The shadow-teleport fantasy itself, enough to carry a 71-MC game to 89% with players | Teleport plus make-shadow **solves the game**; the loop never grows |
| Aragami 2 | 76% | n/a | Removed the light-drain tension; **detection is consequence-free**; 4 objective types over recycled maps |
| Invisible Inc | 90% | Rising alarm pushes you forward; limited rewinds per difficulty | n/a |
| MGSV Revenge | n/a | Countermeasures force variety | Countermeasures pile up all at once |
| Vampyr / Bloodlines | n/a | Vampire licence and fantasy | The mercy path crippled progression (Vampyr); feeding had no risk (Bloodlines) |
| Ereban (2024) | n/a | n/a | Shadow-merge so strong nothing else is needed; enemies give up |

**Market facts that matter for this project:**
- Genre leaders reach roughly 250k–500k units on Steam.
- A free demo was Shadow Tactics' biggest marketing lever, taking wishlists from 15k to 40k in two weeks.
- Mimimi closed after Shadow Gambit because cost outran the genre's ceiling.
- **Implication:** Vespertine's market is the Mimimi audience plus vampire-fantasy buyers. Both groups will compare it directly to Mimimi on readability and fairness, and to Aragami and Dishonored on "do the powers break it".

**The three patterns this game is closest to repeating:**
1. **Aragami / Ereban / Dishonored.** Shadowstep plus darkness-making is the exact combination reviewers call trivialising. → §13 and §15.
2. **Styx.** Detection funnels into reload. → §8.
3. **Aragami 2.** Detection is consequence-free. → §8.

The game currently suffers from (2) and (3) **at the same time**, at different stages of the campaign.

---

## 2. Player expectation baseline

**REQUIRED.** Missing any of these produces negative reviews.

| Requirement | Status in Vespertine |
|---|---|
| Visible detection states and why you were seen | **Partial.** Cones show on demand (inspect or hold); NPC states exist. A detection that comes from shouts heard 200 m away is unexplainable to a player (§9) |
| Unambiguous light versus dark | **Partial.** The model is near-binary beyond about 5 m, but inside the 5 m "near band" you are seen in any light. That is a hidden rule that contradicts "dark = safe" [Code, Played] |
| Quicksave and quickload with fast loads | **Yes.** F5 works any time, even mid-alarm [Code MissionController.cs:1076, 1095] |
| Consistent, predictable guard rules | **No, currently.** A shout reaches the whole map (§9); dazed victims enter a permanent broken state (§9) |
| Precise controls; separate aim mode for targeted powers | Unverified by hand (provisional) |
| ≥2 real routes per major objective | **Yes**, where checked: the M02 quay bypass; the M07 design has 3 Leat crossings plus a ferry |
| No unfair readability traps | Near-band detection in darkness is one (§8) |
| No progress-blocking bugs | No blocker hit. Two state-corrupting AI bugs (§9) |

**EXPECTED.** Fans compare against Mimimi.

| Expectation | Status |
|---|---|
| Plan-then-fire tool (Shadow Mode) | **Absent.** Single protagonist plus thralls. Defensible, but it needs a substitute such as Apex Hunt's time-slow (§11) |
| Hidden, conflicting per-mission badges | **Present but worthless.** Challenges grant nothing [Code MissionController.cs:910–940] |
| Post-mission summary | **Yes.** The debrief shows time, sightings, alarms, loads, challenges and "The Vigil adapts" |
| Save-age reminder and rotating slots | **Absent** |
| Distinct maps, environmental kills and lures | 14 distinct maps [Doc]; canal disposal, lamps, levers, bells |
| Difficulty that changes perception, not HP | **Yes**: detection multiplier, shout radius, cone visibility, autosave mode [Code Data/Difficulty.cs] |
| Lethal and non-lethal both viable without a morality tax | Designed for it (sip versus drain). Currently distorted by the dazed bug (§16) |
| Late enemies that counter your powers | **Yes, on paper and partly in code**: the Dossier countermeasures, inquisitors, hounds, flares |
| Escape after detection possible but costly | **No.** Act I has no cost; mid-game is near-certain death (§8) |

**DIFFERENTIATORS.** Where Vespertine can win.
- **Blood as the tension economy.** Regen costs blood, only in the dark. Starving is audible. Sip versus drain. *This is the strongest idea in the game.*
- **The Dossier.** An MGSV-style adaptive enemy that reads your habits.
- **"Prey becomes predator" arc.** Pounce, Rend, Apex, Terror and Dread Feast. No Mimimi game has a protagonist who changes category mid-campaign.
- **Thralls.** A light squad layer for a single-hero game.
- **Vampire folklore as rules.** Threshold, running water, invitation, light, holy ground. *Barely used yet* (§45).

---

## 3. Actually playing it

### Fresh start (M01)
- [Played] Clean completion in **4:06** by a bot that knew the route.
- [Inferred] A first-time human will take roughly 10–20 min, depending on how much they read the cones.
- M01 is a teaching level with **no abilities**: the Blood Arts tab opens only after M03 (`ArtsOpen => MissionIndex >= 3`) [Code CampaignState.cs:192].

### Mistakes and aggression (M02, careless run)
I walked down the lit street on purpose.
- [Played] w4 at the yard door saw me. **Within 0.2 s all 16 NPCs on the map** went Searching or Panicked.
- HP fell 49 → 2, then the hits stopped.
- I knocked, entered the target's house **while SEEN**, and completed the mission.

Debrief:
- 3:12; spotted 7×; 2 alarms; 0 loads.
- Awarded **"Before the Bell"** (par 8:00), **"Unbroken"** and **"Merciful Hunger"**.
- +60 Vitae, +1 Mark.

The run that should have been a failure was the fastest, highest-rewarded run of M02.

### Stealth (M02, careful run)
- [Played] Draining a posted guard from behind is trivial: about a 5 s approach, 3.2 s to drain, +40 blood.
- The body was found during an idle gap. The search went global.
- A searcher (Pell) left their post about 80 m away.
- Waiting in the dark stable worked: searchers passed at about 9 m and did not see me. This was good, readable stealth.
- A groom woken by noise saw me at 1–2 m **in darkness** (near band) → another global alarm.
- I then fed on a fleeing target (Wick). The pending feed has **no distance or time cap**, so the character chased Wick across the map into the watchman at Grice's door and died. [Code VampireActions.TickPending]

### Mid-game abuse (M07 at Awakening 6)
- [Played] **Shadowstep across the 4 m Mill Leat** at its south end, bypassing all three designed Leat crossings in 16 s. *(Corrected: see I-04.)*
- Then **spotted by w_quay at about 4–5 m while standing in darkness** (light 0.05–0.19, near band).
- **All 48 NPCs** responded.
- Died twice, each time about **10 s after detection**:
  - hunters' flares lit me to 1.0;
  - hunters from the Fen Cut and the road converged from 40–50 m away;
  - the exit was stripped.
- [Played, Detection = 0, geometry only] Three Shadowsteps (leat → pier gap → Fen Cut, 36 blood total), then a walk to the exit. **Mission complete in 1:15.** *(Corrected: see I-04.)*
  - The mission's whole designed crossing structure (toll bridge, lever, ferry, ferryman) is skippable by the movement power the player has owned since M04.
  - The debrief still awarded 4 challenges, +90 Vitae and +2 Marks.

### Late-game "humans hide from me" test (M07 at Awakening 9, blood set to full by debug)
- [Played] I ran through the town (a tanner panicked at the near band) into a **global alarm**.
- Then I cast Apex Hunt and Rended the Leat guard post: sergeant, two checkpoint guards, the beat watchman, a hunter, and the row guard.
- **6 kills in 25 s**; HP 111 → 29; blood 140 → 20; lockdown triggered; survived.

**This is the fantasy working.** The late game does let you turn on them. The cost was right: it nearly killed me, emptied my blood, and locked down the map.

The catch is that it needed full blood. The mission starts at 30/140, and Apex alone costs 45 [Code Vampire.cs:151, Skills.cs].

### Sip-and-wait (M07)
- [Played] I sipped a tanner (dazed, +blood) and watched them.
- When their daze timer expired they **never woke**. They stayed Dazed and filed a witness report **every frame**:
  - WitnessReports went from 0 to 8,143 in about 60 s;
  - the campaign "Sightings" habit went from 10,782 to 20,764.

Root cause in §9.

---

## 4. The first 30 minutes

The first 30 minutes cover **M01, M02, and most of M03**. A human will spend about 30–45 min there before the Blood Arts open [Inferred from M01 4:06 and M02 par 8:00 for an expert].

**What the player does:**
- walks in shadow, reads cones, reads lamp hints;
- feeds from behind;
- carries and dumps bodies in the canal;
- hides in hide spots.

**What the player does not do: anything a human can't do.** No power, no vampire verb beyond feeding.

| Minute | What happens | Fun |
|---|---|---|
| 0–5 | M01 tutorialised movement, cones, feeding | OK. Feeding from behind is satisfying and quick |
| 5–15 | M01 completion; hub; "2 unspent Marks. See Blood Arts." | **Confusing.** The Blood Arts button is *disabled*, with the tooltip "The Abbess has not yet spoken to you." The hub tells you to use a tab it won't let you open [Code UIHub.cs:90, :195] |
| 15–30 | M02 Lantern Street: lamps, roofs, bodies | Good level hints [m02 script 224–247]. But the first time you're seen, either nothing happens (you can still win) or the whole map turns on you, depending on the alarm bugs |

**Benchmark.**
- Mark of the Ninja gives its first tool in about 5 min.
- Dishonored gives Blink in its first real mission.
- Aragami gives the teleport in its first level.

Holding the vampire fantasy back for about 40 minutes is the single most expensive pacing decision in the game. It is also the window in which demo players and refund-window players decide.

---

## 5. Moment-to-moment feel and WASD (provisional, no hands-on session)

**What can be judged from code and logs:**
- Three gaits: sneak, walk and run.
- Carrying a body gives 0.9× speed. Starving gives 1.1× speed plus a 3 m heartbeat every 1.3 s.
- Feeding is a timed channel; drain takes 3.2 s.
- Abilities are "walk into range, then cast": Pounce, Rend, Hemorrhage, Mesmerize, Smother and Shadowstep path into range first. [Code VampireAbilities.TickPendingAbility]

**Risks specific to WASD that a human session must test:**
1. **Auto-walk into range** is a click-to-move behaviour inside a WASD game. If you aim Pounce at someone 9 m away, the vampire walks the extra 2 m **on its own route**. That was the failure I saw with Feed: the pending action carried the character into danger without input. Under direct control, players expect the character to go where the keys say.
2. **Hidden near-band rule.** Moving at 4–5 m from a guard in "darkness" got me seen twice. WASD players hug cones far more tightly than click-to-move players, because they steer by feel. They will hit this constantly.
3. **Shadowstep targeting** with a mouse cursor while WASD moves the body. Mimimi's lesson is that the aim mode must not fight the movement keys.

**WASD verdict (provisional):** the right choice for a *vampire*, because direct control sells the predator body far better than click-to-move. It is the wrong choice for *puzzle-precision stealth* with soft cone edges. The game wants WASD, so the stealth rules have to become WASD-tolerant: a visible near-band ring, forgiving cone edges, and no auto-pathing for actions.

---

## 6. The stealth loop

**The designed loop:** observe → choose a target or route → darken or avoid → feed or pass → hide the evidence → manage blood.

**The loop as played:**
- **Act I:** observe → walk → feed from behind → (if seen, keep walking to the objective).
- **Mid-game:** observe → Shadowstep past the problem → (if seen, die; quickload).

**What works:**
- **Blood economy pressure.** Healing costs blood and only works in the dark. Starving makes noise. That gives the "should I feed now?" decision real teeth. [Code Vampire.cs:238–257]
- **Darkness as real cover beyond the near band.** Waiting in the dark stable while searchers passed at 9 m was the best stealth moment in testing.
- **Body handling with real options:** canal (7 m splash), hide spot, or leave it.

**What breaks:**
- The detection-response loop is binary at map scale (§8, §9). Getting seen never produces a *local* problem to solve. It produces a global state.
- The gap between "I am unseen" and "everyone knows" is one guard and 0.2 s.

---

## 7. Decision density

**Counting real decisions per minute, careful play (M02, [Played]):**

| Decision | Frequency | Real? |
|---|---|---|
| Which route: street, roofs or quay | Once per area | **Yes** |
| Feed now or later | Per target | Yes (blood versus time versus evidence) |
| Sip or drain | Per target | Weak. Sip is strictly better while the dazed bug exists |
| Hide the body or not | Per kill | Yes |
| Snuff a lamp (once you have Smother) | Several per area | Yes, until Dossier cm_caged answers it |
| What to do when seen | Per detection | **No.** Act I: keep going. Mid-game: reload |

**Mid-game (M07):** the biggest decision ("which of the 3 crossings, or the ferry?") is deleted by Shadowstep, which lets you cross anywhere.

The density is decent between incidents and collapses to zero at the moments that should be most intense.

---

## 8. Stealth failure

This is the most important section.

**Act I (M01–M03): detection is consequence-free.**
- [Played] Spotted 7×, 2 alarms, HP 49 → 2.
- I still won M02 faster than par and earned "Unbroken".
- The target's door works as a safe zone.
- Rewards come only from first completion, optionals and secrets [Code MissionController.cs:910–940]. **Nothing in the reward code reads sightings or alarms.** Challenges are cosmetic records.

**Mid-game (M07): detection is fatal.**
- [Played] Detection → global alarm (all 48 NPCs) → flares light you to 1.0 → hunters converge → dead in about 10 s, twice.
- With no break-contact tool equipped, there is no recovery play. The answer is quickload.

**Late-game (A9 with Apex): detection is a choice to fight.**
- [Played] Survivable and thrilling, but only with full blood.

**Comparison:**
- **Styx** was punished for "detected = reload".
- **Aragami 2** was punished for "detected = nothing".
- **Vespertine has both, in sequence.**
- **The well-liked middle ground is Invisible Inc and Dishonored:** detection costs escalating resources and makes the mission harder, but it can be survived.

**What it should be:** alarms that start **local** and escalate visibly by stages:
1. **Suspicion.** A local investigation.
2. **Hunt.** The guard group plus nearby posts, about 15–25 m. Lamps relit, flares.
3. **Lockdown.** Exits closed, inquisitors spawn, the Dossier gains a habit.

Escaping from stage 2 should be a designed play (go dark, mist, threshold, canal). Stage 3 should cost rewards (no "Unbroken", Dossier weight) without killing the run.

Most of this machinery already exists: Escalate levels, `LockdownThreshold`, Wary radius, hunters' flares. **The shout bug (§9) is what flattens it into "everyone, instantly".**

---

## 9–10. AI

### Critical bug A: shouts reach about 225 m on Hunter (270 m for screams)

[Code AI/AIDirector.cs, `Shout` and `HearNoise`]

```csharp
float r = 15f * Difficulties.Current.ShoutRadius;   // ShoutRadius is already metres (10/15/22) → 225 m on Hunter
...
var diffMul = kind == NoiseKind.Scream || kind == NoiseKind.Voice ? Difficulties.Current.ShoutRadius : 1f;
float r = radius * diffMul;                          // an 18 m scream × 15 → 270 m
```

- **Intended:** 10 / 15 / 22 m. GAME_DESIGN.md:319 says "Shout radius | 10 m | 15 m | 22 m"; ENEMIES.md:67 says shouts alert "within 15 m (difficulty-scaled)".
- **Actual:**
  - shouts reach 150 / 225 / 330 m;
  - screams from NpcStates.cs:143 and NpcSquad.cs:54 (18 m) reach 180 / 270 / 396 m;
  - escort screams (NpcEscort.cs:292, 16 m) reach 240 m on Hunter.
- The occlusion check only applies beyond 60% of the radius (135 m on Hunter), so walls don't help.
- **Evidence:**
  - M02: one sighting turned all 16 NPCs Searching or Panicked within 0.2 s;
  - M07: all 48 NPCs, twice;
  - M07 late test: one tanner panicking at (7,42) set off guards at (21,5), (52,35) and (55,35), 50–70 m away, in one frame.
- **Consequences:** every system designed around local alarms is invisible. That includes guard groups, `Split`, the Wary radius, escalation tiers, and the lockdown threshold (which can only bite once everyone already knows). It explains the mid-game death spiral and why "humans hide from me" only exists with Apex.
- **Fix (one line each):**
  - Shout: `float r = Difficulties.Current.ShoutRadius;`
  - HearNoise: use `ShoutRadius / 15f` as the multiplier.

### Critical bug B: sipped and dazed victims never wake, and report every frame forever

[Code AI/NpcInteractions.cs:156–178, AI/NpcStates.cs:193, AI/AIDirector.cs:135–142]

```csharp
case NpcState.Dazed: if (_t >= _stateDuration) WakeUp(true); break;   // every frame once the timer expires
...
public void WakeUp(bool report, Npc by = null) {
    if (State != NpcState.Dazed || Carried) return;
    ... if (report && _fromFeed ...) Game.AI?.ReportWitness(this);   // reports
    if (Civilian) EnterPanic(...); else EnterSearching(...);         // both return early: Incapacitated includes Dazed
}
```

`EnterPanic` and `EnterSearching` both bail out on `Incapacitated`, and `Incapacitated` includes `Dazed`. So the state never changes, and WakeUp runs again the next frame.

**Each frame, `ReportWitness` does three things:**
- `WitnessReports++`;
- `AddHabit(Sightings, 0.5)` (weighted ×2);
- `Escalate(1)`, which also zeroes the alarm's calm timer, so **the alarm can never fall back to 0**.

It also marks everyone within 15 m Wary.

**Evidence [Played]:**
- WitnessReports went 0 → 8,143 → 9,982 over about 70 s after one sip.
- The campaign Dossier holds **sightings = 20,764** after only three missions.
- Alarm stuck at 1.
- The M07 debrief applied **cm_inquest** ("Inquisitors examine the living: dazed victims wake in 25 s"), driven by this flood rather than by my play.

**Consequences:**
1. Sipping removes a human from the mission **permanently** (they never get up), so the "mercy" option is strictly stronger than draining.
2. **Every campaign gets cm_inquest at M07** (DossierStartsAt = 6) as soon as the player sips once. The adaptive enemy is not adapting; it is reacting to a counter overflow.
3. The alarm is pinned at 1 for the rest of the mission after any sip.
4. A dazed victim found by an NPC [NpcStates.cs:412 `t.WakeUp(true, this)`] enters the same loop.

**Fix:**
- Set the state before routing, for example `SetState(NpcState.Relaxed)` (or a "Groggy" state) before `EnterPanic`/`EnterSearching`.
- Add a guard so `ReportWitness` fires once per victim.
- Then **wipe affected Dossier data** (dev saves only; it is a dev build).

### What the AI does well
- **Searchers who don't cheat.** They passed my dark hiding spot at 9 m. [Played]
- **Inquisitors who examine bodies and read the cause.** Sip → Sips habit; drain → Lethal; hemorrhage → Blood. This is real systemic storytelling. [Code Npc.cs:496–507]
- **Hunters with flares.** A counter to darkness that scales: exactly what Aragami lacked. [Played]
- **Bodies make people Wary within 20 m, and enough of them trigger lockdown.** Readable escalation, once shouts are local.
- **Thrall exposure.** An officer who spots a thrall shouts "The mark of the leech!" and frees them. Good theatre. [Code NpcInteractions.cs:425]

### What the AI does badly (apart from the bugs)
- **Near band:** sees you at about 5 m in any light. Correct as a rule, but **invisible**. Give it a ring.
- **Panicked civilians** are effectively alarm sirens. With global shouts, one tanner who noticed a running vampire in the dark woke 13 NPCs. After the fix they become local again, which is fine.
- **Pounce on a Searching NPC** returned success but didn't start a feed in one M07 attempt. *Not reproduced, so verify.* [Played, single occurrence]

---

## 11. Vampire fantasy

| Fantasy beat | Delivered? |
|---|---|
| Hunting from shadow and feeding | **Yes.** Feeding from behind is fast, quiet and rewarding |
| Light is the enemy | **Yes.** Regen only in the dark; lamps; Smother; Eclipse |
| Hunger | **Yes.** Starving heartbeat and speed. A great, audible tell |
| The bite has consequences | Partial. Sip versus drain and the Dossier exist, but the bug distorts them |
| "Humans hide from me" | **Only at Awakening 9+ with full blood, and through Apex** |
| Vampire folklore (threshold, running water, invitation, mirrors, holy ground) | **Thin.** Holy auras and silver exist. Running water is the theme of M07 ("The Toll Bridges"), and **Shadowstep crosses it** *(Corrected: see I-04.)* |
| Mind dominion | **Yes, on paper.** Beckon, Mesmerize, Thrall, False Orders. Not field-tested here |

**Biggest missed opportunity:** *vampires cannot cross running water.* M07 is a map of rivers, toll bridges and a ferry. It is begging for "you **must** find a bridge, ferry or culvert; Shadowstep and Mist cannot cross running water". That one rule would:
- fix the M07 bypass *(Corrected: see I-04.)*;
- make the map's design matter;
- give the world a vampire rule players will remember.

---

## 12. Progression

**Structure:**
- Vitae raises Awakening 1–10, which raises max HP (40 → 120), max blood (60 → 150) and loadout slots (2 → 6).
- Marks buy nodes in 4 trees (42 nodes, 66 Marks total); tiers are gated at Awakening 1 / 3 / 5 / 8.

**Problems:**
1. **Nothing to buy for three missions.** The tree is locked until after M03, while the hub nags you to spend Marks.
2. **The Predator style over-earns.** [Sim] Predator reaches **Awakening 10 at M10** and has **71 Marks against a 66-Mark tree** by M14. It buys everything and still has spare Marks.
   - The last 4 missions offer no growth for the aggressive player.
   - The lethal playstyle, the one the fantasy ends on, is the one that runs out of progression.
3. **The Ghost style trails.** Ghost reaches A8 at M13. Tier 4 capstones open at Awakening 8, so a pure ghost gets their capstone for the last two missions only.
4. **CAMPAIGN.md's power-curve table** (A10 after M14) matches only the Typical simulation. Both extremes leave the documented curve.
5. **First-clear-only rewards** make replays worthless for progression [Code MissionController.cs:912–922]. That is fine for anti-grind, but challenges should then pay something.

---

## 13. Skills and abilities

**Strong:**
- **Pounce** (snare then feed; height bonus): a great predator verb, with roofs as a reason to be up high.
- **Rend + Apex:** the fight-back fantasy works [Played].
- **Smother + Lingering Dark + Black Main:** a light-manipulation chain with real map consequences.
- **Mesmerize → Thrall → False Orders / Puppet Strike:** a genuine squad-lite layer.
- **Blood Snare + False Trail:** a trap and lure with a hound counter-interaction.

**Problem abilities:**
- **Shadowstep:** 12 m, dark destination, LOS, 12 blood, 1 s cooldown.
  - It deletes map topology: water gaps, walls with LOS over them, pier gaps [Played M07]. *(Corrected: see I-04.)*
  - This is the Dishonored Blink / Aragami problem verbatim.
  - The cost of 12 blood is trivial once a single drain gives +40.
- **Hemorrhage:** a silent ranged kill that leaves a huge blood pool. It overlaps Pounce (silent take-down) and Rend (kill). Its niche is "kill at 10 m silently", which is strong.
- **Corpse Puppet / Living Lie:** overlap the thrall layer at higher complexity.
- **Court of Night:** Mesmerize in an area. Overlaps Gloom + Shroud and Apex.
- **Bloodmend:** an instant 50% heal anywhere. It undercuts the best rule in the game: you heal only in darkness, by paying blood.

---

## 14. Dominant strategies

| Phase | Dominant strategy | Why |
|---|---|---|
| Act I | **Ignore detection and walk to the objective** | Alarms have no reward or objective consequence; the door is a safe zone |
| All | **Quicksave after every guard** | F5 any time, no save-age pressure, no rotating slots |
| All | **Sip everything** (while bug B exists) | The victim never wakes; no body; counts toward "Merciful Hunger" |
| Mid-game | **Shadowstep past every chokepoint** | No terrain restriction; cheap |
| Late (Predator) | **Apex → Rend chains** | Refunds blood per kill; 35% world speed |

After the bug fixes, the only *design* dominants left are quicksave and Shadowstep. Those are the two to design against.

---

## 15. Ability overlap table

| Job | Abilities that do it | Verdict |
|---|---|---|
| Remove an unaware human silently | Feed (sip/drain), Pounce, Hemorrhage, Snare, Puppet Strike, Shroud of Sleep | **Too many.** Six ways. Cut or merge Hemorrhage |
| Kill an aware human | Rend, Apex (enabler), Puppet Strike | Fine |
| Create darkness | Smother (one light), Gloom (sphere), Eclipse (25 m area) | A good ladder |
| Move past obstacles | Shadowstep, Mist, Bound, Between Bars mod | **Shadowstep dominates.** Mist and Between Bars are both "pass bars" |
| Neutralise vision | Mesmerize (1), Court (area), Gloom (cones can't pierce), Eclipse (blind), Shroud (sleep) | **Too many.** Five tools for one job |
| Manipulate positions | Beckon, False Orders, Thrall, Blood Snare + False Trail | Distinct enough |
| Information | Blood Sense (+free mod), Scent of Blood | Two info passives. Merge Scent into Sense |
| Healing | Regen in dark (core), Bloodmend, Nightblood, Vessel | Bloodmend undermines the core rule |

**Simplify toward about 30 nodes:**
- merge Scent into Blood Sense;
- cut or rework Bloodmend;
- merge Between Bars into Mist;
- fold Court of Night into Mesmerize as a capstone mod;
- remove Hemorrhage or Corpse Puppet (keep one "evidence-horror" tool).

---

## 16. Blood economy

**Good:**
- Blood is HP-repair currency, ability currency **and** a noise liability (starving).
- Drain gives +40 for about 3 s of risk.
- Regen is paid 1 blood per HP and capped at 15% reserve.

That's a coherent triangle.

**Problems:**
1. **Missions start low on blood** (50% of max by default; M07 sets a flat 30, which was 30/110 at Awakening 6 and 30/140 at Awakening 9). That's good tension early. Late capstones (Apex 45, Eclipse 45, Court 40), however, can't be cast at mission start. The capstone fantasy depends on feeding first, which is *fine*, as long as the first feed is reachable without the power.
2. **Sip versus drain isn't a real trade-off yet.**
   - Sip gives 60% of the Vitae and leaves a dazed victim who (when not bugged) wakes and reports.
   - Drain gives full blood, a corpse and the Lethal habit.
   - With bug B, sip has no downside at all.
   - After the fix, the Lethe mod (no report) makes sip strictly safer. Fine, if that's the Dominion path's payoff.
3. **Apex kills refund 15 blood each**, which makes Rend chains during Apex nearly free (95 → 80 over three kills in my test).

---

## 17. Encounters

I tested M02 and M07 directly; the rest is read from map files.
- **M02:** a posted guard at the yard door, lamp-lit street, stable, quay bypass. Good teaching density.
- **M07:** four banks, three Leat crossings, a toll-bridge lever, a ferry gated on downing the ferryman, 48 NPCs including hunters and hounds. Optionals are faithful, manual, **quiet (no lockdown)** and shrine.

**Verdict:** the encounter writing is the strongest part of the project. M07's optional "quiet" is a direct answer to the alarm problem, but it only works once shouts are local.

## 18. Level design

**Strong:**
- Real alternate routes: the M02 quay runs parallel to the street the whole length; M07 has multiple crossings.
- The map script hints are good teaching ("snuff this lamp", "use the roof").
- Every map is distinct (14 titles, 14 layouts).

**Weak:**
- Designed chokepoints don't account for the player's movement power. M07's bridges are mandatory only for a player without Shadowstep, who doesn't exist by M07.
- **The level-design contract needs a rule-set of "what Shadowstep and Mist cannot do"**, the way Dishonored 2 had to make every space reachable without powers. Running water and holy ground are the cheapest, most on-theme options.

## 19. Creativity

The toolkit supports creative play:
- Beckon a guard to the canal edge, Pounce from a roof, dump the body in the water;
- Thrall a guard and use False Orders to open a post.

The limiting factor is not tools but **the incentive to be creative**. When one Shadowstep solves a crossing and detection costs nothing (Act I) or everything (mid-game), the creative middle is never needed.

## 20. Emergence

**Good seeds:**
- Inquisitors reading the cause of death.
- Bodies → Wary → lockdown.
- Hounds sniffing Snare runes (False Trail).
- Hunters' flares versus your darkness.
- The Dossier changing the next mission.

**Blocked by:** global shouts. Emergence needs **locality**: things happening in one part of the map that the player can watch and exploit. Right now any event becomes a map-wide state in one frame.

## 21. Challenge

| Difficulty | Detection | Shout (intended) | Shout (actual) | Cones | Autosave |
|---|---|---|---|---|---|
| Merciful | ×0.7 | 10 m | **150 m** | all | frequent |
| Hunter | ×1.0 | 15 m | **225 m** | all | objectives |
| Apex | ×1.3 | 22 m | **330 m** | 20 m only | start only |

- The challenge curve is **inverted at the start and a cliff in the middle**: Act I has no failure state, while M07 at Awakening 6 kills in 10 s.
- Hunter's description, "Mistakes are recoverable, but costly", is currently false at both ends.

## 22. Pacing

| Phase | Missions | Pacing issue |
|---|---|---|
| Act I | M01–M03 | Powerless for about 40 min; no fail pressure |
| Act II | M04–M07 | Powers arrive fast; Shadowstep makes M07's structure moot |
| Act III | M08–M11 | Predator maxes Awakening at M10 [Sim] |
| Finale | M12–M14 | Typical play reaches A10 only at M14; Ghost reaches A8 at M13 |

Fix the start first: open the Arts after M01 and give Smother or Shadowstep during M02.

## 23. Tedium

| Source | Comparable complaint |
|---|---|
| Quickload loops at mid-game (detection = death) | Shadow Tactics' #1 negative theme |
| Waiting for patrol windows with no setup tool | Shadow Tactics |
| Walking back after an auto-pathed feed chase | Mine, from M02 |
| Long missions (M07 has 48 NPCs) with only a mission-start autosave on Apex | Commandos: Origins was punished for load times |

## 24. Replayability

- Challenges are cosmetic, and rewards are first-clear only, so the replay incentive is weak.
- **Benchmark:** Mimimi badges are cosmetic too but hidden and conflicting. Desperados' Baron's Challenges remix maps. Hitman's mastery unlocks tools.
- **Cheapest fix:** each challenge earned for the first time gives 1 Mark, or a Vitae grant, or unlocks a start point or thrall type in that mission.

## 25. Mission variety

From titles and scripts:
- infiltration (M02 Lantern Street);
- hunt (M03 Fishmarket, M08 Hollin's Hunt);
- crossing (M07);
- social stealth (M06 Masquerade, M11 Opera);
- industrial (M10 Gasworks);
- fortress (M12 Vane's Bastion);
- endurance (M13 The Long Night);
- boss (M14 The Abbess Beneath).

That's real variety on paper. Not verified in play.

---
## 26. Player behaviour prediction

| Player type | Predicted behaviour today | Result |
|---|---|---|
| Mimimi veteran | F5 every 20 s, reads every cone, sips everything | Finds Act I trivial; finds M07 solved by Shadowstep; complains that challenges mean nothing |
| Vampire-fantasy buyer | Plays aggressively, wants to bite people | Bored for about 40 min without powers; gets spotted and is surprised that nothing happens; then hits the mid-game death wall |
| Pad player | Steers by feel along cone edges | Hits the invisible near band constantly (provisional) |
| Completionist | Replays for challenges | Finds no reward and stops |

## 27. The optimal boring player

The player who minimises risk and effort:
1. Quicksave before every guard.
2. Sip everything. While bug B exists, victims never get up.
3. Skip every designed crossing with Shadowstep.
4. Never fight.

This player finishes with the best rewards and has the least fun. **Every one of those four steps should be made less optimal:**
- a save-age reminder plus a "no loads" challenge that pays;
- sipped victims wake up and report;
- running water blocks Shadowstep (the aimed cast already does; see I-04);
- the Apex/Rend path gets its own payoff (Predator's Marks).

## 28. The chaotic player

The player who just bites everything:
- **Act I:** wins, and is rewarded for winning fast.
- **Mid-game:** dies in 10 s, repeatedly, and probably quits around M05–M07 without understanding why the rules changed.
- **Late-game:** if they survive to Apex, they finally get the fantasy.

**The chaotic player's journey is the game's real tutorial problem: it teaches the wrong lesson early, then punishes it at full strength.**

## 29. Motivation

| Motivator | Present? |
|---|---|
| Mastery (getting better at the systems) | Yes, but quickload-shaped |
| Power growth | Yes, after M03 |
| Story and lore | Present (codex, ledger lore pop-ups, Abbess); not judged |
| Curiosity (secrets, optionals) | Yes. Secrets and optionals pay Marks |
| Expression (playstyle identity) | **Strong on paper**: four trees plus a Dossier that reads your style. It is the game's real hook, once bug B is fixed |

## 30. Rewards

- **What pays:** first completion, optionals, secrets, notable drains (each grants a Mark when fed).
- **What doesn't pay:** challenges, clean play, speed, no alarms, no loads.

**The reward structure is indifferent to *how* you play, in a game whose whole identity is how you play.** Fix that before adding anything else.

---

## 31. Benchmark matrix (0–10; Vespertine scored as played)

| Dimension | Shadow Tactics | Desperados III | MotN | Styx 2 | Dishonored | Aragami | **Vespertine** |
|---|---|---|---|---|---|---|---|
| Detection readability | 9 | 9 | 10 | 6 | 7 | 5 | **6** |
| Fair failure / recovery | 7 | 7 | 8 | 4 | 7 | 5 | **3** |
| Local, legible AI response | 8 | 8 | 9 | 5 | 7 | 4 | **2** (shout bug) → 7 after fix |
| Toolkit depth | 8 | 9 | 7 | 6 | 9 | 5 | **8** |
| Power vs stealth balance | 8 | 8 | 8 | 4 | 5 | 3 | **4** |
| Level route density | 9 | 9 | 7 | 7 | 9 | 6 | **7** (M02, M07) |
| Resource tension | 5 | 6 | 6 | 6 | 6 | 8 | **8** |
| Replay structure | 8 | 9 | 8 | 5 | 7 | 5 | **3** |
| Fantasy delivery | 7 | 7 | 8 | 7 | 9 | 8 | **6** |
| Adaptive opposition | 3 | 3 | 4 | 3 | 5 | 2 | **6** (Dossier; bugged) |
| Early pacing (first 30 min) | 8 | 8 | 9 | 6 | 8 | 8 | **4** |

Vespertine's peaks (toolkit, resource tension, adaptive opposition) are genuinely above the genre. Its troughs are basic fairness items the genre treats as **required**.

## 32. Commercial expectation over time

| Time | What a player experiences today | Likely player verdict |
|---|---|---|
| **15 min** | M01: shadows, cones, feeding, no powers | "Competent Shadow Tactics-like with a vampire skin. Where are the powers?" |
| **1 h** | M02–M03, still no powers; detection costs nothing; the hub says spend Marks but won't let you | Refund risk. "The vampire game where I'm not a vampire yet" |
| **3 h** | Powers arrive; M04–M06 | The game finally shows its identity. First global alarms at full strength |
| **Mid-game (M07–M09)** | Shadowstep bypasses map structure; detection kills in 10 s; the Dossier countermeasure is fixed (cm_inquest) by the bug | Mixed. "Powers trivialise it, mistakes are instant death, I quickload constantly" |
| **Endgame (M12–M14)** | Predator is maxed since M10; Apex and Rend deliver the fantasy | "The last few hours are what I wanted from the start" |

That timeline is backwards for Steam reviews: most reviews are written within the first 2–5 hours.

## 33. "Why not just play Shadow Tactics / Aragami / Dishonored?"

**Honest answer today:** because none of them have a blood economy that makes healing a stealth decision, an enemy dossier that adapts to how you hunt, or a protagonist who becomes the predator.

**Honest answer from a player's first hour today:** there's no reason yet. Those three differentiators are invisible for the first 40 minutes (no powers, Dossier from M07) or broken (bug B).

**The answer has to be visible in the first 15 minutes**, in the demo:
- feed;
- heal in the dark;
- snuff a lamp;
- watch a guard examine a body and say what killed them.

## 34. Fun autopsy

**5 most fun moments (observed or directly evidenced):**
1. Waiting in the dark stable while searchers pass at 9 m. Pure, readable tension.
2. Apex Hunt plus a Rend chain through a guard post during a global alarm (6 kills, 25 s, 29 HP left).
3. Draining a posted guard from behind for +40 blood. Fast, quiet, satisfying.
4. Shadowstepping a river gap. The power feels great, and that is exactly why it's a problem.
5. The debrief line "THE VIGIL ADAPTS: Inquisitors examine the living". A great beat, even though bug B earned it.

**5 least fun moments:**
1. The whole map turning on you within 0.2 s of one guard seeing you.
2. Dying 10 s after detection, twice, with no break-contact play available.
3. A feed auto-chase that carried the vampire across the map into a guard.
4. Completing M02 while spotted 7× and getting "Unbroken". The game told me failure didn't matter.
5. The hub saying "See Blood Arts" while the Blood Arts button is disabled.

**5 underused systems:**
1. **The Dossier.** It starts at M07, and the counter flood drives it.
2. **Thralls.** A squad layer the missions don't demand.
3. **Body examination.** Inquisitors read the cause of death. Make this visible to the player (a bark plus an HUD line).
4. **Lockdown.** Only reachable after the whole map is already alerted.
5. **The starving heartbeat.** Great; make it a visible ring.

**5 missed opportunities:**
1. **Running water.** M07 is literally about it.
2. **Threshold and invitation.** Doors that need a thrall or an invitation to cross.
3. **An in-world save fiction**, for example "Memories" in blood, like Shadow Gambit's ship.
4. **Hunted-to-hunter flip as a mechanic**, for example Terror making guards *flee you* at the late game, so humans literally hide.
5. **A Shadow Mode equivalent.** Apex's time-slow is one already; give a weaker version earlier.

## 35. Brutal questions

**Is WASD right for this design?**
Yes for the fantasy, no for the current rules. WASD needs:
- a visible near band;
- forgiving cone edges;
- no auto-pathing actions.

Without those, WASD stealth with gradient light reads as unfair.

**Is the game fun right now?**
In patches: about 15 of the first 60 minutes, and much of the late game. The middle is a quickload loop.

**Would a Mimimi fan finish it?**
Not at present. They'll see the one-sighting global alarm as broken AI and the bypassable M07 as broken level design.

**Would a vampire fan finish it?**
They'd leave in Act I for lack of powers.

**Is the toolkit too big?**
Yes. 42 nodes for one character, with five tools to neutralise vision and six to silently remove a human.

**Does the Dossier work?**
The idea is the best thing in the design. The implementation is driven by a counter overflow today.

**Is it fixable?**
Yes. The two worst problems are one-line bugs. The design problems (alarm middle ground, Shadowstep limits, Act I pacing, challenge rewards) are each days of work, not months.

---

## 36. Scores

| Category | Score /100 | Notes |
|---|---|---|
| Core stealth loop | 55 | Good darkness and hiding; collapses on detection |
| Moment-to-moment feel and WASD | 55 *(provisional)* | Untested by hand; auto-pathing and near band are risks |
| Readability and feedback | 52 | On-demand cones good; near band and global alarms unreadable |
| AI | 38 | Two critical bugs; strong examine, flares and search logic |
| Stealth failure and recovery | 25 | Free in Act I, fatal in mid-game |
| Vampire fantasy | 52 | Feeding and hunger great; powers late; folklore thin |
| Abilities and toolkit | 62 | Deep, but bloated and with one dominant |
| Progression | 48 | Locked for 3 missions; Predator maxes at M10 |
| Blood economy | 68 | The best system; sip versus drain distorted by bug B |
| Level design | 64 | Real routes; doesn't respect movement powers |
| Encounter design | 66 | M07 optionals and crossings are well designed |
| Emergence | 50 | Good seeds, blocked by global alarms |
| Challenge curve | 30 | Inverted start, mid-game cliff |
| Pacing | 40 | About 40 min powerless |
| Replayability | 32 | Challenges unrewarded; first-clear-only rewards |
| Adaptive opposition (Dossier) | 45 | Excellent concept; bug-driven data |
| Commercial readiness | 35 | Fails required fairness items |

### **OVERALL GAMEPLAY SCORE: 47 / 100**

- With only the two critical bug fixes (§9): about **56**.
- With the Top 10 below: a credible **70–75** game, competitive with Sumerian Six and Aragami 1 and approaching Mimimi on systems, but not yet on polish.

## 37. Industry readiness

| Milestone | Ready? | Blocking |
|---|---|---|
| Internal playtest | **Yes** after the two bug fixes | n/a |
| Public demo (M01–M02) | **No** | No powers in the demo; detection costs nothing; Blood Arts UI contradiction |
| Early Access | **No** | Failure model, Shadowstep dominance, challenge rewards |
| 1.0 | **No** | All of the above plus a hands-on feel and readability pass |

## 38. Issue tiers

| Tier | Issues |
|---|---|
| **P0: broken** | I-01 Shout radius ×15; I-02 Dazed wake loop |
| **P1: design-critical** | I-03 Alarm has no middle ground; I-04 Shadowstep bypasses designed structure *(corrected: harness artefact; see I-04)*; I-05 Act I powerless; I-06 Act I detection is consequence-free |
| **P2: significant** | I-07 Challenges unrewarded; I-08 Feed auto-chase has no cap *(corrected: overstated)*; I-09 Predator economy overflow; I-10 Toolkit overlap; I-11 Near band invisible; I-12 No save-age reminder or rotating slots |
| **P3: polish** | I-13 Buff timer shows "-2147483648s"; I-14 Blood Arts tab disabled while hub prompts; I-15 Pounce on Searching NPC didn't feed (verify) |

## 39. Issues in detail

### I-01: Shouts and screams reach the whole map
- **Severity:** P0
- **Problem:** `AIDirector.Shout` computes `15f * ShoutRadius`, but ShoutRadius is already in metres (10/15/22). `HearNoise` multiplies scream and voice radii by ShoutRadius too.
- **What happens now:** 150 / 225 / 330 m shouts; an 18 m scream becomes 270 m on Hunter. One sighting alerts every NPC on the map within a frame.
- **Why it hurts:** it invalidates local escalation, guard groups, lockdown and emergent play, and it produces the mid-game death spiral.
- **Evidence:** [Code] AIDirector.cs `Shout` and `HearNoise`. [Doc] GAME_DESIGN.md:319, ENEMIES.md:67. [Played] 16/16 NPCs in M02 and 48/48 in M07, within 0.2 s.
- **Industry comparison:** Styx's "psychic AI" complaints; Mimimi alarms are local, with a visible shout radius.
- **Likely player behaviour:** concludes the AI is broken or cheating, then quickloads after every sighting.
- **Recommended fix:**
  - `float r = Difficulties.Current.ShoutRadius;`
  - in HearNoise use `ShoutRadius / 15f`;
  - draw the shout radius as a ring when it fires.
- **How this improves fun:** detection becomes a local, solvable problem. Every escalation system starts working.
- **Implementation scale:** minutes, then a retune pass.

### I-02: Dazed victims never wake and report every frame
- **Severity:** P0
- **Problem:** `WakeUp` routes through `EnterPanic`/`EnterSearching`, which reject `Incapacitated` (that includes Dazed), so the NPC stays Dazed. The Dazed tick calls `WakeUp` again every frame, and each call runs `ReportWitness`.
- **What happens now:**
  - sipped and dazed NPCs are removed permanently;
  - the alarm is pinned at 1;
  - Sightings in the Dossier grows by about 2 per frame (20,764 after 3 missions);
  - cm_inquest is guaranteed.
- **Why it hurts:** it makes sipping strictly dominant, corrupts the Dossier (the game's signature system) and breaks alarm decay.
- **Evidence:** [Code] NpcStates.cs:193, NpcInteractions.cs:156–178, AIDirector.cs:135–142. [Played] WitnessReports 0 → 9,982 in about 70 s from one sip; Dossier sightings 10,782 → 20,764.
- **Industry comparison:** the MGSV Revenge system works because its inputs are honest.
- **Likely player behaviour:** sips everything, then is puzzled why inquisitors arrive whatever they did.
- **Recommended fix:**
  - set the state (Relaxed or a new Groggy state) before routing;
  - fire `ReportWitness` once;
  - add a regression check (a dazed NPC has left the Dazed state ≤1 s after its timer).
- **How this improves fun:** sip versus drain becomes a real choice, and the Dossier reflects the player.
- **Implementation scale:** an hour.

### I-03: Detection has no survivable middle ground
- **Severity:** P1
- **Problem:** detection either costs nothing (Act I) or kills (mid-game). There is no designed "hunt" state you escape from.
- **What happens now:**
  - M02: spotted 7×, still rewarded.
  - M07: dead within 10 s, twice.
- **Why it hurts:** this is the exact Styx/Aragami 2 failure pair, and it makes quickload the core loop.
- **Evidence:** [Played] M02, M07. [Code] no alarm or sighting terms in the reward code (MissionController.cs:910–940).
- **Industry comparison:** Invisible Inc's alarm levels; Dishonored's survivable combat.
- **Likely player behaviour:** reckless in Act I, savescumming from M04.
- **Recommended fix:**
  - after I-01, design three stages: **Suspicion → Hunt (local) → Lockdown (map)**;
  - give Hunt a clear "break contact" condition: out of LOS for N s in darkness, or crossing a threshold;
  - make Lockdown cost Dossier weight and challenges, not life;
  - make flares local and visibly telegraphed.
- **How this improves fun:** getting seen becomes a fun chase instead of a reload.
- **Implementation scale:** days.

### I-04: Shadowstep bypasses designed map structure
> **Correction:** a harness artefact. The bot's direct `Cast` skipped `ValidateAim`, which already blocks running-water crossings (`CrossesWater` returns true for all three crossings below). Remaining issues: `Cast` itself doesn't check water (latent bug; fix in hours); wall/fence skipping is unverified; and the cursor-aimed teleport conflicts with WASD. Severity of the bypass as described: **not reproducible by a player**. The redesign replaces Shadowstep with a navmesh-bound Shadow Dash (GAMEPLAY_REDESIGN.md §11, P6).

- **Severity:** P1
- **Problem:** a 12 m LOS teleport to any dark point, with no terrain restriction.
- **What happens now:** M07's three Leat crossings, toll bridge, ferry and Fen Cut are bypassed by three casts (36 blood). The mission was done in 1:15.
- **Why it hurts:** level design work is deleted; it repeats the Blink/Aragami "trivialised" reviews.
- **Evidence:** [Played] M07 geometry test, (16,46)→(21,45), (43,35)→(47,35), (58,38)→(62,38).
- **Industry comparison:** Dishonored Blink; Aragami teleport; Ereban merge.
- **Likely player behaviour:** uses Shadowstep for everything.
- **Recommended fix:**
  - **Running water blocks Shadowstep and Mist.** Folklore, readable and on-theme, and M07 becomes the mission that teaches it.
  - Optionally, cost scales with distance, or there's a short exhaustion in which regen stops.
- **How this improves fun:** maps keep their puzzles, the vampire gains a rule, and creative crossings (ferry, bridge, thrall) matter.
- **Implementation scale:** days, including a level pass marking water volumes.

### I-05: First three missions have no powers
- **Severity:** P1
- **Problem:** `ArtsOpen => MissionIndex >= 3`.
- **What happens now:** about 40 min of play with a human's toolkit.
- **Why it hurts:** the demo and refund window miss the core fantasy.
- **Evidence:** [Code] CampaignState.cs:192.
- **Industry comparison:** Mark of the Ninja, Dishonored and Aragami all give the signature power early.
- **Likely player behaviour:** "where's the vampire?" Refunds.
- **Recommended fix:**
  - open the Arts after M01;
  - grant Smother free at M01's end (light is the core rule);
  - make Shadowstep the M02/M03 purchase.
- **How this improves fun:** the identity shows in the first 15 minutes.
- **Implementation scale:** hours (plus script and hint updates).

### I-06: Act I detection is consequence-free
- **Severity:** P1
- **Problem:** nothing blocks objectives or reduces rewards after detection, and hits stop once you reach the door.
- **What happens now:** spotted 7× and still earned "Before the Bell", "Unbroken" and "Merciful Hunger".
- **Why it hurts:** it teaches the wrong lesson and makes M04+ feel unfair.
- **Evidence:** [Played] M02 careless run debrief.
- **Industry comparison:** Aragami 2 ("failure carries no weight").
- **Likely player behaviour:** runs through early missions.
- **Recommended fix:**
  - lockdown seals the objective door, or the target flees or locks in;
  - "Unbroken" requires no sightings.
- **How this improves fun:** early stealth matters, so later difficulty feels earned.
- **Implementation scale:** hours to days.

### I-07: Challenges give no reward
- **Severity:** P2
- **Problem:** challenges are recorded and never paid.
- **What happens now:** no replay incentive and no incentive for clean play.
- **Evidence:** [Code] MissionController.cs:910–940.
- **Industry comparison:** Mimimi badges are cosmetic but conflicting; Hitman mastery unlocks are better liked (Desperados reviewers asked for exactly that).
- **Likely player behaviour:** ignores them.
- **Recommended fix:** first-time challenge gives Vitae, or every 3 challenges give a Mark, or a mission mastery unlock (start point, thrall type).
- **How this improves fun:** how you play matters.
- **Implementation scale:** hours.

### I-08: Feed auto-chase has no cap
> **Correction:** overstated. Any WASD input already cancels the pending feed (D114; VampireMovement.cs:74–78); the chase ran because the bot held no keys. The real problem is that two movement authorities exist: with no key held, the action layer paths Ilse into range. **Revised fix:** remove player auto-walk entirely; grey out-of-range targets with the distance; add a short grab lunge (GAMEPLAY_REDESIGN.md P7).

- **Severity:** P2
- **Problem:** the pending Feed pursues while the target lives, with no distance or time cap.
- **What happens now:** I chased Wick across the map into a watchman and died.
- **Evidence:** [Code] VampireActions.TickPending. [Played] M02.
- **Industry comparison:** direct-control games don't take over movement.
- **Likely player behaviour:** feels robbed of control.
- **Recommended fix:** cancel the pending action on target flight beyond 3 m, on any WASD input, or after 2 s.
- **Implementation scale:** an hour.

### I-09: Predator economy overflows; Ghost trails
- **Severity:** P2
- **Problem:** Predator reaches A10 at M10 with 71 of 66 Marks; Ghost reaches A8 only at M13.
- **Why it hurts:** the fantasy path has no progression for 4 missions, and the stealth path gets its capstones late.
- **Evidence:** [Sim] CampaignEconomy.Simulate.
- **Recommended fix:**
  - flatten drain Vitae;
  - give Ghost Vitae for clean optionals and challenges (ties to I-07);
  - target A10 at M13 for both styles.
- **Implementation scale:** a tuning pass.

### I-10: Toolkit overlap and bloat
- **Severity:** P2
- **Problem:** 42 nodes. Five tools to neutralise vision; six to remove a human silently.
- **Why it hurts:** it dilutes choice and makes balance (I-04) harder.
- **Recommended fix:** the merges in §15, targeting about 30 nodes. **Bloodmend in particular should go**: it contradicts the dark-regen rule.
- **Implementation scale:** days (data plus UI plus mission hints).

### I-11: Near band is invisible
- **Severity:** P2
- **Problem:** humans see you at about 5 m in any light, but nothing shows this.
- **Evidence:** [Played] spotted at 4–5 m in light 0.05–0.19 (M07), and at 1–2 m in the dark (M02 groom).
- **Recommended fix:** a faint ring at the near-band radius on inspected or nearby NPCs, and a near-band tint on the player's light indicator.
- **Implementation scale:** hours.

### I-12: No save-age reminder or rotating slots
- **Severity:** P2
- **Problem:** F5 is available any time, including mid-alarm, with no save-age cue and one slot.
- **Why it hurts:** quicksaving into an already-lost state; savescum without structure.
- **Industry comparison:** Mimimi's save timer and slots are the genre standard.
- **Recommended fix:** a save-age timer in the HUD, 3 rotating quicksave slots, and a "no loads" challenge that pays (I-07).
- **Implementation scale:** a day.

### I-13 to I-15: polish
- **I-13.** The "Iron (+25% max health)" buff displays "-2147483648s", an int.MinValue duration render. Hide the timer for permanent buffs.
- **I-14.** The hub says "N unspent Marks. See Blood Arts." while the tab is disabled. Hide the line until `ArtsOpen` [UIHub.cs:90, :195]. Moot after I-05.
- **I-15.** Pounce on a Searching ferryman returned OK but didn't start a feed. Observed once, so reproduce it before fixing.

## 40. Remove or simplify first

1. **Bloodmend.** Remove it; the dark-regen rule is better.
2. **Scent of Blood.** Merge into Blood Sense.
3. **Between Bars.** Merge into Mist.
4. **Court of Night.** Fold into Mesmerize as a capstone mod.
5. **Hemorrhage or Corpse Puppet.** Keep one.
6. **Feed auto-chase.** Remove it (I-08).
7. **Global lockdown-by-shout.** Replace it with a staged alarm.

## 41. Root causes

1. **Systems were tuned by reading, not by playing.**
   - The ×15 shout bug and the wake loop survive because the design docs describe the intended behaviour and nobody measured the real one.
   - **Process fix:** a 10-line automated "AI sanity" playmode test: a shout reaches ≤ the documented radius; a dazed NPC leaves Dazed after its timer.
2. **Powers were added without a counter-rule.** Every new power needs a "cannot" (running water, holy ground, light cost).
3. **Rewards were never wired to playstyle.** The debrief measures everything and pays for nothing.
4. **The campaign arc was planned from the end.** Act I serves the story's "before" state, not the player.

## 42. Not worshipping the GDD

**Where the GDD is right and the build is wrong:**
- shout radius (10/15/22 m);
- Hunter's "mistakes are recoverable but costly".

**Where the GDD itself should change:**
- The **Act I no-powers** plan. The story of a fledgling doesn't require zero powers; it requires *weak* powers.
- The **Dossier from M07** (`DossierStartsAt = 6`). Start a one-habit Dossier at M03, so players learn the system while they still have a small toolkit.
- **42 nodes.** The GDD favours breadth; the market rewards "depth without complexity" (Mimimi's own postmortem phrase).
- **Bloodmend.** It contradicts the GDD's best rule.

## 43. Design risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| "Powers trivialise stealth" reviews | High | High | I-04 running water; cost scaling; Dossier counters (cm_rooftop, cm_caged, cm_censer, cm_salt) |
| "Quickload simulator" reviews | High | High | I-03, I-12 |
| "Not a vampire for the first hour" | High | High | I-05 |
| Countermeasures pile up (the MGSV complaint) | Medium | Medium | Cap active countermeasures at 2–3 and show them before the mission |
| Scope versus the market ceiling (Mimimi's closure) | Medium | Existential | Cut nodes (§40); reuse no maps; demo early |
| WASD precision complaints | Medium | Medium | Hands-on test; near-band ring; no auto-path |

## 44. Progression stress test

| Build | Act I | Mid (M07) | Late (M12) |
|---|---|---|---|
| **Pure ghost (no kills)** | Fine | Dossier reacts to sips; trails on Awakening [Sim] | Capstone only at A8 (M13) |
| **Pure predator** | Wins by force without powers | Dies to global alarms until Apex | Maxed since M10; nothing to buy [Sim] |
| **Shadow (Shade tree)** | n/a | Shadowstep solves maps | Eclipse plus Shadowstep is the Aragami "solved" loop |
| **Dominion** | n/a | Thrall play is untested here | Court plus thralls risks the "solo squad" problem |
| **No-power run** | Required | Is M07 possible without Shadowstep or the ferry? Probably yes (bridges) | Unknown |

**Breaks found:**
- **Predator:** runs out of progression at M10.
- **Shade:** Shadowstep dominance.
- **Every build:** bug B drives the Dossier.

## 45. Identity

**What Vespertine is, at its best:**
- a Mimimi-readable stealth game where **light is your hunger meter and your enemy**;
- humans **learn how you hunt** (the Dossier, inquisitors reading wounds);
- a campaign arc in which you **stop hiding and start hunting**.

**What it must not become:** "Shadow Tactics with a teleport" (Aragami) or "a 42-ability buffet" (late Styx).

**Rules that would make the identity unmistakable:**
1. **Running water cannot be crossed by power.**
2. **Healing happens only in the dark, paid in blood.** Already true; protect it.
3. **The Vigil learns.** A visible Dossier from M03, capped and shown before each mission.
4. **Terror at the end.** Late guards flee and hide from you (Terror and Herding exist), so the fantasy line pays off as a mechanic and not just a stat.

## 46. Top 10 changes, in order

| # | Change | Scale | Impact |
|---|---|---|---|
| 1 | Fix shout and scream radius (I-01) | Minutes | Unlocks local AI, escalation and emergence |
| 2 | Fix the dazed wake loop and reset dev Dossiers (I-02) | An hour | Restores sip versus drain and an honest Dossier |
| 3 | Staged alarm with break-contact (I-03) | Days | Turns failure into play |
| 4 | Running water blocks Shadowstep and Mist (I-04) *(corrected: the aimed Shadowstep already respects it; add the check to `Cast`, hours)* | Days | Restores level design; a signature vampire rule |
| 5 | Open Blood Arts after M01; free Smother (I-05) | Hours | Fantasy in the first 15 min; better demo |
| 6 | Act I detection has consequences (I-06) | Hours to days | Teaches the right lesson |
| 7 | Pay challenges (I-07) and rebalance economy styles (I-09) | A day | Replay; playstyle identity |
| 8 | Save-age timer, rotating slots, paid no-load challenge (I-12) | A day | Genre-standard save UX |
| 9 | Remove the feed auto-chase *(corrected: remove all player auto-walk)*; show the near-band ring (I-08, I-11) | Hours | WASD fairness |
| 10 | Prune the toolkit to about 30 nodes, Bloodmend first (I-10) | Days | Depth without complexity; easier balance |

## 47. KEEP / CHANGE / CUT

**KEEP**
- Blood economy (dark-only regen paid in blood; starving heartbeat).
- Near-total darkness beyond the near band.
- Feeding from behind; sip and drain (after the fix).
- Inquisitors examining bodies by cause.
- Hunters with flares; hounds.
- The Dossier concept and the debrief "The Vigil adapts" line.
- Canal and hide-spot body disposal.
- Pounce, Rend and Apex.
- Smother's chain mods.
- M07's crossing design.
- Difficulty that changes perception.

**CHANGE**
- Alarm model (local and staged).
- Shadowstep (water rule, cost).
- Act I power gate.
- Challenge rewards.
- Economy curves.
- Dossier start (M03) and cap.
- Save UX.
- Near-band visibility.
- Pending-action pathing.

**CUT**
- Bloodmend.
- Feed auto-chase.
- Scent of Blood (merge).
- Between Bars (merge).
- Court of Night (merge).
- One of Hemorrhage or Corpse Puppet.
- The "See Blood Arts" nag before the Arts open.

## 48. Final assessment

**Would I recommend this game today?** No.

The blocker isn't a lack of ideas; it has more good ideas than most games in the genre. The blocker is that the two systems a stealth game is judged on first (who hears an alarm, and what happens to someone you've touched) are broken in ways that distort every downstream system. That includes the Dossier, which is this game's signature feature.

**Is it a good design?** Underneath, yes.
- The blood triangle is genuinely better than Aragami 1's light-cost tension, which Aragami 2 was punished for removing.
- The Dossier is a better-motivated MGSV Revenge system.
- The prey-to-predator arc is something no Mimimi game offers.
- The late-game Apex/Rend test delivered "humans hide from me" in a way that felt earned.

**The honest path:**
1. Fix I-01 and I-02 today.
2. Do a hands-on 20-minute WASD session.
3. Do I-03 to I-06 before any public build.

Then this audit's 47 likely becomes the high 60s. The Top 10 are mostly additions of rules, not systems, and could put it in the low-to-mid 70s. That's a game Mimimi's orphaned audience would actually adopt.

**Judged as players will play it today, it is a Shadow Tactics-like that:**
- withholds its vampire for the first hour;
- shouts every mistake across the whole map;
- lets one teleport skip its best map *(corrected: only by a harness path that bypasses aim validation; see I-04)*.

**Judged by what is already in the code, it is two bug fixes and four rules away from being the vampire stealth game this genre is missing.**
