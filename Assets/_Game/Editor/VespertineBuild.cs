using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vespertine.EditorTools
{
    /// <summary>
    /// Player builds. Only the Main scene ships: everything else (missions, materials, UI) loads from Resources.
    /// The result is written to Builds/last_build.txt. Menu: Vespertine/Build Windows, or batch mode
    /// (-executeMethod Vespertine.EditorTools.VespertineBuild.Windows). From the CLI against a live Editor, the
    /// Pipeline's own async `build` command does the same job (see Tools/build.sh).
    /// </summary>
    public static class VespertineBuild
    {
        public const string Scene = "Assets/_Game/Scenes/Main.unity";
        public const string OutDir = "Builds/Win64";
        public const string ResultPath = "Builds/last_build.txt";

        [MenuItem("Vespertine/Build Windows")]
        public static void Windows() => Build(false);

        [MenuItem("Vespertine/Build Windows (development)")]
        public static void WindowsDev() => Build(true);

        static void Build(bool development)
        {
            Directory.CreateDirectory(OutDir);
            PlayerSettings.productName = "Vespertine";
            PlayerSettings.companyName = "Vespertine";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = OutDir + "/Vespertine.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            string result;
            try
            {
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                result = $"{s.result} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize / (1024 * 1024)}MB " +
                         $"time={s.totalTime.TotalSeconds:0}s path={s.outputPath}";
                if (s.result != BuildResult.Succeeded)
                    foreach (var step in report.steps)
                        foreach (var m in step.messages)
                            if (m.type == LogType.Error || m.type == LogType.Exception) result += "\n" + m.content;
            }
            catch (System.Exception e) { result = "Exception " + e; }
            File.WriteAllText(ResultPath, result);
            Debug.Log("[Build] " + result);
        }
    }
}
