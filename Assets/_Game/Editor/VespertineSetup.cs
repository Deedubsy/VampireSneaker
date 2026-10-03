using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Vespertine.Core;

namespace Vespertine.EditorTools
{
    /// <summary>
    /// One-shot project setup: base materials, UI theme + PanelSettings, and the Main scene.
    /// Idempotent: safe to run again after pulling changes. Menu: Vespertine/Setup Project.
    /// </summary>
    public static class VespertineSetup
    {
        const string Res = "Assets/_Game/Resources";
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("Vespertine/Setup Project")]
        public static string Run()
        {
            Directory.CreateDirectory(Res + "/Materials");
            Directory.CreateDirectory(Res + "/UI");
            Directory.CreateDirectory("Assets/_Game/Scenes");
            var log = new System.Text.StringBuilder();
            Materials(log);
            Panel(log);
            Scene(log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        // ---------------------------------------------------------------- materials
        static void Materials(System.Text.StringBuilder log)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var overlay = Shader.Find("Vespertine/Overlay");
            if (!lit || !overlay) { log.AppendLine("ERROR: shaders not found"); return; }

            var m = Mat("Lit", lit);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);

            var o = Mat("Overlay", overlay);
            o.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            o.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            o.renderQueue = 3100;
            EditorUtility.SetDirty(o);

            var x = Mat("OverlayXray", overlay);
            x.SetFloat("_ZTest", (float)CompareFunction.Always);
            x.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            x.renderQueue = 3200;
            EditorUtility.SetDirty(x);

            var a = Mat("OverlayAdd", overlay);
            a.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            a.SetFloat("_DstBlend", (float)BlendMode.One);
            a.renderQueue = 3150;
            EditorUtility.SetDirty(a);
            log.AppendLine("materials ok");
        }

        static Material Mat(string name, Shader shader)
        {
            var path = $"{Res}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            return m;
        }

        // ---------------------------------------------------------------- UI
        static void Panel(System.Text.StringBuilder log)
        {
            var tssPath = Res + "/UI/VespertineTheme.tss";
            if (!File.Exists(tssPath))
            {
                File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(tssPath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
            var path = Res + "/UI/PanelSettings.asset";
            var p = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(p, path);
            }
            p.themeStyleSheet = theme;
            p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            p.referenceResolution = new Vector2Int(1920, 1080);
            p.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            p.match = 0.5f;
            p.sortingOrder = 10;
            EditorUtility.SetDirty(p);
            log.AppendLine(theme ? "panel ok" : "panel ok (theme failed to import)");
        }

        // ---------------------------------------------------------------- scene
        static void Scene(System.Text.StringBuilder log)
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Object.FindAnyObjectByType<GameRoot>();
            if (root == null) new GameObject("Vespertine").AddComponent<GameRoot>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            log.AppendLine("scene ok");
        }
    }
}
