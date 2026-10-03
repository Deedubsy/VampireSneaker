#!/bin/bash
# usage: trace.sh "x1,z1 x2,z2 ..." — hold the left button and drag through those world points (y = her feet), release
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
for p in $1; do x=${p%,*}; z=${p#*,}
bash $S/ev.sh "var sp=Vespertine.Core.Game.Cam.Cam.WorldToScreenPoint(new UnityEngine.Vector3(${x}f,P.Feet.y,${z}f)); var st=new UnityEngine.InputSystem.LowLevel.MouseState{position=new UnityEngine.Vector2(sp.x,sp.y), delta=new UnityEngine.Vector2(1,1)}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left); UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, st); return \"\";" >/dev/null
sleep 0.15; done
bash $S/ev.sh 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState{position=UnityEngine.InputSystem.Mouse.current.position.ReadValue()}); return "";' >/dev/null
sleep 0.3
bash $S/ev.sh 'var r=Vespertine.Core.ReadTestRunner.Instance; var a=r.Current.Answers[r.Current.Answers.Count-1]; return "asking="+r.Asking+" last: Q"+a.Q+" "+a.Target+" given="+a.Given+" ok="+a.Correct;'
