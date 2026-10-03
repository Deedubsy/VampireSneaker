#!/bin/bash
# usage: click.sh '<C# expr giving a world Vector3 w>' — left-click there (mouse state events)
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "var w=$1; var sp=Vespertine.Core.Game.Cam.Cam.WorldToScreenPoint(w); var st=new UnityEngine.InputSystem.LowLevel.MouseState{position=new UnityEngine.Vector2(sp.x,sp.y)}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left); UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, st); return \"click \"+w;"
sleep 0.3
bash $S/ev.sh 'UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, new UnityEngine.InputSystem.LowLevel.MouseState{position=UnityEngine.InputSystem.Mouse.current.position.ReadValue()}); var r=Vespertine.Core.ReadTestRunner.Instance; var a=r.Current.Answers[r.Current.Answers.Count-1]; return "asking="+r.Asking+" last: Q"+a.Q+" "+a.Target+" given="+a.Given+" ok="+a.Correct+" err="+a.Error.ToString("0.00");'
