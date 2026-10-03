Resume work on Vespertine (Unity 6000.6.0f1 URP project at /mnt/e/ShadowTactics/StealthVampire). Source:
https://github.com/Deedubsy/VampireSneaker (push as the Deedubsy GitHub user; commit and push only when asked).

The original brief still applies:
- act as Game Director and the whole team, and build the full commercial game (not an MVP);
- don't keep asking questions, and don't stop to ask whether to continue;
- keep the Docs/ files current (STATUS, GAME_DESIGN, TECHNICAL_DESIGN, CAMPAIGN, PROGRESSION, ENEMIES, LEVEL_DESIGN,
  TASK_LIST, PROGRESS, DECISIONS, KNOWN_ISSUES);
- write automated tests for deterministic systems;
- don't call anything "complete" just because it compiles.

Act freely inside the project folder, but ASK FIRST before changing anything outside it (e.g. C:/Users/Admin/AppData/LocalLow/...).
Reading is fine. Never use PlayerPrefs for scratch (it writes outside the project).

START WITH Docs/STATUS.md: where the work is and what's left. The rule that matters most: no Hunt or mission tuning
(including the cm_* body groups in M03–M06) until the WASD Stealth Readability Test (R22) passes with human testers.

SETUP: copy this folder into the new session's scratchpad as tools/. Then fix the old scratchpad path inside every
*.sh and maps/*.sh:
  sed -i "s#/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools#$NEW/tools#g; s#/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad#$NEW#g" *.sh maps/*.sh
Open the Unity Editor, then run `compile.sh 20` (rerun it if it prints "failed: ?") and `timeout 420 ./tests.sh`.
Expect 0 errors and 292/292.

HELPERS (in the editor, saves go to <project>/Temp/DevSaves via DevSandbox):
- compile.sh, tests.sh: stop play mode first with `./Tools/u.sh editor_stop` from the project.
- run.sh <mission>: stop, compile, play, start the mission (gym, movegym, m01–m14), dismiss the intro. It does not
  reset the campaign.
- ev.sh '<C#>': P = player, L = LevelRuntime, M = MissionController. Use `return`. No single quotes inside the code.
  Each call takes a few seconds.
  - Cells are 2 m: L.Data.CellToWorld(c,r,0), L.CellOf(world). Map files use x = column, y = row from the top.
  - Dev hooks: P.DebugMove (a Vector3?), P.DebugDash, Vampire.GodMode, Vampire.NoTarget.
  - Simulated key presses never reach the game (K27); call the handlers instead.
- shot.sh (game view to shot.png), snap.sh x z [dist pitch yaw], look.sh, con.sh warn|log N, errs.sh.
  The console keeps stale entries: log a marker and trust only what comes after it.
- sweep.sh <mission> <part>: the ability sweeps.
  - part 1 and part 6: m05
  - parts 2–4: m02
  - part 3: m08
  - part 5: m06
  - part 7: m01 (footsteps, doors, hiding, gate)
- camprun.sh <prefer>: a full campaign run M01 → ending.
- cone_check.sh / knee_check.sh (in Tools/): the cone truth sweep and the light-knee check.
- probe.sh Q2..Q5: opens a readability probe. trace.sh, click.sh, toe.sh, fit.sh, knee.sh: readability checks.
- prof.sh, profgc.sh, fps.sh: profiling. Tools/build.sh and Tools/smoke.sh: Windows build and its smoke test.
- Maps: maps/mNN.py, plus head, tail and script fragments, assembled by maps/asmNN.sh into
  Assets/_Game/Resources/Missions/mNN.txt. Edit the fragments, not the .txt.
  gens/gym_gen.py and gens/movegym.py generate gym.txt and movegym.txt.

STATE (2026-10-03): see Docs/STATUS.md. Next numbers are D157, K45 and R37.
- Everything up to the human-test gate is built:
  - SR.1–SR.14;
  - QW1–QW19;
  - Shadow Dash (D155);
  - the Readability Test harness (D148);
  - the Movement Test kit (D156), with Test maps on the main menu.
- Open now without the gate: K37, K39, K43 and K44 (small fixes).
- Waiting on the owner:
  - R22 and the Movement Test with people;
  - route choices for M02 and M05;
  - K42 (dash Rise 4 m or 4.5 m);
  - K41 (when Ironblood pays).
