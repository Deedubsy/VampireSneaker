S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh 'Vespertine.Core.DevFrameProbe.Run(6f); return "s";' | tail -1 >/dev/null
sleep 10
bash $S/ev.sh 'return Vespertine.Core.DevFrameProbe.Report;' | tail -1
