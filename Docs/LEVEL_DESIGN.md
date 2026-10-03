# VESPERTINE — Level Design

## 1. Rules
1. **Three routes minimum** to every primary objective. At least one is vertical (roofs/walls), one goes through
   darkness or the underground, and one uses manipulation (thrall, lure, light).
2. **Light pools are the walls of the level.** Draw the lamp network before the patrols. Every lit chokepoint
   has a way to darken it (lamp, valve, thrall, ability) *and* a way around it.
3. **Feeding opportunities are placed, not random.** Each area has at least one *isolatable* human: a lone
   post, a drunk, a privy visitor, a sleeping guard. The player should plan around *"who can I feed on safely?"*
4. **Patrol loops ≤ 40 s.** No waiting simulator. Long routes become two short loops with a hand-off.
5. **Readable clusters.** At most 3 cones overlap any chokepoint. If more are needed, one of them must be
   manipulable (lamp, lure).
6. **Escape vertical.** Every combat-likely zone has a climbable surface within ~6 m (Act I–II) so mistakes are
   recoverable without reloading.
7. **Evidence disposal.** Canals, wells, crates, dark corners: each area offers somewhere to put a body.
8. **Secrets** reward vertical or off-route exploration (rooftop shrines, sealed crypts, dead drops).
9. **Countermeasure hooks.** Every Dossier countermeasure has a systemic effect in every mission (wards, censers, salt,
   caged lamps, shorter daze, guards who look up, a lower lockdown threshold). From M07 (the first night the Dossier
   can answer) through M13, every map also carries dormant `group cm_*` blocks that add bodies when that countermeasure
   is active:
   - `cm_rooftop`: 1–2 sentries pacing a long wall or roof run.
   - `cm_paired`: partners spawned on existing patrol routes.
   - `cm_inquest`: an inquisitor on a new loop.
   - `cm_censer`: two alchemist posts at crossings or doors.

   New routes and spawns stay at least 10 cells from the player start. Partners on routes that already exist are
   exempt, because they add no new ground. `DossierContentTests` enforces both. M14 has none: there's no Vigil
   under the cathedral.
10. **Story in the space.** Notes, barks and staged scenes (two orderlies discussing "the ones that woke up").

