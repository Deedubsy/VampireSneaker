# VESPERTINE — Where we are (2026-10-03)

A one-page handoff. The details live in TASK_LIST (rows), PROGRESS (what was seen), DECISIONS (why) and
KNOWN_ISSUES (what's still wrong). Numbers in use: next decision **D157**, next known issue **K45**, next task
row **R37**. Edit-mode tests: **292/292**.

## Done
- **The game:** all 14 missions authored and played start to end; campaign, refuge, progression, Dossier, Codex,
  menus, saves, music, Windows build and the smoke test (PASS).
- **Controls redesign:** WASD camera-relative movement, follow camera, doors, sneak and gait footsteps,
  BG3-style thrall control.
- **GAMEPLAY_REDESIGN up to the test gate:**
  - the stealth readability work (SR.1–SR.14: light knee, cones, grace, meter patterns, Spotted picture,
    accessibility), with the cone truth sweep clean on every mission;
  - the quick wins QW1–QW19;
  - Shadow Dash (D155), which replaced Shadowstep;
  - the Readability Test harness (D148) and the Movement Test kit (D156), with the **Test maps** main-menu
    entry on a sandboxed throwaway campaign.

## Waiting on human testers (the gate)
Nothing in "After the gate" starts until R22 passes.

1. **WASD Stealth Readability Test (R22, §44.1):** 6–8 testers. Turn on Settings → Gameplay → *Readability test
   mode*, then Main menu → *Test maps*: the Readability Gym, M02, M05. The verdict is in
   `readability/report.txt` beside the saves.
2. **WASD Movement Test (§44):** 5 new players, keyboard and pad. Turn on *Movement test mode*, then *Test maps*:
   the Movement Gym, then M02. The verdict is in `movement/report.txt`.
3. **Play-checks by a person:** R17, R18, R20, R23, R24 and R25 are built and checked by script (`[~]`). The
   Readability Test covers them.

## Waiting on the owner's decisions
4. **Route choices** for M02 and M05: which routes the Readability Test asks about.
5. **K42, dash height:** the Rise is 4 m, which clears walls (3 m) but not houses (4.5 m). Raise it to 4.5?
6. **K41, the Ironblood reward:** pay its +1 Mark when the campaign is finished, or for each mission finished
   without a load?

## After the gate
7. **R30, the blood remembers (§48):** a sip shows the victim's next minute; a drain shows what they knew.
8. **K40:** dormant countermeasure body groups (`group cm_*`) in M03–M06, so the Dossier's answers there add people.
9. Hunt and mission tuning, as GAMEPLAY_REDESIGN orders it.

## Small fixes, any time
10. **K37:** a cone is drawn flat at its guard's height, so a rooftop guard's cone floats over the street.
11. **K39:** a lantern-lit far band trails a moving lantern by about 0.35 m (the 0.25 s ground-light cache).
12. **K43:** a settings change made during a Test map is saved in the sandbox folder.
13. **K44:** a gym loaded from the dev console pays rewards to the real campaign.

## Working notes
- Unity 6000.6.0f1, URP. In the editor, saves go to `Temp/DevSaves` (DevSandbox), never to AppData.
- Helper scripts for driving the editor are backed up in `Tools/scratch_backup/`; read its `RESUME_PROMPT.md` first.
- Source: https://github.com/Deedubsy/VampireSneaker
