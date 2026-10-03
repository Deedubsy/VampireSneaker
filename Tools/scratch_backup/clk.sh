#!/bin/bash
# usage: clk.sh TEXT — click the button whose label starts "N." in any UIDocument (choice dialogs)
S=/tmp/claude-1000/-mnt-e-ShadowTactics/707807bc-a0d1-48d2-80ab-faf908724265/scratchpad/tools
bash $S/ev.sh "var r=\"none\"; foreach (var d in UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>()) { UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Button>(d.rootVisualElement).ForEach(b => { if (r==\"none\" && b.text.StartsWith(\"$1\")) { using (var e=UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { e.target=b; b.SendEvent(e); } r=b.text; } }); } return r;"
