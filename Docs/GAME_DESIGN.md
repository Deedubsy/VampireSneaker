# VESPERTINE — Game Design Document (authoritative)

> *ves·per·tine* (adj.) — of the evening; active, blooming, or hunting at dusk.

**Genre:** Real-time tactical stealth (isometric, single protagonist + thralls)
**Platform:** PC (mouse + keyboard), Unity 6 / URP
**Target length:** ~8–10 hours, 14 missions in 4 acts
**Fantasy:** *"I begin as something hiding from humans. By the end of the game, humans are hiding from me."*

---

## 1. Concept analysis (Step 1)

### What makes the concept compelling
1. **The arc lives in the mechanics.** The prey→predator arc is felt *with your hands*, not just told:
   in Act I the player avoids people; in Act IV people avoid the player, and the game measures it (Terror, curfews,
   hunters, barricades).
2. **Light is a weapon on both sides.** Every vampire stealth game says "darkness is good". Ours makes light a
   **resource contested by both sides**. The player destroys light; the city builds it back. Lamplighters relight,
   hunters throw flares, the Church carries holy fire, and the Institute invents the *sunstone lamp*.
   The city's lighting grid *is* the level design.
3. **Feeding is risk you go looking for.** Blood is earned only by getting close to a human. The question
   *"Who can I safely feed on?"* drives route planning. Each feed also leaves something behind: a body, a stain, a
   witness, or a sleeper who will wake.
4. **The enemy learns *you*.** Between missions the Pale Vigil's **Dossier** records what you actually did
   (rooftops, mist, mesmerism, lethal feeding) and fields counters for it. Your habits become your next problem.
   Builds stay fresh, and dominant strategies are punished by the fiction rather than by an arbitrary nerf.
5. **Thralls are the vampire's squad.** Shadow-Tactics-style coordination comes from humans you *take*, not from
   a party you are handed. Coordination is something the vampire earns.

### Design pillars
| # | Pillar | Test question |
|---|--------|---------------|
| P1 | **Predatory stealth** — stalk, isolate, take. | Does this make the player feel like a hunter rather than a burglar? |
| P2 | **Light is contested terrain** | Does this interact with the light grid? |
| P3 | **Blood is risk** | Does spending/earning blood create a decision? |
| P4 | **Above and beneath** — verticality humans can't use | Does the vampire move here in a way a human couldn't? |
| P5 | **The city adapts** | Does the world remember what the player did? |
| P6 | **Readable at a glance** | Can a new player tell why they were seen? |

### Biggest risks and mitigations
| Risk | Mitigation |
|------|------------|
| Late-game god mode | Countermeasures scale per-player (Dossier); ability **loadout cap** (4 → 6 slots); blood economy gates spam; wards/holy auras/sunstone selectively disable whole trees (never all at once). |
| Waiting simulator | Patrols are short loops (≤40 s); **Beckon** and light manipulation let the player *create* openings; Blood Sense shows routes. |
| Quickload simulator | Detection is a meter, not binary; Suspicious → Investigate gives a recovery window; early guards miss often; Ilse can flee upward (rooftops) where humans can't follow. |
| Information overload | Only the cones that can catch her soon show by themselves (at most 4, D134); inspect one, or hold **Alt** for all; colour language is strict (§9). |
| Scope (14 missions, art) | All levels are **text-authored tactical maps** built at runtime → fast iteration, consistent look, data-driven. Stylised low-poly "ink & ember" art is achievable with primitives + lighting. |
| Feature graveyard | Abilities are few and deep (≈16 actives across 4 trees); every one interacts with light, bodies, or thralls. |

---

## 2. Identity (Step 2)

### Elevator pitch
*Vespertine* is a real-time tactical stealth game about **Ilse Marrow**, a fever-ward nurse who was turned into a
vampire in a secret experiment and left to drown. Across a gaslit canal city that slowly learns to fear her, she
stalks, feeds, snuffs out the lamps, takes thralls, and climbs where no human can. She grows from starving prey into
the thing the city's lights were built to keep out.

### Setting — **Ostmere, 1871**
A fog-choked canal city on a northern fen estuary: brick warehouses, iron bridges, flooded crypts, gaslight.
- **Gas lamps** line every street. They are maintained by the **Lamplighters' Guild**, the city's oldest and proudest
  institution. The lamps mark the boundary of civilisation.