## 2. Map file format (`Resources/Missions/<id>.txt`)
Sections begin with `@name`. Comments start with `#!` (only at line start).
Cell = **2 m**. Coordinates are `x y` = column, row (row 0 = top/north). +x = east, +y(row) = south.
Every character of a map row is a column, a leading space included: a row indented by one space shifts everything
after it one column east of where the entities think it is (the gym's first draft lost its doorway that way).

```
@mission
id = m01
title = The Drowned Ward
subtitle = Coldwater Institute — lower ward
act = 1
ambient = 0.06
ambientColor = #1a2135
fog = 0.03
blood = 10
music = drowned
briefing = They left you with the dead. ...
dawn = 0            #! seconds until dawn (0 = none)
moon = 0.35         #! optional: directional moonlight intensity. Drop it (M14: 0.05) under open-topped vaults,
                    #! or the moon lights the floor through the missing ceiling
dossier = all       #! optional: every countermeasure the Dossier has evidence for (weight ≥ 2), not just the
                    #! night's pick. Used by M12, where Vane has read all of it

@legend              #! optional per-mission overrides: char = tilekind
Z = wall

@map
##########
#..p.....#
...

@entities
player 3 4 face=E
npc o1 orderly 10 5 face=S route=r1 [wary] [asleep] [post] [id-specific flags]
route r1 loop | 10 5 wait=3 look=N | 20 5 | 20 12 wait=4
route r2 pingpong | ...
light l1 gaslamp 12 6 [group=a] [off] [caged]
prop crate 4 4 / prop well 6 7 hide / prop bed ...
door d1 7 3 [locked] [key=k1] [threshold] [open]   #! starts shut unless `open`; E opens/closes (D121)
valve v1 15 15 group=a           #! toggles lamp group a
valve v2 15 16 gate=g1 [noise=18] [wheel=<item id>] [sealed=Why_text]   #! wheel=: unusable until that item is taken
lever lv1 10 10 gate=g1
gate g1 12 10 [rise=1.7] [why=Barred._Text_for_the_interact_key]
note n1 5 5 "Title" "Body text..."
secret s1 8 8 "The Abbess's shrine" lore
bell b1 20 3
spawn sp1 30 2 [type=watchman] [count=2] [vigil|novigil] [face=W]   #! lockdown reinforcement point
zone z1 10 10 4 3 [restricted]   #! x y w h
group cm_rooftop                  #! following entities belong to group until 'endgroup'
endgroup

@objectives
primary escape "Escape through the culvert" reach 40 2 2 2
optional record "Find your patient record" interact n2
optional mercy "Kill no one" nokill
primary steal "Steal the manifest" interact doc1
primary kill_pell "Drain Sergeant Pell" kill pell
primary kidnap "Carry Penrose to the boat" deliver penrose 40 30 2 2
primary bells "Silence the bells" interact_all b1 b2 b3

@script
on start: say Ilse "Cold. So cold..."
on enter z1: bark o1 "Did you hear that?"
on complete record: say Ilse "So that's what they did."
on objective escape: win
```

### Tile legend (base)
| Char | Kind | Height | Walk | Blocks vision | Notes |
|---|---|---|---|---|---|
| ` ` | void | – | no | yes | outside the playable area (dark) |
| `.` | street (cobble) | 0 | yes | no | |
| `,` | dirt | 0 | yes | no | quieter |
| `g` | grass | 0 | yes | no | quieter |
| `c` | carpet (indoor) | 0 | yes | no | quietest |
| `_` | wood floor (indoor) | 0 | yes | no | |
| `:` | tile floor (indoor) | 0 | yes | no | |
| `w` | shallow water | 0 | yes | no | splashing: Rush noise ×1.5 |
| `~` | canal (running water) | – | no | no | uncrossable; bodies dumped vanish |
| `=` | bridge / dock | 0 | yes | no | |
| `#` | stone wall | 3 | top | yes | climbable (ClimbAny) |
| `u` | upper floor / gallery | 3 | top | yes | indoor raised floor |
| `h` | house | 4.5 | roof | yes | low roofs; pipes reach them from the street |
| `H` | building | 6 | roof | yes | |
| `T` | tower | 9 | top | yes | |
| `\|` | iron bars / fence | 2.5 | no | no | mist passes; Shadow Dash with Between Bars |
| `v` | vent / grate in wall | 3 | no | yes | mist passes |
| `b` | hedge / reeds | 0 | yes | yes (concealment) | |
| `x` | crates / clutter | 1.2 | no | no | |
| `+` | doorway | 0 | yes | no | place `door` entities on these |
| `s` | stairs / ladder base | 0 | yes | no | link to adjacent raised tile (everyone) |
| `p` | drainpipe / ivy base | 0 | yes | no | climb link to adjacent raised tile (vampire) |
| `P` | pipe from a raised tile up | (raised) | top | | climb link from this raised tile to a higher neighbour |
| `S` | stairs from a raised tile up | (raised) | top | | ladder link from this raised tile to a higher neighbour |

### Vocabulary (authoritative list: `MissionValidator`)
- **Entities:** player, npc, corpse (`type=` archetype), light, prop, hide, door, gate, valve, generator, lever, note,
  secret, item, bell, window, boat, use, spawn, listen, trap, zone, stain.
- **NPC flags:** `route=`, `face=`, `name=` (underscores → spaces), `wary`, `asleep`, `sit`, `post`, `nolantern`,
  `notable`, `friendly` (story ally: never perceives, cannot be fed on), `dazed[=s]`, `hp=`, `group=`,
  `scan` (a post that sweeps its gaze), `paired=<npc>` (notices the partner missing: incapacitated, hidden or more
  than 14 m away for 6 s, then searches), `follow=<npc>` (walks at the leader's heel while relaxed, e.g. a hound
  and its handler; implies `paired`; falls back to its own route/post if the leader is downed). Both are checked by
  the validator. `hunts=<s>` makes a tracker who needs no alarm to find Ilse (see Hunts below).
- **Reinforcement points:** on Lockdown every `spawn` point brings in `count` of `type` (one fewer on Merciful,
  one more on Apex; three or more watchmen are led by a sergeant), wary and searching where she was last seen. Once
  the Vigil hunts her (M07 on, or header `vigil = 1`; `vigil = 0` opts out) the point flagged `vigil` (else the
  first) also sends a Vigil hunter and hound. Put points at map edges,
  out of the player's likely sight, on a human-walkable cell (the validator checks).
- **Interactables:** `sealed[=Reason_text]` makes any interactable (except bells) unusable, showing the reason;
  the script `unseal`s it (e.g. the M04 registry until the bells are cut).
- **Zones:** `zone id x y w=.. h=..` with `restricted`, `home=<door id>[,<door id>…]` (vampire cannot enter until
  one of the doors is invited — the threshold law; one invitation opens every listed door, so a house with a front
  and a kitchen door is one home), used by `on enter <zone>`.
- **Listen points:** `listen id x y radius=R speakers=a,b [delay=] [rest=] "Title" "a: line" "b: line" …`. The
  speakers loop the exchange while relaxed and within R+3 m of the point; Ilse must be within R (and 4.5 m
  vertically) from the first line to the last. Hearing it fires `interact <id>`. Keep every speaker's route inside
  R+3 or the exchange breaks whenever they wander.
- **Traps (arranged accidents):** `trap id x y radius=R victims=a,b [drop=x,y] [verb=] [time=] [noise=]
  [fallnoise=] "Name" [armed=Text] [sealed=Why]`. Ilse rigs it (an interaction); the next listed victim to stand
  within R dies with cause `accident` (dropped to `drop` if given), fires `interact <id>.sprung`, and leaves a body
  that witnesses read as a misfortune (they investigate, they don't raise the alarm). `kill <npc> accident`
  objectives complete only on an accident. `sweep` (traps over running water: a sluice, rotten planks over a drain)
  sends the body away as if `dispose canal`: there is nothing for anyone to find, and it plays a splash, not a thud.
- **Masks:** an `item` of type `mask` lets Ilse pass as a guest to civilians, servants and the Watch (not the Vigil,
  not Warded NPCs, not anyone already alerted or searching) while she walks, keeps her feet on a floor, isn't
  feeding, carrying or misting, and stays out of `restricted` zones. The HUD says which rule she is breaking.
- **Objective types:** reach, escape, deliver, survive, flag, item, interact, interact_all, kill, kill_all, feed,
  feed_types, dispose, protect, valves, escort, nokill, nokill_type, noalert, nodetect, nobodies, nofeed, feedonly,
  nolockdown, noholy, take, hpfloor, evacuate. `feedonly <blood type…>` fails on feeding from any other kind of blood.
  **Conduct** objectives (nokill, nokill_type, noalert, nodetect, nobodies, nofeed, feedonly, nolockdown, noholy, protect)
  start satisfied, fail on a breach, complete on a win and never block one.
  `count=N` on interact_all / feed_types / kill_all needs only N of the listed parts; `dispose <canal|hide|any>
  count=N` counts bodies given to water or hidden. `valves <id…>` completes when every listed gas main is shut at
  once (progress drops if a lamplighter reopens one). `deliver <npc> x y w h` completes when the NPC lies alive
  (or dead, with `dead`) in the rect and is not being carried; dropping them there never tips them into water.
- **Gas mains:** `valve id x y group=<lamp group> ["Name"]`. Shutting it puts out every lamp in the group and marks
  them gas-cut: a lamplighter who notices one walks to the valve, works it, and the group relights. Put the valve
  on or near that lamplighter's circuit so the counter is visible, and give the player a way to deal with him.
- **Sunstone and the generator (Act III):** a `sunstone` light burns Ilse (and fledglings) inside 0.85 × radius
  with line of sight; it can't be snuffed or smothered. Cells are 2 m, so radius 5 burns about 2 cells each way:
  use 7 to seal a 4-cell corridor. Each sunstone with an id gets a thrall-only `<id>.smash` interactable unless
  flagged `nosmash`: a smashed lamp stays dark for good (and guards who pass it grow wary). `generator id x y
  group=<light group> ["Name"]` is a valve with a breaker: throwing it cuts every light in the group, and an
  engineer (an NPC with the Relights flag) walks to it and restores it. Restoring never relights smashed lamps. Post
  the engineer near the breaker so the window is short unless the player deals with him. The validator requires
  `group=` to match some light's group.
- **Prisoners and escort (Act III):** `npc … prisoner [stand] [freed=Bark_text]` sits shackled (Captive). Ilse or a
  thrall breaks the shackles (`<id>.free`); then the prisoner follows Ilse on foot (click to wait or follow). They
  can't climb, string out behind her in a line, are put back in chains by any guard who reaches them, and leave the
  map on reaching the escort rect (`<id>.out`). Guards who see a following prisoner raise `evidence prisoner` and
  search. Undead prisoners (fledglings) balk at burning light, back out of it, and burn if caught in it. The Drain key
  over a freed one turns it loose (`<id>.loose`): it hunts the nearest human, kills the unready (cause `fledgling`,
  counted as hers for Terror), and dies on an Alerted or Searching guard. `escort <npc…> x y w h [count=N] [loose=1]`
  completes as the listed prisoners get out (N of them, default all). With `loose=1` a turned-loose one counts too.
  It fails once too many are dead or lost. Prisoners ignore panic, alarms and distractions.
- **Searchlights (M10 on):** `light id x y searchlight from=x,y[,h] sweep=x1,y1,x2,y2,… [speed=2.5] op=<npc>
  [group=g]`. An arc lamp on a tower (`from`, h metres above that cell's surface) throws a moving pool of light (its
  GameLight sits at the pool, so the light field and every guard's perception treat it as a lamp). While its
  operator is at the lamp (within 3 m of `from`, alive, not dazed, enthralled or mesmerised), the pool ping-pongs
  along the sweep cells with a 1.2 s dwell at each end. When the operator turns suspicious it swings to what caught
  his attention, and when he is searching or alerted it follows the last known position. The operator sees anything
  inside 0.72 × radius of the pool, at any distance. A dead or dazed operator leaves the beam where it stopped.
  Cutting the `group=` at a `generator` turns it off. Post operators with `sentry post nolantern` on the tower. Keep the
  sweep away from the player start and from long stretches of the only route.
- **Sunbeams (M14):** `light id sunbeam x y from=x,y[,h] path=x1,y1,x2,y2,… start=<mission s> over=<s>`. Dawn
  through a high window: before `start` it is still night. From `start` the light thickens over 20 s and the pool
  creeps along the `path` cells for `over` seconds, a burning ring (like a sunstone) that can't be snuffed, smothered
  or cut at a generator. The HUD names it "Sunlight". Stagger the starts so the floor closes up a beam at a time, and
  keep at least one path in the shade for the whole night. (`dawn` is an alias for the kind.)
- **Any light** takes `intensity=` (and `radius=`) to override its kind's default. A `lamp` indoors has a small
  radius on 2 m tiles: to spill through a doorway it must stand within a few cells of it, clear of the jamb (the gym's
  room lamp is `radius=10` two cells from its door): M14's grate moonbeam at
  `intensity=10` is what makes the Abbess legible in a black vault.
- **Stair pitfall:** a stair `s` next to a `T` wall ramps onto the wall top, giving roof access and a drop into
  whatever is on the far side. M14's south-aisle stair let her past both locked ways down until it moved one cell
  off the wall. Check every `s` near a tall wall with `reach` before locking the doors that were meant to matter.
- **Timed sequences:** `countdown <timer> <secs> "Label"` starts a script timer and shows it on the HUD (label and
  m:ss); it survives saves. `blast x y <radius m> ["death text"]` is an explosion: a fireball, a shake, a gunshot-loud
  noise, death (cause `blast`) for every non-friendly NPC inside the radius, panic out to 2.5×, and a loss with the
  death text if Ilse is inside. `swapprop x y <type>` replaces the static prop on that cell (the `gasholder` becomes a
  `gasholder_wreck`); the swap is stored as a mission flag, so loads rebuild it.
- **Evacuation:** `npc … evac=x,y` gives an NPC somewhere to run. When it panics (a scream, a body, Ilse, or the
  script action `evacuate [ids]`, which panics every evac NPC or just the listed ones) it runs to that cell, leaves
  the map and fires `interact <id>.evac`. The `evacuate <npc…> [count=N]` objective completes as they get out and
  fails if one of them dies (a `blast` counts).
- **Hunter squads (M13 on):** `squad id x y [name=Display_name] [nerve=N]` declares a squad and its rally point
  (x y, at a map edge). Members are `npc … squad=<id>`; the one flagged `lead` leads (else the first listed), and the
  rest take wedge slots in file order (the last is the rearguard, who glances back). Only the leader needs a
  `route=`. Members in a `group` not yet active join when it is switched on (`group raid`), so a squad can arrive
  by script. Start nerve: `nerve=`, or 80 Vigil / 60 Watch / 50 other, less 6 per point of Terror over Rumour (up to
  5), plus 30 if Vane is among them. Shocks: a man downed in sight 25 (unseen 20), a feed seen 20 (+15 drained, +20
  Terror build, +25 Dread Feast), a lamp she puts out within 18 m 10, another squad routing within 30 m 20, Apex Hunt
  30, and seeing her at Awakening 9+ 12 (every 8 s). After each shock the squad rings up back to back for 14 s, then
  hunts as one. Nerve recovers 1.5/s after 25 s calm, to at most 60% of the start. At 0 it breaks. Survivors rout to
  the rally point and leave the map; Fearless men rout too, but Vane does not. Objective `break <squad…> [how=rout]`
  completes when every listed squad is broken (`how=rout` fails one that was killed to the last man). Events:
  `broken <squad|any>`, `routed <squad|any>`. Script action `rout <squad>` breaks it at once. The squad's nerve,
  ring and routed state survive saves.
- **Tobias world flags:** `tobias_alive`, `tobias_thrall`, `tobias_lost` (his campaign state) can be used in rule
  conditions and `if=`.
- **Header `nowindows = x y w h; …`:** suppresses the window decals on raised tiles in those rects. Use it for
  raised blocks that aren't buildings (a gas holder's casing, a bastion wall).
- **`use id x y "Name" verb=Verb_text [time=s] [noise=m] [prop=<prop>]`:** a one-off scripted deed (burn the notes).
  It fires `interact <id>`. `window` and `boat` are the same class without the prop.
- **Objective categories:** `primary` (required), `optional`, `hidden` (a primary that is invisible until a script
  `reveal`s it; still required to win; the validator rejects a hidden objective that no rule reveals). `needs=<objective>` orders them; on `reach` and `escape` it gates completion; on `take` and `interact` a completion that comes too early is held (a saved `held.<id>` flag) and lands as soon as the needs are met. Each need is a completed objective or a mission flag (`needs=abbess.destroy`), because a hidden objective is live before it is revealed.
- **Script events:** start, enter, complete, objective, fail, kill, feed, interact, timer, alert, lockdown, flag,
  discover, spotted, secret, thrall (arg = npc id), evidence (arg = trail, stain, pool, body…), dispose, death, choice, down (arg = npc id,
  fired once per NPC the first time it is dazed, enthralled or killed by any means: "deal with him however you
  like", e.g. M07 `on down ferry: unseal skiff`), framed (arg = npc id or `any`: Ilse's kill of a non-Vigil NPC with a
  living, standing Vigil man within 6 m on the same floor; a thrall's strike counts, and the man is named in a toast),
  crescendo (arg `any`).
- **Script actions:** say, bark, toast, hint, document, reveal, objective, complete, fail, flag, unflag, campaign_flag,
  grant (`grant <skill id>` — a story gift: free, kept through a respec, equipped if a slot is free; thrall
  commands are learned but not slotted), ferry (`ferry x y [yaw]` — fade, move Ilse,
  snap the camera; used by skiffs), lore, group, ungroup, spawn, remove, unlock, lock, seal, unseal,
  route (`route <npc> <route id>` — give a living NPC a new patrol, persisted in saves), invite, open, close, light, lightgroup, alarm, lockdown, timer,
  canceltimer, countdown, blast, evacuate, swapprop, crescendo (`crescendo <s>`: for that long every noise but bells
  and lures carries a quarter as far; a HUD chip shows it, and `crescendo any` fires), dawn, hunt (`hunt <npc> on|off|now`), weather (`weather rain|clear`), kill, wake, investigate (`investigate <npc> x y` — send a living NPC to search a tile, as if he'd heard something there; the readability gym uses it to keep a seeker searching), routechoice (`routechoice <id> <safe zone> <other zone>` — a §44.1 route choice: with *Readability test mode* on, the first of the two zones Ilse enters is logged as her choice; otherwise nothing), give, vitae, mark, terror, rumour, tobias, camera, music, sound, checkpoint, choice
  (`choice <id> "prompt" key "text" key "text" …` — a modal the player must answer; the answer sets the mission and
  campaign flag `<id>.<key>` and fires `choice <id>.<key>`; the validator checks `on choice` args against the offered
  keys. The separator is `.` because rule headers split at the first `:`), win, lose. `on <event>:` rules fire once; `every <event>:` rules fire each time.
- **Campaign flags must matter.** A `campaign_flag` has to be read by a later mission (`if cf_<flag>` or a
  `group flag_<flag>` block) or by code (epilogue, endings, dreams). A mission reads only flags set on earlier
  nights. `CampaignFlagTests` enforces both rules; record-only flags are listed there.
- **Rule conditions:** `on <event> [arg] if a !b:` fires only while every name holds (a script flag is set or an
  objective is complete; `!` negates). A rule whose condition fails is not used up. The validator rejects names that
  are neither objectives nor flags the script sets. **World flags** are also accepted: `rumour` / `terror` (that
  reputation leads), `cm_<countermeasure>` (active from the Dossier), `cf_<campaign flag>`.
- **Conditional objectives:** `if=a,!b` on an objective line (world flags only) hides the objective entirely
  unless the conditions hold at mission start (e.g. M07 "Free the Faithful" `if=rumour`).
- **Running water and crossings (Act II):** the vampire nav never crosses canal cells: leaps refuse any gap over
  canal or void. Crossings are bridges (floor), boats (`boat` + `ferry`), and raised tiles that span the water
  (mill walls, houses built over a leat). Give every river 2–3 crossings with different costs. Lone 1-cell raised
  tiles (a bridge arch, a gatepost) are standable (nav min region 0.4 m²), so arches can be climbed around a gate.
- **Hunts (M08):** an NPC with `hunts=<s>` (or switched on by `hunt <npc> on`) takes a fresh fix on Ilse's scent
  every `s` seconds (default 16): a random point within a radius of 40% of the distance to her (6–22 m), times 0.4
  if she is Bleeding, times 1.5 in rain, shrinking 12% per fix in a row (to 45%). The tracker walks there, and her
  `follow=` hounds range up to 8 m around the point for 9 s. A fix while Ilse is hidden or in mist breaks the scent:
  the run of fixes resets and the tracker goes back to her post. `hunt <npc> off` pauses; `hunt <npc> now` (a
  scripted give-away, e.g. a candle she left as bait) gives an immediate fix as if three fixes in. The player beats
  a hunt by hiding, misting, staying unhurt, waiting for rain, or by being found *where she wants to be found*: a
  trap on the tracker's path.
- **Weather:** header `rain = 1` starts the mission in rain; script `weather rain|clear` changes it (use `every timer`
  pairs for squalls). Rain is a stealth state: hounds and trackers smell at half range, blood-trail drops wash away
  after 30 s, footstep noise is 60%, and hunt fixes are 50% looser. The ambience switches to `amb_rain`.
- **`take <npc>`** objective: completes when the NPC is killed (any cause) or enthralled. Fires `thrall <npc>` on an
  enthral, so a script can tell the two apart (`on thrall hollin: campaign_flag hollin_thrall`).
- **`hpfloor <pct>`** conduct objective: fails the first time Ilse's vitality drops below pct% of its maximum.
- Every shipped mission file is validated by an EditMode test (`Mission_<id>`).
- Objective target checks: `deliver` needs an npc id and an area (`deliver <npc> x y [w h]`); `feed` takes `any`, an
  npc id or an archetype id (as does a script `on feed X`); `reach`, `escape` and `deliver` area values must be
  numbers.

## 3. Mission concepts
See CAMPAIGN.md for narrative and objectives. Layout notes per mission are kept as a header comment in each map
file.

## 4. Mission metrics (targets)
| Act | Map size (cells) | NPCs | Lights | Primary routes |
|---|---|---|---|---|
| I | 40×30 – 60×40 | 8–16 | 12–25 | 3 |
| II | 60×40 – 80×50 | 16–28 | 20–40 | 3–4 |
| III | 70×50 – 90×60 | 22–36 | 25–50 | 4 |
| IV | 80×60 – 100×70 | 28–45 | 30–60 | 4–5 |

## Theatre props (M11 on)

- `seats x y face=N`: a row of four theatre seats, 1.5 m wide, solid to walk round.
- `curtain x y`: a stage curtain for a 12 m proscenium opening, drawn open, with a valance at about 7 m.
- `scenery x y`: a painted flat on a brace.
- `balustrade x y [yaw=90]`: a 1 m box-front rail. It blocks walking and doesn't block sight. Place it on a cell edge
  (x.45 / x.55): the prop takes the height of the cell it rounds to.

## Cathedral props (M14)

- `abbess x y`: the Abbess kneeling in the black water, arms drawn out by four chains to the floor. Pair it with a
  `use abbess … sealed=…` interactable for the final choice.
- `vat x y`: a vitae vat, 2 m tall, with a pipe. `still x y`: Saule's distillation still.
- `bricks x y`: a bricked-up stair arch; pair it with a `use … time=8 noise=16` deed to break through.
