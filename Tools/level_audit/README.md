# Level audit analyser

Offline analysis of the mission maps (`Assets/_Game/Resources/Missions/mNN.txt`), used by `Docs/LEVEL_DESIGN_AUDIT.md`.
No Unity needed. Python 3 with `pip install pillow numpy networkx`.

| Command | Output (in `Docs/level_audit/`) |
|---|---|
| `python3 report.py [m01 …]` | `mNN_layout.png`, `mNN_analysis.png`, `metrics.json` |
| `python3 report.py variants [m01 …]` | `variants.json`: safest and roof-preferred routes with and without the dormant `cm_*` bodies, and with thresholds opened |
| `python3 locks.py` | Every locked door and gate: are both sides reachable without it at A1, Ghost and Typical Awakening? |
| `python3 special.py` | `special.json`: M05 carry vs the gas mains, M09 escort vs the sunstones, perimeter use |
| `python3 feeding.py` | `feeding.json`: dark drag pockets beside posts, drop-feed ledges along patrols |

`mapmodel.py` lists the game rules it mirrors (TileDefs, NavBuilder, Vampire.AreaMask, GameLight, LightSystem,
DetectionMath, Archetypes). If any of those change, update it, or the numbers drift from the game.

Known approximations: line of sight is a 2D march over cell heights; sub-cell props are ignored; searchlights are swept as
intermittent cover; carried lanterns and boats are not modelled. Confirm anything decisive in the editor with
`Tools/scratch_backup/reach.sh` at the right Awakening.
