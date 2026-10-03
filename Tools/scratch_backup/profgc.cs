UnityEditorInternal.ProfilerDriver.enabled = false;
int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex, first = System.Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex, last - 90);
var acc = new System.Collections.Generic.Dictionary<string, double>();
int frames = 0;
for (int f = first; f <= last; f++)
{
    using (var v = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory, false))
    {
        if (!v.valid) continue;
        frames++;
        var stack = new System.Collections.Generic.List<int>(); stack.Add(v.GetRootItemID());
        var kids = new System.Collections.Generic.List<int>();
        while (stack.Count > 0)
        {
            int id = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1);
            string nm = v.GetItemName(id);
            double self = v.GetItemColumnDataAsFloat(id, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory);
            acc[nm] = (acc.ContainsKey(nm) ? acc[nm] : 0) + self;
            v.GetItemChildren(id, kids); stack.AddRange(kids);
        }
    }
}
var top = acc.OrderByDescending(kv => kv.Value).Take(60).Select(kv => kv.Key + "=" + (kv.Value / frames).ToString("0"));
return frames + " frames | " + string.Join(" ; ", top);