- **Canals** (running water) cut the city into districts that a vampire can only cross by bridge, boat or roof-leap.
- The city's elite, the **Council of Lanterns**, secretly funds the **Coldwater Institute**. There Dr. Emeric
  Saule keeps an ancient vampire chained in a flooded vault and harvests her blood ("eternal vitae") for the
  Council's own immortality. Prisoners from the fever wards are the test subjects.

### Tone
Gothic, melancholy, human. Not camp. The vampire's power is seductive and costly. Dialogue is spare. Barks are
grounded and working-class: guards complain about wages and wet boots, then later about *the thing in the fog*.

### The vampire — Ilse Marrow
- 26, ward nurse at Coldwater, discovered the experiments, was used as a test subject.
- Woke in the Drowned Ward: starved, weak, *new*. She cannot yet fully stand in lamplight.
- Wants: to find out what was done to her, to protect her younger brother **Tobias** (a lamplighter's apprentice),
  and to make the Council pay.
- Is haunted by **the Abbess**, the ancient vampire whose blood made her. The Abbess speaks in blood-dreams between
  missions: half mentor, half tempter.

### Factions (enemy escalation)
| Faction | Introduced | Role |
|---------|-----------|------|
| **City Watch** (watchmen, sergeants) | Act I | Lantern-carriers, slow, superstitious. |
| **Lamplighters' Guild** | Act I–II | Relight snuffed lamps; carry light-poles; guildhalls control gas mains. |
| **Church of the Kindled Flame** (priests, acolytes) | Act I late | Holy aura suppresses abilities; bells raise district alarms. |
| **Coldwater Institute** (orderlies, alchemists) | Act I / III | Garlic-smoke censers, sunstone lamps, restraints. |
| **The Pale Vigil** (hunters, trackers, bulwarks, inquisitor) | Act II+ | Professional vampire hunters who learn and adapt. |
| **The Abbess / Saule's creations** | Act III–IV | Supernatural rivals. |

---

## 3. Core loop

**Moment-to-moment (seconds):** Read cones and light → move between darkness → isolate a target → take
(feed / mesmerise / snuff a lamp) → handle the evidence.

**Encounter (minutes):** Scout (Blood Sense, rooftops, looking ahead with the camera) → set up (thralls in place,
a snare laid, a lamp snuffed) → create an opening (Beckon, a thrall's distraction) → strike → recover (hide bodies,
retreat to dark, regenerate). All of it in real time: the world never stops for orders.

**Mission (30–50 min):** Briefing → infiltrate with several possible routes → primary objectives plus optional
objectives, secrets and notable victims → extract → debrief (Vitae, Awakening, Dossier update).

**Campaign:** Spend **Marks** in the skill trees → choose a **loadout** → face the city's adapted countermeasures →
see the story and world state change (Terror, curfews, blackout, Tobias's fate).

---

## 4. The vampire's capabilities

### 4.1 Base kit (never in skill trees)
| Action | Notes |
|--------|-------|
| **Move** (WASD / left stick) | Direct, camera-relative. Glide (silent walk). Hold **Shift** = **Run** (2× speed, noise radius 6 m). She slides round humans rather than through them. |
| **Climb** | Walk into a climb point (drainpipe, ivy, ladder) for a moment, or press **Space** beside it. The **Wallcrawler** awakening (Awakening 4) lets her climb any brick or stone wall. Humans need ladders/stairs. |
| **Drop / Leap** | Walk off a roof edge, wall or balcony (a drop waits a beat longer than a climb so it is meant), or press **Space**. Silent. Leaps (Bound) work the same across gaps. |
| **Feed** (F on an unaware human from behind, or on a mesmerised, sleeping or snared one; within 2.6 m she lunges the last step, further says "Too far": D129) | Choose **Sip** (non-lethal, 1.6 s, 60% blood; victim collapses *Dazed* and will wake and report) or **Drain** (lethal, 3.2 s, 100% blood, corpse + blood stain). |
| **Carry / Drop body** (C) | Slows Ilse to 60%, no climbing. Bodies can be **dumped in canals** (gone), **hidden** in hiding spots (wells, crates, privies) or **left in darkness**. |
| **Snuff** (interact with an adjacent light) | Free and silent, but a dark lamp is *noticed* by guards and relit by lamplighters. |
| **Interact** (G, or click) | Doors, levers, valves, documents, secrets, hiding places, lamps. G takes what is beside her first, else what is under the cursor (she walks to it). |
| **Hide** | Darkness *is* hiding. Hedges and tall reeds block vision cones. |

**Classic vampire laws (mechanical, not flavour):**
- **Running water** (canals, river) cannot be crossed except by bridge, boat or roof-leap. Mist cannot cross it.
- **Threshold.** Doors marked as *homes* cannot be entered uninvited. A thrall or an ally can invite her in.
- **Sunlight and holy light** burn her. Standing in them costs health.
- **Garlic** wreaths and smoke block doorways and disable Dominion abilities inside their area.
- **Silver** weapons wound her and stop regeneration for 10 s.

### 4.2 Blood (the resource)
- **Blood** (0–max). Starts at max 60, grows to 150 with Awakening. Spent on abilities, healing and Mist upkeep.
- **Health** (0–max). Regenerates only in darkness and only by spending blood (1 blood → 1 HP, at 4 HP/s while
  in dark and out of combat). *Healing is therefore also a blood decision.*
- **Starving** (blood < 15%): Ilse moves 10% faster but her heartbeat-hunger makes her **audible** within 3 m
  (heavy breathing). Pushes the player to feed, which pushes the player toward risk.
- **Blood qualities** (victim archetypes give a *Humour*, a temporary mission buff lasting until the next feed):
  | Victim | Blood | Humour |
  |--------|-------|--------|
  | Common (civilian, watchman) | 30 | — |
  | Drunk (sailor, guest) | 25 | *Languid*: −15% noise; −10% speed. |
  | Fevered (sick, beggar) | 15 | *Fever-sight*: Blood Sense free for 60 s. |
  | Soldier / Hunter | 40 | *Iron*: +25% max HP until next feed. |
  | Priest / Acolyte | 30 | *Scalding*: −10 HP on feed, but Sanguis abilities −50% cost for 60 s. |
  | Occultist / Alchemist | 30 | *Lucid*: Dominion abilities −50% cost for 60 s. |
  | Notable (named targets) | 60 | +1 **Mark** (skill point) on Drain. |

  Choosing *whom* to feed on is a tactical choice, not a spreadsheet.

### 4.3 Awakening (vampire level, 1–10)
Raised by **Vitae**: total blood consumed across the campaign plus objective rewards. Each level is a **new
capability** first and a stat bump second:
| Lvl | Unlock |
|-----|--------|
| 1 | Sip/Drain, climb points, snuff |
| 2 | Carry bodies at full speed on flat ground; +1 loadout slot (2) |
| 3 | **Roof-leap**: jump gaps of 2 cells between rooftops |
| 4 | **Wallcrawler**: climb any brick/stone wall; +1 slot (3) |
| 5 | **Feeding from mesmerised targets while they are in a cone without being seen** (veil of the feeding) |
| 6 | +1 slot (4); Health regeneration doubled |
| 7 | **Drop-feed**: dropping on a target from a height starts a Drain instantly |
| 8 | +1 slot (5) |
| 9 | **Dread presence**: civilians that see her flee silently instead of screaming |
| 10 | +1 slot (6); the *Abbess's gift* (story) |

### 4.4 Skill trees
See **PROGRESSION.md** for full trees (4 trees: **Predator, Shade, Dominion, Sanguis**). Each has 4 active
abilities, passives, modifier nodes and a capstone. Marks come from objectives, secrets and notable victims, roughly
3–4 per mission, about 45 in total, against ~70 nodes. **No player can buy everything.** The best play is a build.

### 4.5 Loadout
Only **equipped** actives are usable. Slots: 2 → 6 by Awakening. Equipping happens in the pre-mission screen, so a
replay with a different loadout means a different route.

---

## 5. Stealth simulation

### 5.1 Perception
- **Vision cone:** angle 90° (archetype-defined). Two bands:
  - **Near band** (≤ near range, e.g. 6 m): sees Ilse in *any* light.
  - **Far band** (near → far range, e.g. 6–16 m): sees Ilse only if her **light level ≥ 0.35**.
  - Rendered as a two-tone cone: a solid near sector ending in a bright arc, and a far band filled only where she
    would be lit, ending in a thin arc (D135). *Far band = "they see you if you're lit."* A 1.4 m touch circle
    shows around a seeing guard within 4 m of her.
  - **Which cones show** (D134): guards aware of her within 25 m, and guards whose detecting region is within 4 m
    of her now or in 1.5 s; at most 4 in full. Alt shows all. Middle-click pins a guard's cone (up to 3; the
    oldest drops) and hovering a guard shows his (D140).
  - **Ownership** (D139): where cones overlap, the guard whose meter is rising on her draws on top at full strength
    and the others at half. A short arc at each guard's feet says whose cone it is. Within 8 m of her, the dark
    part of the far band gets a faint fill and its sides an outline. A mesmerised or dazed guard's cone (Alt or
    pinned only) is a violet outline with no fill.
  - **Roofs** (D141): where a guard below can see her on a roof (his near sector, or his lit far band if he looks up,
    and nothing in the way of his eye), the cone carries on across the roof, stopping where the roof's edge hides her.
  - **Cone-edge grace** (D138): the outer 10° on either side and the outer metre of what sees her (inside the near
    edge in the dark; at the far end when lit) fill at half rate, drawn at half strength and dashed. The first
    0.25 s of a sighting doesn't count, and that allowance returns only after he has lost her for 1.2 s. Touch
    range, searchlights and smell have no grace. Merciful widens the fringe ×1.5. The near edge stays solid.
  - **State styles** (SR.5): a guard who has lost her (Searching) draws red at 60% with a dashed end; a blinded
    guard draws grey and dashed.
  - **State marks** (D146): a Wary guard shows a small ring at his head; a suspicious one a "?". An investigating
    guard's walk is a dotted amber line to a small ring where he is going; a searching guard's walk to his next search
    point is the same in dim red. The dots march the way he walks.
  - **Time to contact** (D146): a guard whose cone will reach her within 2.5 s, from his walk and turn and hers, shows
    his cone (ahead of nearer guards) and, off screen, an edge pip.
  - **Suspicious pulse** (D147): a suspicious guard's cone pulses slowly (70–100% strength, 0.8 Hz).
  - **Accessible cones** (D147, SR.12), in Settings → Gameplay: *Always show all vision cones* (every seeing guard on
    screen, within the difficulty's range, without the rest of the tactical view); *Cone key toggles instead of hold*;
    *Shape-coded cone states*, so colour is never the only channel: the origin arc at his feet is small and solid
    while he is unaware, large and broken while suspicious or investigating, large and solid once he knows (spotted,
    hunting, searching, panicked). *High-contrast vision cones* now also doubles the near-edge and end-arc widths.
  - **Readability test mode** (D148, dev, §44.1), in Settings → Gameplay: twelve times a map the game freezes for 2 s
    with the overlays as they were and asks one question (can he see you; where his cone ends; how close you can get in
    the dark; where a lamp stops exposing you; a safe route to a mark; will your next step expose you), then darkens
    the screen for the answer (Y/N, a click, or a traced route). After a detection it asks what caused it. Every answer
    is scored against the rules at the frozen frame, and the verdict over all sessions is written beside the logs.
  - **Exposure rims** (D132): each light near her draws a thin line where it stops exposing her (light 0.35).
  - **Truthful pools** (D145): a light's pool on the ground is flat and bright where it exposes her and ends within a
    third of a metre of that line, so the pool itself says "exposed". Braziers, fires and candles throw the same kind
    of pool. Walls cut a pool only where the light casts real-time shadows (the lights nearest the camera).
  - **Spotted caption** (D136): when a guard spots her, a line under him says why ("Lit by the gas lamp: light
    0.91 (exposed above 0.35), 9.1 m away.").
- **Vertical:** a target more than 2.5 m above the observer is only seen in the near band, unless the archetype
  **Looks Up** (Vigil hunters, rooftop sentries).
- **Detection meter** per observer (0–1). It fills while the target is visible: rate = base × distance factor ×
  light factor × motion (Rush ×2, standing still ×0.6) × difficulty. It drains when the target is out of view.
  - ≥ 0.35 → **Suspicious** (yellow "?"), the guard stops and turns.
  - 1.0 → **Spotted** (red "!"), the guard is Alerted.
- **Hearing:** noise events with a radius (Rush 6 m, drop body 5 m, breaking a lamp 10 m, gunshot 30 m, bell 60 m,
  scream 18 m). Walls halve the radius.
- **Smell (hounds):** hounds sense Ilse in 360° within 5 m regardless of light, and follow **blood trails**.

### 5.2 AI states
`Relaxed → Suspicious → Investigating → (Relaxed but Wary) | Alerted → Searching → Wary`
plus **Panicked**, **Dazed**, **Mesmerised**, **Thrall**, **Dead**, and the level-wide **Lockdown**.
- **Wary** is persistent. A guard who investigated something stays wary for the rest of the mission: 25% faster
  detection and looks around while patrolling. *Enemies don't forget.*
- **Alerted** guards shout (alerting others within 15 m), shoot (muskets, slow reload) or melee, and run to the
  nearest **alarm bell** if one is within 20 m.
- **Searching**: they go to the last known position, then search through nearby points (lanterns raised) for
  30–60 s.
- **Lockdown** (bell rung or 3+ bodies found): every guard becomes Wary, gates close, lamplighters relight
  everything, reinforcements arrive at the mission's reinforcement points, and the Vigil (from Act II) sends a squad.
- **Panicked** (witnessing a feed or kill, unless Vigil/officer): civilians always, watchmen with low morale. They
  flee toward the nearest light or group and scream, which is itself noise. Late Predator play *uses* panic to herd
  crowds.

### 5.3 Evidence
| Evidence | Effect when noticed |
|----------|--------------------|
| Corpse | Alerted → Searching; the body is reported; 3 reported bodies → Lockdown |
| Dazed victim (Sip) | Wakes after 45 s and reports: the guard turns **Wary**, the area searched, +Dossier "Sightings" |
| Blood stain (from Drain) | Suspicious → Investigating; Inquisitors track it |
| Snuffed lamp | Suspicious if in a guard's route; **lamplighters walk over to relight** |
| Missing patrol partner | Paired guards check their partner's last checkpoint (Wary) |
| Thrall acting strangely | Officers notice a thrall in a restricted area and investigate |

### 5.4 Light
- Every light source has a radius and intensity. **Gameplay light** at a point = max over visible sources of
  `intensity × (1 − d/r)²`, with line-of-sight against walls, plus mission ambient (moonlight 0.05–0.2).
- **Light types:** torch, lantern (carried), gas lamp (street, networked by gas main), brazier, fireplace,
  candelabra, **holy flame** (burns, aura), **flare** (thrown by hunters, 20 s), **searchlight** (sweeping),
  **sunstone lamp** (Act III; burns like sunlight), and **sunbeams** (dawn missions).
- **Manipulation:** snuff (adjacent), *Smother* (ranged), break, **gas valves** (switch off a whole lamp group),
  thralls turning off lamps, *Gloom* clouds, *Eclipse*.
- Guards respond to darkness. A lit route that goes dark makes nearby guards suspicious, and a lamplighter will
  come. Carried lanterns make guards visible from far away (readability) and light the area around them.

---

## 6. Thralls

### Thralls (Dominion tree)
A thrall is an enthralled human the player controls like a party member in a party RPG. Click their portrait in the
**Party** panel (or press **T** to cycle Ilse → each thrall → Ilse) and the move keys steer *them*, with the camera
following. Ilse holds where she stands. Right-click, or click Ilse's portrait, to take her back. Whoever is not
under the player's hand **holds position**; **X** tells a thrall to follow Ilse instead (with Ilse in hand, X makes
every thrall follow or hold). While controlling a thrall:
- **1 Distract**: speak to the human under the cursor, or the nearest within 3 m (a 6 s distraction that turns
  their cone toward the thrall). Clicking a human does the same.
- **2 False Orders** / **3 Puppet Strike** (when learned): aim, then click.
- **4 Release**. **G** or a click uses a door, valve or lever the thrall can operate.
- Pushing into a ladder takes it.

Thralls:
- Are **trusted** by their own faction: they can walk restricted areas, talk to guards (a 6 s distraction that
  turns the guard's cone toward the thrall), open gates, operate valves, carry bodies, **invite Ilse across
  thresholds**, remove garlic wreaths, and deliver *False Orders*.
- Are **not trusted** if seen with Ilse, carrying a body, or in a forbidden area (officers and inquisitors notice).
- Can be **fed upon** safely at any time (Sip only). This makes a portable blood bank and a moral weight.
- Limit: 1 (2 with upgrade, 3 with capstone).

### Coordinated kills
There is no planning mode. A synchronised strike is set up in real time: walk each thrall into place and leave it
holding, lay a Blood Snare, then take control of each in quick succession (a strike runs on its own once given)
before striking with Ilse. The opera's crescendo (M11) is the window this is built for.

### Tactical pause
**P**: a plain pause. The camera can still move and cones can be inspected, but no orders can be given until time
runs again. **Esc** opens the pause menu.

---

## 7. The enemy learns — the Dossier
After each mission the Vigil (and before them, the Watch) **records the player's methods**. Tracked counters
include rooftops used, lamps snuffed, lethal feeds, sips, mesmerisms, thralls, mists, bodies found and
sightings. The two highest-weighted habits generate **Countermeasures**, which are added to the Dossier and active
in all later missions:
| Habit | Countermeasure |
|-------|----------------|
| Rooftops | Rooftop sentries; hunters Look Up |
| Snuffing lamps | Caged lamps: half of every gas lamp, wall lamp, brazier and candle (rounded up) can't be snuffed by hand. Every Watch, Vigil, Church, Guild and Institute man carries a taper and relights a dark lamp he finds |
| Lethal feeding / bodies found | Paired patrols; bodies trigger Lockdown at 2 instead of 3 |
| Sips / sightings | Inquisitors examine victims; dazed victims wake in 25 s |
| Mesmerism / thralls | Ward charms (some guards immune to Dominion) |
| Mist | Censer-bearers (garlic smoke reveals and blocks mist and Shadow Dash) |
| Hemorrhage / blood magic | Salt lines (Blood Snares fail); priests escort |

Level files contain **dormant entity groups** tagged with countermeasure ids. Scripted story escalation (curfews,
emergency lighting, Vigil squads, sunstone lamps) is layered on top.
Habits are tracked from data, so two players get different late games.

**World-state consequences (no morality meter):**
- **Terror** (lethal kills of civilians, witnessed horrors) leads to curfews: fewer civilians (fewer easy feeds,
  fewer witnesses) and more patrols.
- **Rumour** (spared victims, sips) leads to "the Pale Lady" stories. The **Faithful** leave windows open and give
  hints (shortcuts) in later missions, but the Vigil learns faster (wake time and Dossier weight up).
- **Tobias** can be protected, enthralled, or lost. This changes Act III/IV routes and the ending.

---

## 8. Campaign
14 missions in 4 acts: **Prey → Hunted → Predator → Apex**. Full detail is in **CAMPAIGN.md**.

## 9. Readability language (strict)
| Colour | Meaning |
|--------|---------|
| Pale bone white | Neutral, interactable |
| Amber / warm | Light: dangerous to stand in |
| Indigo / black | Safe darkness |
| Pale bone cone | Relaxed observer |
| Yellow cone / "?" | Suspicious / investigating |
| Red cone / "!" | Alerted / hostile |
| Crimson | Ilse, blood, her abilities |
| Violet | Thralls and Dominion |
| Gold-white | Holy (forbidden) |

**Ground disc** (D142): under Ilse's feet. A hollow ring while she is hidden, filled warm once she is lit enough for
a far band. Violet, grey or shade blue while the masque, mist or her darkness overrides light; a gold double rim while
holy light burns her. A tick on the rim points at each guard who can see her, in his colour, as long as his meter. The
rim pulses in a guard's colour within 1 m of his near sector. A chevron at the front shows what the next step does:
orange into open light, a guard's colour into ground he sees, flashing into his near sector.

**Light Meter**: an eye glyph by Ilse's portrait shows her current light level (closed eye = dark, open = lit),
with a coloured ring when she's in a far band of any cone. Its label has one threshold (D147), the disc's own line at
light 0.35: DARK or LIT (MASKED when a mask holds her lit, CONCEALED when hidden, BURNING in holy light). The old
middle SHADOW state is gone: nothing in the rules changed at its line. **Detection pips** above heads fill as a meter.
Ability targeting shows range rings and costs.

**Meter fills** (D143): the pip's fill says which sense is filling it: solid for close (near band or touch), stripes
for the lit far band or a searchlight, waves for a noise, dots for scent. When a meter first moves, the edge she has
just crossed glints for a moment: his near arc, his touch circle, or the light's rim. A guard off screen who has a
meter on her or is searching, within 25 m (40 m while Alt is held, any range if pinned), gets a pip on the screen edge
with his meter, an arrow toward him and a wedge showing which way he faces.

**Spotted picture** (D144): when a guard spots her, for the 4 s the caption shows, his cone is drawn in full, the
line she crossed stays lit (his near arc, his touch circle, or the lamp's rim with a ring at the lamp), a ring pulses
at his feet and a line runs from his eye to her. After the night, the debrief's **Detections** button lists each
sighting as a small top-down sketch (him, her, his cone cut by walls, the crossed line in white) with the caption.

## 10. Difficulty
| | Merciful | Hunter (standard) | Apex |
|---|---|---|---|
| Detection rate | 0.7× | 1× | 1.3× |
| Search duration | 0.7× | 1× | 1.5× |
| Shout radius | 10 m | 15 m | 22 m |
| Blood from feeds | 1.25× | 1× | 0.85× |
| Ability costs | 0.85× | 1× | 1.15× |
| Grace fringe (D138) | ×1.5 | ×1 | ×1 |
| A cone shows by itself when she is within (D140) | 6 m | 4 m | 4 m; unaware guards show their near sector only |
| Exposure rims within | 10 m | 10 m | 6 m |
| Alt shows cones | all in view | all in view | within 20 m |
| Autosave | every objective + 3 min | every objective | mission start only |

## 11. Controls (default, all rebindable)
| Action | Key |
|---|---|
| Move (Ilse, or the thrall under her hand) | WASD / left stick (camera-relative) |
| Run | hold Shift |
| Climb / drop / leap / slip through | Space (or walk into it) |
| Use what is under the cursor / confirm a target | LMB |
| Cancel / back to Ilse | RMB |
| Look ahead (camera eases back when you move) | Arrow keys / MMB drag |
| Camera rotate | Q / E (hold) |
| Zoom | Mouse wheel |
| Re-centre camera | Home |
| Abilities 1–6 (thrall commands while one is controlled) | 1–6 |
| Feed | F (Sip), R (Drain) |
| Interact / snuff | G |
| Carry/Drop body | C |
| Inspect enemy (show cone) | MMB click / hover |
| Show all cones | hold Alt |
| Blood Sense | hold V (when unlocked) |
| Take control: next thrall / Ilse | T, or click a portrait |
| Thrall: follow Ilse / hold | X |
| Pause | P |
| Menu | Esc |
| Quick save / load | F5 / F9 |

## 12. Art direction — "Ink & Ember"
- **Geometry:** stylised low-poly, flat-shaded, chunky silhouettes (brick blocks, slate roofs, iron).
- **Palette:** near-monochrome indigo/slate night; **amber ember** pools of gaslight; **crimson** reserved for Ilse
  and blood; violet for Dominion; holy gold-white.
- **Lighting is the signature:** real-time point lights for every gameplay light; volumetric-feeling fog; the
  readable shape of a level comes from its pools of light.
- **Post:** bloom on lights, strong vignette, desaturated colour grade, subtle film grain.
- **Characters:** readable silhouettes by faction (watch: stovepipe hat + lantern; lamplighter: pole; priest: robe
  cone + halo glow; hunter: wide brim + long coat; hound: low quadruped). Ilse: dark cloak, pale face, crimson eyes.

## 13. Audio (as gameplay)
- **Heartbeats**: nearby humans' heartbeats are audible (panned) when Blood Sense is active, and always within 4 m.
  Alerted humans' heartbeats race.
- **Footsteps** of guards are audible within 10 m (positional).
- Cues: rising string tone with suspicion, sharp sting on spotted, bell tolls (district alarm), dog barks.
- **Feeding**: a muffled heartbeat that slows and stops (Drain) or steadies (Sip).
- Procedurally synthesised placeholders for all of this (see KNOWN_ISSUES / asset list).

## 14. Endgame
Act IV turns the city into a fortress against Ilse: curfew, sunstone lamps, Vigil squads, wards and censers. The
final mission (**The Abbess Beneath**) is a three-stage infiltration of the Institute vault beneath the cathedral at
the coming dawn: sunbeams creep through the windows as real time passes. It ends with a choice (free, consume, or
end the Abbess) shaped by Tobias's fate and the Terror/Rumour balance. Multiple endings; see CAMPAIGN.md.
