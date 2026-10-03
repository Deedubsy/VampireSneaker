# VESPERTINE — Progression

Three interlocking systems:
1. **Awakening** (level 1–10): raised by **Vitae**. It unlocks *capabilities* and loadout slots (GAME_DESIGN §4.3).
2. **Marks** (skill points): bought from objectives, secrets and notable victims. Spent in the 4 trees.
3. **Loadout**: equipped actives (2→6 slots). This is the actual build you take into a mission.

## Vitae
- 1 Vitae per blood point a feed gives (scaled by difficulty: Merciful ×1.25, Apex ×0.85). It is not capped by the
  blood pool, so a feed when full still counts.
- Objective rewards are paid only the first time: first clear +60, each new optional +30, each new secret +20
  (lore-only secrets too). Scripted shrine grants (`vitae N`) add a little more.
- Awakening thresholds (cumulative): L2 120 · L3 400 · L4 700 · L5 1050 · L6 1400 · L7 1800 · L8 2250 · L9 3000
  · L10 3800. They were tuned with `Progression.CampaignEconomy` (D100).

## Marks
- Mission completion: +1 (first clear). Each secret: +1 (some secrets are lore only, marked). Optional objectives pay
  Vitae only (D130).
- Each challenge, the first time it is earned on that mission: +1 (D130).
- Notable victims (Drain): +1, **once per notable per campaign**. Replaying a night cannot farm the same throat.
- The trees total 66 Marks. **Nobody gets everything before the endgame.**

## The earning curve (X6)
`CampaignEconomy.Survey` counts what each shipped mission offers. `Simulate` plays three styles through the
campaign on Hunter:
- **Ghost:** 3 sips a night, no drains, half the optionals, a third of the secrets.
- **Typical:** 6 sips, 1 drain (a notable where there is one), 60% of optionals, half the secrets.
- Challenges earned for the first time a night: Ghost 3, Typical 2, Predator 2.
- **Predator:** 4 sips, 6 drains including every notable, 80% of optionals, 70% of secrets.

Awakening / Marks at the start of each night:

| | M01 | M02 | M03 | M04 | M05 | M06 | M07 | M08 | M09 | M10 | M11 | M12 | M13 | M14 | end |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Ghost | 1/0 | 2/4 | 2/8 | 3/12 | 3/16 | 4/21 | 5/26 | 5/30 | 6/34 | 6/38 | 7/42 | 7/46 | 8/50 | 8/54 | 8/58 |
| Typical | 1/0 | 2/3 | 3/6 | 4/10 | 5/13 | 5/18 | 6/23 | 7/26 | 8/30 | 8/33 | 8/36 | 9/40 | 9/44 | 10/48 | 10/52 |
| Predator | 1/0 | 2/4 | 4/8 | 5/13 | 6/17 | 7/22 | 8/33 | 9/37 | 9/42 | 10/46 | 10/50 | 10/56 | 10/62 | 10/66 | 10/70 |

- The Blood Arts open after M01 (D127). **Tier II** (Awakening 3) is open for everyone by M04.
- **Tier III** (Awakening 5) is open by M07.
- **Capstones** (Awakening 8):
  - Predator: M07.
  - Typical: M09.
  - Ghost: M13.
- **Awakening 10:**
  - Predator: M10.
  - Typical: the last night.
  - Ghost: never.
