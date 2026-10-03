var tm = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var layers = tm.FindProperty("layers");
string[] names = {"Ground","Wall","Bars","Foliage","Character","Corpse","Interactable","Vent","Water","Overlay","Prop"};
for (int i=0;i<names.Length;i++) layers.GetArrayElementAtIndex(8+i).stringValue = names[i];
tm.ApplyModifiedProperties();
var nav = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
var areas = nav.FindProperty("areas");
string[] an = {"Climb","ClimbAny","Leap","Ladder","Mist","Threshold"};
float[] ac = {3,4,2,3,1,1};
for (int i=0;i<an.Length;i++){ var a=areas.GetArrayElementAtIndex(3+i); a.FindPropertyRelative("name").stringValue=an[i]; a.FindPropertyRelative("cost").floatValue=ac[i]; }
nav.ApplyModifiedProperties();
var s = nav.FindProperty("m_Settings");
var sn = nav.FindProperty("m_SettingNames");
var h = s.GetArrayElementAtIndex(0); h.FindPropertyRelative("agentRadius").floatValue=0.35f; h.FindPropertyRelative("agentHeight").floatValue=1.8f; h.FindPropertyRelative("agentClimb").floatValue=0.4f;
bool hasV=false; for(int i=0;i<sn.arraySize;i++) if(sn.GetArrayElementAtIndex(i).stringValue=="Vampire") hasV=true;
if(!hasV){ s.arraySize++; var v=s.GetArrayElementAtIndex(s.arraySize-1); v.FindPropertyRelative("agentTypeID").intValue=7777; v.FindPropertyRelative("agentRadius").floatValue=0.35f; v.FindPropertyRelative("agentHeight").floatValue=1.8f; v.FindPropertyRelative("agentClimb").floatValue=0.4f; v.FindPropertyRelative("agentSlope").floatValue=45f; sn.arraySize++; sn.GetArrayElementAtIndex(sn.arraySize-1).stringValue="Vampire"; }
nav.ApplyModifiedProperties();
UnityEditor.AssetDatabase.SaveAssets();
return "ok";
