# VESPERTINE — Enemy Roster

Archetype ids are used in level files (`type=`) and `Game.Data.Archetypes`.

## Shared stat legend
- **FOV**: cone angle. **Near/Far**: vision band ranges (m). **Hear**: hearing multiplier.
- **Morale**: `brave` (alerts), `low` (may panic on witnessing; 50%), `civilian` (always panics), `fearless`
  (Vigil: never panics).
- **Flags**: `LooksUp` (sees elevated targets in the far band), `Lantern` (carries a light, radius 5),
  `Relights` (relights dark lamps on route), `HolyAura` (radius 6: holy-blocked abilities fail inside, i.e.
  Shade, Dominion and Sanguis, though Silverblood frees Sanguis; Predator pounce, rend and apex still work. The aura
  does not burn; holy flame and sunstone lights do, D57), `Ward` (immune to Dominion), `Smell` (360° 5 m sense, follows blood trails),
  `Armored` (cannot be Sip/Drained from the front; Pounce fails unless from above), `Officer` (organises
  searches; notices thralls).

| Id | Name | Faction | HP | FOV | Near/Far | Weapon | Morale | Flags | Blood | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| `civilian` | Citizen | — | 1 | 100 | 4/10 | — | civilian | | common | Screams (18 m) & flees to light/guards. |
| `drunk` | Drunk | — | 1 | 80 | 3/7 | — | civilian | | drunk | Wanders; slow detection. |
| `sailor` | Sailor | — | 2 | 90 | 4/9 | — | low | | drunk | M03 docks. Ashore and in drink: brawls, shouts for the Watch, staggers. |
| `beggar` | Fevered Beggar | — | 1 | 90 | 4/9 | — | civilian | | fevered | Sits; coughs (masks noise). |
| `orderly` | Orderly | Institute | 2 | 90 | 5/13 | cudgel | low | Lantern | common | M01. Slow; melee only. |
| `watchman` | Watchman | Watch | 2 | 90 | 6/15 | musket | low | Lantern | soldier | Slow reload (5 s). |
| `sergeant` | Watch Sergeant | Watch | 3 | 100 | 6/16 | pistol | brave | Lantern, Officer | soldier | Leads searches (3 guards). |
| `lamplighter` | Lamplighter | Guild | 1 | 90 | 5/12 | — | low | Relights, Lantern | common | Walks a lamp circuit; relights; flees & shouts if threatened. |
| `priest` | Priest | Church | 2 | 100 | 6/14 | — | brave | HolyAura | priest | Rings bells; stands at shrines. The aura (6 m) is drawn as a pale gold ring on the ground. |
| `acolyte` | Acolyte | Church | 1 | 90 | 5/12 | — | low | Lantern | priest | Carries censer light. |
| `guest` | Guest | — | 1 | 100 | 4/11 | — | civilian | | drunk | Social crowds (M06, M11). |
| `servant` | Servant | — | 1 | 100 | 5/12 | — | civilian | | common | Moves between rooms; ideal thrall. |
| `soldier` | Militia Soldier | Watch | 3 | 90 | 7/17 | musket | brave | | soldier | Act II+ checkpoints. |
| `hunter` | Vigil Hunter | Vigil | 3 | 90 | 7/18 | silver crossbow | fearless | LooksUp, Flares | soldier | Throws flares at suspicious dark points (20 s light). |
| `hound` | Vigil Hound | Vigil | 2 | 360 (smell) | 5/— | bite | fearless | Smell | — | Can't be fed on (animal blood: 10, no humour). Barks (20 m). Follows trails. |
| `tracker` | Tracker (Hollin) | Vigil | 5 | 100 | 8/20 | silver crossbow | fearless | LooksUp, Smell, Ward, Officer | soldier | Boss M08. Starts Wary. Hunts (`hunts=`): needs no alarm to find her. |
| `alchemist` | Alchemist | Institute | 2 | 90 | 6/14 | garlic censer | low | Censer | occult | Garlic smoke (4.5 m) follows them: no mist or Shadow Dash inside it, and it forces her out of mist. |
| `scholar` | Institute Scholar | Institute | 1 | 90 | 4/11 | — | civilian | | occult | M09. Sits at desks and benches; screams for the orderlies. Clement is one (`prisoner`). |
| `engineer` | Generator Engineer | Institute | 2 | 90 | 5/13 | cudgel | low | Relights, Lantern | common | M09. Posted at the sunstone `generator`; when the breaker is thrown he walks to it and restores the current. |
| `worker` | Gasworker | Guild | 2 | 90 | 4/11 | — | civilian | | common | M10. Night shift at the retorts and sheds. With `evac=` they run off the site when frightened, or when the works whistle sends them all. |
| `fledgling` | Fledgling | — | 3 | 60 | 3/6 | — | civilian | Undead | — | M09 rescue subjects (`prisoner`). Can't climb, balk at and burn in sunstone light; can't be fed on. Turned loose: hunts the nearest human, dies on alerted guards. Ranged guards shoot it on sight. |
| `inquisitor` | Inquisitor | Vigil | 4 | 100 | 7/18 | pistol | fearless | LooksUp, Officer, Ward | priest | Reads bodies and dazed victims: each adds to the Dossier habit it shows, and a staged accident is treated as a killing. Exposes thralls within near sight. |
| `bulwark` | Bulwark | Vigil | 6 | 80 | 6/14 | halberd | fearless | Armored, Lantern | soldier | Slow, heavy light; must be taken from above or mesmerised (without ward). |
| `sentry` | Rooftop Sentry | Vigil/Watch | 2 | 100 | 7/18 | musket | brave | LooksUp, Lantern | soldier | Countermeasure group. |
| `notable` | (named) | varies | 2 | 90 | 6/14 | — | civilian | | notable | Mission targets: Penrose, Crane, Saule... |
| `vane` | Inquisitor-Captain Vane | Vigil | 6 | 110 | 8/22 | silver pistol | fearless | LooksUp, Officer, Ward, HolyAura | notable | Final antagonist. |