- **Marks** (out of the trees' 66; challenges pay +1 each the first time, D130):
  - Ghost earns 58: careful play now progresses as fast as feeding (it earned 42 before D130).
  - Typical earns 52.
  - Predator can afford every node only from M14.

The Masquerade (M06, seven notables) is the predator's spike.

`CampaignEconomyTests` hold these properties. If a mission edit breaks one, retune the thresholds or the mission.

## Challenges (+1 Mark each, first time)
Each mission has seven challenges, judged from a single night:
- **Unseen**: never spotted.
- **Merciful Hunger**: no kills.
- **Silent Night**: no alarm.
- **Before the Bell**: finish under the mission's par (`MissionInfo.Par`).
- **Unbroken**: no alarm and no loads (D131).
- **Every Thread**: every optional objective.
- **Apex**: finish on Apex difficulty.

Earned challenges are kept per mission and shown in the debrief, the Missions tab and the Record. Each pays one
Mark the first time it is earned (D130, superseding D94), so careful play is paid as well as hungry play.

## Tree rules
- Each tree has **tiers I–IV**. Tier II needs Awakening 3, tier III Awakening 5, tier IV (capstone) Awakening 8.
- A node requires its parent node.
- **Cross-tree synergy nodes** require nodes in two trees (marked ⟷).
- Respec: free between missions (encourages experimenting with builds on replays).

Node id convention used in code: `tree.node` (e.g. `shade.smother`).

---

## PREDATOR — the body
*Stalk, pounce, feed, terrify.*
| Id | Tier | Type | Cost | Name | Effect |
|---|---|---|---|---|---|
| `predator.stalker` | I | Passive | 1 | **Stalker** | While gliding within 4 m *behind* a human, their detection gain from you is 0. Feeding from behind is 30% faster. |
| `predator.pounce` | I | Active (15 blood) | 2 | **Pounce** | Leap onto a human within 7 m (12 m when dropping from 3+ m height) and pin them: instantly starts a feed. Lands with noise radius 4 m. |
| `predator.pounce_silent` | II | Mod | 1 | *Soft Landing* | Pounce makes no noise. |
| `predator.gorge` | II | Passive | 1 | **Gorge** | Feeding 40% faster. Drain gives +25% blood. |
| `predator.scent` | II | Passive | 1 | **Scent of Blood** | Wounded, bleeding and dazed humans, hounds and blood trails are visible through walls within 30 m. |
| `predator.rend` | III | Active (20) | 2 | **Rend** | Instantly kill one *alerted* or facing human in melee. Loud (8 m). No blood. The answer to "I got caught". |
| `predator.terror` | III | Passive | 2 | **Terror** | Anyone who sees you kill or feed (except Vigil and Bulwarks) **panics** instead of raising the alarm, and flees screaming toward the nearest light. |
| `predator.herd` | III | Mod | 1 | *Herding* | Panicking humans flee **away from you** in the direction you face, so you can drive them into traps, dark, or other guards (spreading panic). |
| `predator.bound` | III | Passive | 1 | **Bound** | Roof-leap range 3 cells; climb speed doubled. |
| `predator.apex` | IV | Capstone (45) | 3 | **Apex Hunt** | 8 s: everyone else moves at 35% speed. Every feed in it is instant; each kill refunds 15 blood. |
| `predator.dread_feast` | IV | ⟷ Sanguis.sense | 2 | **Dread Feast** | Draining in view of others drives them mad with fear. The witnesses are Panicked for 20 s and their heartbeats are revealed. |

## SHADE — the dark
*Darkness, light-theft, impossible movement.*
| Id | Tier | Type | Cost | Name | Effect |
|---|---|---|---|---|---|
| `shade.smother` | I | Active (8) | 2 | **Smother** | Extinguish a light within 16 m silently (no noise; guards still notice it's dark). |
| `shade.smother_lingering` | II | Mod | 1 | *Lingering Dark* | Smothered lights can't be relit for 60 s. Lamplighters fail and become confused. |
| `shade.smother_chain` | III | Mod | 1 | *Black Main* | Smothering a gas lamp puts out every lamp on the same gas main. |
| `shade.umbral` | I | Passive | 2 | **Umbral Step** | A third Shadow Dash charge (the dash itself is baseline, D155). After a dash that ends in darkness she stays unseen for 1 s. |
| `shade.dash_bars` | II | Passive | 1 | *Between Bars* | Shadow Dash carries her through bars. |
| `shade.nightblood` | II | Passive | 1 | **Nightblood** | In darkness, regenerate 1 HP/s for free (no blood). |
| `shade.gloom` | II | Active (18) | 2 | **Gloom** | A sphere of supernatural darkness (5 m radius, 15 s) at a point within 18 m. Vision cones don't penetrate it. Lights inside go out while it lasts. |
| `shade.mist` | III | Active toggle (6 + 3/s) | 2 | **Mist Form** | Become mist: pass through bars, vents and gaps under doors; slow (70%); can't interact; in darkness undetectable, in light *noticed as strange fog* (Suspicious only). Can't cross running water. |
| `shade.eclipse` | IV | Capstone (45) | 3 | **Eclipse** | Every light within 25 m dies and can't be relit for 30 s. Humans in the area are **Blinded** (sight 2 m) for 8 s. |
| `shade.shroud` | IV | ⟷ Dominion.mesmerize | 2 | **Shroud of Sleep** | Humans inside Gloom fall asleep after 3 s (feedable, wake in 30 s). |

## DOMINION — the mind
*Lures, mesmerism, thralls, lies.*
| Id | Tier | Type | Cost | Name | Effect |
|---|---|---|---|---|---|
| `dominion.beckon` | — | Active (6) | 0 (story, M03) | **Beckon** | Whisper to a human within 20 m. They walk alone to a point you choose (within 10 m of them), then look around (Suspicious) for 6 s. |
| `dominion.beckon_mimic` | I | Mod | 1 | *Familiar Voice* | Beckoned humans believe their superior called them: they don't become Wary afterwards. |
| `dominion.mesmerize` | I | Active (12) | 2 | **Mesmerize** | A human within 10 m freezes for 10 s; their cone closes. You can feed on them from any side. |
| `dominion.mesmerize_forget` | II | Mod | 1 | *Lethe* | Mesmerised and Sipped humans forget: they don't report on waking and don't become Wary. |
| `dominion.thrall` | II | Active (25) | 2 | **Enthrall** | Take a mesmerised, dazed or unaware-from-behind human as a **thrall** you command (see GAME_DESIGN §6). Max 1. |
| `dominion.thrall_second` | III | Mod | 1 | *Second Puppet* | Max 2 thralls. |
| `dominion.false_orders` | III | Thrall command | 1 | **False Orders** | Your thrall orders another guard of their faction to a point you choose; the guard goes there and holds for 30 s. |
| `dominion.puppet_strike` | III | Thrall command | 1 | **Puppet Strike** | Your thrall kills an adjacent human. They are then seized or flee; witnesses blame the thrall, not "the beast". |
| `dominion.court` | IV | Capstone (40) | 3 | **Court of Night** | Every human within 10 m is mesmerised for 8 s. Max 3 thralls. |
| `dominion.living_lie` | IV | ⟷ Sanguis.puppet | 2 | **Living Lie** | Corpse Puppets also deliver False Orders and pass officer checks. |

## SANGUIS — the blood
*Blood magic, evidence, traps, healing.*
| Id | Tier | Type | Cost | Name | Effect |
|---|---|---|---|---|---|
| `sanguis.sense` | I | Active hold (1/s) | 2 | **Blood Sense** | See every heartbeat within 30 m through walls, coloured by state, with patrol routes ahead. Audio: heartbeats. |
| `sanguis.sense_free` | II | Mod | 1 | *Hunter's Pulse* | Blood Sense costs nothing while standing still. |
| `sanguis.mend` | I | Active (20) | 1 | **Bloodmend** | Instantly heal 50% HP anywhere (even in light/combat). |
| `sanguis.clean` | I | Passive | 1 | **Clean Feeder** | Drain leaves no blood stain; dazed victims don't bleed (no trail). |
| `sanguis.snare` | II | Active (15) | 2 | **Blood Snare** | Place a rune within 8 m (max 2). The first human to cross it collapses *Dazed* (silent). Lay it ahead of a coordinated strike. |
| `sanguis.hemorrhage` | II | Active (30) | 2 | **Hemorrhage** | Burst the veins of a human within 10 m: silent kill, but a **huge blood pool** and gruesome corpse (anyone who sees it panics or goes to Lockdown). |
| `sanguis.puppet` | III | Active (18) | 2 | **Corpse Puppet** | A corpse within 6 m rises and resumes its owner's patrol (or stands at its post) for 40 s, fooling observers at > 4 m. Then it collapses where it stands. |
| `sanguis.false_trail` | III | Mod | 1 | *False Trail* | Blood Snare can instead be placed as a blood trail that leads hounds and trackers to a point. |
| `sanguis.silverblood` | III | Passive | 1 | **Silverblood** | Silver and holy damage halved; holy auras no longer block Sanguis abilities. |
| `sanguis.communion` | IV | Capstone (0) | 3 | **Red Communion** | Drain every corpse and dazed human within 12 m at once (lethal to the dazed), refilling blood. Their blood becomes a crimson mist (darkness 10 s). |
| `sanguis.vessel` | IV | ⟷ Predator.gorge | 2 | **Vessel** | Max blood +50; overfeeding (feeding at full blood) converts extra blood to HP above max (up to +30). |

---

## Design checks (anti-redundancy)
Every problem has 3+ tree answers, and no single ability solves everything:
| Problem | Predator | Shade | Dominion | Sanguis |
|---|---|---|---|---|
| Stationary guard facing you | Pounce from a height | Gloom then walk past | Mesmerize | Hemorrhage (evidence!) |
| Lit street | Rush between cover | Smother / Black Main | Thrall turns lamps off | Corpse puppet "lamplighter" |
| Two guards watching each other | Apex Hunt / Terror | Gloom over one | False Orders | Snare + Beckon |
| Hounds | Rend | Mist (smell still works: no!) → Dash breaks line of sight | Hounds immune to Dominion | False Trail |
| Locked/threshold door | Roof route | Mist under door | Thrall invites | Puppet corpse "opens" (Living Lie) |
| Getting caught | Rend / Terror | Dash to dark / Eclipse | Court of Night | Bloodmend, run |

Countermeasures (Dossier) pressure each tree:
Predator → paired patrols, bulwarks. Shade → caged lamps, censers, sunstone. Dominion → ward charms.
Sanguis → salt lines, priests.
