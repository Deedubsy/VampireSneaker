#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Vespertine.Core
{
    /// <summary>
    /// Development check (P3/P4): samples frame times for a few seconds and reports the median, 95th percentile and
    /// worst frame, plus GC allocations per frame. Start with <c>DevFrameProbe.Run(seconds)</c>; read <see cref="Report"/>.
    /// Editor numbers include Editor overhead: use them to compare missions, not as a shipping budget.
    /// </summary>
    public class DevFrameProbe : MonoBehaviour
    {
        public static string Report = "";
        public static bool Running;
        readonly List<float> _ms = new List<float>();
        float _left, _skip = 0.5f;
        long _gc0;
        int _frames;

        /// <summary>Main-thread markers sampled each frame; the report names the worst frame's biggest ones.</summary>
        static readonly string[] Markers =
        {
            "PlayerLoop", "EditorLoop", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.PhysicsFixedUpdate", "PostLateUpdate.FinishFrameRendering", "Gfx.WaitForPresentOnGfxThread",
            "GC.Collect", "Shadows.RenderShadowMaps", "RenderPipelineManager.DoRenderLoop_Internal", "NavMeshManager",
            "Gfx.WaitForGfxCommandsFromMainThread", "Loading.ReadObject", "Shader.CreateGPUProgram", "Debug.Log",
        };
        readonly List<ProfilerRecorder> _rec = new List<ProfilerRecorder>();
        float _worst = -1f;
        string _worstDetail = "";

        void OnEnable() { foreach (var m in Markers) _rec.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, m, 1)); }
        void OnDisable() { foreach (var r in _rec) r.Dispose(); _rec.Clear(); }

        string MarkerLine()
        {
            var parts = new List<string>();
            for (int i = 0; i < _rec.Count; i++)
            {
                double ms = _rec[i].Valid ? _rec[i].LastValue * 1e-6 : -1;
                if (ms >= 2.0) parts.Add($"{Markers[i]}={ms:0}");
            }
            return string.Join(" ", parts);
        }

        public static void Run(float seconds = 6f)
        {
            if (Running) return;
            Running = true;
            Report = "sampling...";
            new GameObject("DevFrameProbe").AddComponent<DevFrameProbe>()._left = seconds;
        }

        void Update()
        {
            if (_skip > 0f) { _skip -= Time.unscaledDeltaTime; _gc0 = Profiler.GetMonoUsedSizeLong(); return; }
            _ms.Add(Time.unscaledDeltaTime * 1000f);
            if (Time.unscaledDeltaTime * 1000f > _worst) { _worst = Time.unscaledDeltaTime * 1000f; _worstDetail = MarkerLine(); }
            _frames++;
            _left -= Time.unscaledDeltaTime;
            if (_left > 0f) return;
            _ms.Sort();
            float med = _ms[_ms.Count / 2], p95 = _ms[Mathf.Min(_ms.Count - 1, (int)(_ms.Count * 0.95f))], worst = _ms[_ms.Count - 1];
            long heap = Profiler.GetMonoUsedSizeLong();
            int npcs = Game.AI != null ? Game.AI.Npcs.Count : 0;
            int lights = Game.Lights != null ? Game.Lights.All.Count : 0;
            Report = $"{_frames} frames: median {med:0.0} ms ({1000f / med:0} fps), p95 {p95:0.0} ms, worst {worst:0.0} ms; " +
                     $"mono heap {(heap - _gc0) / 1024f / _frames:0.0} KB/frame growth; {npcs} npcs, {lights} lights" +
                     $"\n  worst frame (previous frame's markers, ms): {_worstDetail}";
            Debug.LogWarning("[probe] " + Report);
            Running = false;
            Destroy(gameObject);
        }
    }
}
#endif