## Behaviour states
| State | Icon | Cone colour | Behaviour |
|---|---|---|---|
| Relaxed | — | green | Patrol / post / routine (talk, smoke, warm hands at braziers). |
| Suspicious | yellow "?" filling | yellow | Stop, turn toward stimulus, meter rises/decays. |
| Investigating | yellow "?" | yellow | Walk to stimulus point; look around 4–6 s; return; become **Wary**. |
| Alerted | red "!" | red | Shout (alerts 10/15/22 m by difficulty, drawn as a red ring), engage (shoot/melee), ring bell if near, chase. |
| Searching | orange "?" | orange | Go to last known position; check 3–5 nearby search points; 30–60 s; then Wary. |
| Panicked | white "!!" | none | Flee toward light/group, scream periodically (noise). Becomes Wary after 20 s. |
| Dazed | "z" | none | On ground; wakes after 45 s (25 s with countermeasure) and reports. Feedable. |
| Mesmerised | violet swirl | none | Frozen. Feedable from any side. |
| Thrall | violet eye | violet | Player-commanded. |
| Dead | — | — | Corpse (evidence). |

### Masks and accidents
- **Fooled by a mask:** NPCs of no faction (guests, servants, notables) and the Watch take a masked Ilse for a guest
  — her detection from them is zero — unless they are Warded (Vane) or already Alerted / Searching. The Vigil and
  the Church are never fooled. The mask fails while she runs, climbs, mists, feeds, carries a body or trespasses.
- **Accident bodies:** a body whose cause is `accident` (a sprung trap, or a scripted `kill`) makes a witness gasp
  and investigate. No report, no panic, no alarm. The evidence event `accident` fires.

## Group behaviour
- **Shout**: alerted humans alert all brave/officer allies within `DifficultyDef.ShoutRadius` metres (10 / 15 / 22, D124)
  with LOS or same room. The radius is drawn on the ground as a red ring (D135).
- **Alarm bells**: an alerted guard within 20 m of a bell runs to ring it → Lockdown.
- **Paired patrols**: partners expect each other at checkpoints; a missing partner → Wary investigation of the
  partner's last checkpoint.
- **Officers** take command of a search: assign up to 3 nearby guards to search points.
- **Lamplighters**: track their circuit's lamps; a dark lamp → go relight (5 s), even during searches. A lamp
  whose gas main is shut can't be relit there: they walk to the main and reopen it ("Who shut the main?").
- **Squads** (M13 on): 4–6 men in a wedge behind a leader, with a rearguard who glances back every 7 s. Whatever
  any man hears or finds becomes the leader's search, so the squad hunts together. They share one nerve. Losing a
  man, watching a feed, a lamp going out nearby or another squad routing past shakes it. A shaken squad rings up
  back to back for 14 s (360° facings), then hunts. At zero the squad breaks: survivors run for its rally point and
  leave the streets, Fearless or not. A downed leader is replaced by the next man standing. Vane, leading the close
  squad in M13, adds 30 nerve and never runs. See LEVEL_DESIGN for the numbers.

