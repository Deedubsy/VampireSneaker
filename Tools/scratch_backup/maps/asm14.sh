S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
cd $S/maps; python3 m14.py >/dev/null && (cat m14_head.txt; echo "@map"; cat m14.grid; echo; cat m14_tail.txt m14_script.txt) > /mnt/e/ShadowTactics/StealthVampire/Assets/_Game/Resources/Missions/m14.txt
