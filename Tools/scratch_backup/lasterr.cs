var le=System.Type.GetType("UnityEditor.LogEntries,UnityEditor"); var et=System.Type.GetType("UnityEditor.LogEntry,UnityEditor");
int c=(int)le.GetMethod("GetCount").Invoke(null,null); le.GetMethod("StartGettingEntries").Invoke(null,null);
string outp="count="+c+"\n"; var e=System.Activator.CreateInstance(et);
for(int i=System.Math.Max(0,c-3);i<c;i++){ le.GetMethod("GetEntryInternal").Invoke(null,new object[]{i,e}); var m=(string)et.GetField("message").GetValue(e); outp+="--"+i+": "+m.Substring(0,System.Math.Min(900,m.Length))+"\n"; }
le.GetMethod("EndGettingEntries").Invoke(null,null); return outp;