## Combat
- Muskets: 5 s reload, hit chance 70% at 5 m → 25% at 20 m, −30% if Ilse in darkness, −20% if she's rushing.
  Damage 18 (watch) / silver bolt 22 + no regen 10 s.
- Melee: 10 dmg/1.2 s.
- Ilse in combat can **Rush** to break line of sight, climb out of reach, or (with Rend) fight one at a time.
  Standing and fighting 3 guards is death.
- Death = mission failure → reload last save (quick/auto).

## Blood trails and hounds (M07 on)
- While Ilse is **Bleeding** (hit within the last few seconds, or below 33% vitality) and not concealed or in mist,
  she drops a trail drop every 1.8 m (at most 90 kept; the oldest dry up).
- A **Smell** NPC (hound, tracker) smells a drop within 8 m in any light, through fences. It barks, the
  `evidence trail` event fires, it turns Wary and runs along the drops (up to 5 drops / 4.5 m gaps per leg),
  spending them as it goes so it never doubles back, then smells on from where it stops.
- Sighted NPCs see drops like small stains at 60% of stain range.
- Hounds placed with `follow=<hunter>` walk at their handler's heel; a hound whose handler is downed
  notices within 6 s and searches.

## The tracker's hunt (M08, Sister Hollin)
- A hunting tracker takes a fix on Ilse's scent every 16–18 s and walks to a point near her. The fixes tighten the
  longer she keeps the scent (see LEVEL_DESIGN, Hunts): in the open, about three fixes put Hollin within a few
  metres. Blood makes it worse; rain makes it easier.
- At each fix her hounds **cast about**: after 1.5 s at heel they range to a point up to 8 m out and sniff for 9 s,
  then come back to heel. This is the window to get at Hollin alone.
- Hiding (a hiding spot) or mist when a fix is taken breaks the scent ("Lost her."): the run resets and Hollin goes
  back to her post until the next fix.
- **Ward tearing:** Hollin and inquisitors are Warded (Dominion fails on them). Feeding on a Warded NPC tears the
  charm loose: from then on Dominion works on them, including Enthrall on the dazed body.
- **Calm hounds:** when a handler is enthralled, her hounds settle at once (Relaxed, detection cleared) and keep to
  her heel, ignoring Ilse, since their handler's will is now Ilse's. A hound whose handler is *dead* searches.
- Hounds can't be caught in traps (they cross a sawn plank or a sluice safely; only a human's weight springs it).

## Searchlight operators (M10 on)
- Any NPC named as a searchlight's `op=` mans it: he stays at the lamp (never leaves his post to investigate or
  search) and turns with the beam.
- Anything inside the pool is in his sight at any range. Outside the pool his own eyes work as usual, so the tower's
  foot is his blind spot.
- His states steer the beam. A suspicion swings it to the point that caught his attention, and an alert or search
  rides it on the last known position at 5.5 m/s. Relaxed, it sweeps its route at about 2.5 m/s.
- Counters:
  - kill or daze him, or enthrall or mesmerise him: the beam freezes where it was
  - throw the generator that powers the group
  - in M10, the blackout after the blast also cuts them


## The Watch–Vigil split (M11 on)

If a Council death at the Opera is pinned on the Vigil (`framed` → campaign flag `watch_vigil_split`), Watch and
Vigil NPCs stop answering each other's shouts for the rest of Act III (`AIDirector.Split`). They still see Ilse,
answer their own side, and share a lockdown. A Watch patrol that spots her brings only Watch; the hunters arrive on
their own, later.

## Dr. Emeric Saule (M14)
`saule` archetype: a notable who doesn't flee his own laboratory. He walks a loop: his desk (45 s), the sunstone
ring, then the purge chamber. A `bulwark` warder follows him (`follow=saule`). When he sees her he shouts for the
ring. Cutting `gen_lab` darkens the ring and the lab lamps together, and he shouts about that too. There are three
ways to deal with him, and each writes a different flag for the epilogue:
- **kill** writes `saule_dead`.
- **the purge trap** writes `saule_dead` and `saule_purged`, and its line names the accident.
- **thrall or a non-lethal take** writes `saule_spared`.

Script pattern: `down` also fires on death, and `cf_` conditions are a snapshot taken at mission start. The script
therefore guards with local flags (`saule_killed`, `saule_bound`) so that only one outcome is written.

