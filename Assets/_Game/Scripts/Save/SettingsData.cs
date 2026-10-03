using System.IO;
using UnityEngine;

namespace Vespertine.Save
{
    [System.Serializable]
    public class SettingsData
    {
        public float MasterVolume = 0.8f, MusicVolume = 0.6f, SfxVolume = 0.9f, AmbienceVolume = 0.7f, VoiceVolume = 1f;
        public int ShadowQuality = 2;         // 0 off, 1 hard, 2 soft
        public int QualityLevel = -1;         // -1 = leave as is
        public bool Fullscreen = true;
        public int ResolutionIndex = -1;
        public bool VSync = true;
        public int TargetFps = 120;
        public float UiScale = 1f;
        public bool EdgePan = false;           // the camera follows whoever she controls; edge panning is opt-in look-ahead
        public float CameraPanSpeed = 1f, CameraRotateSpeed = 1f;
        public bool InvertRotate;
        public bool Subtitles = true;
        public bool TutorialHints = true;
        public bool ShowAllConesHotkey = true;
        /// <summary>Show the cones that matter right now with no key held (QW15, SR.6).</summary>
        public bool ContextualCones = true;
        /// <summary>Draw where each nearby light stops exposing her (QW16, SR.4).</summary>
        public bool ExposureRims = true;
        /// <summary>Ilse's ground disc: exposed or hidden, who is watching, and what her next step does (SR.7, SR.8).</summary>
        public bool IlseDisc = true;
        public bool ScreenShake = true;
        public bool PostProcessing = true;
        public float Brightness = 0f;          // -1..1 (exposure)
        public string BindingOverrides = "";
        public bool HighContrastCones;
        /// <summary>Accessibility (SR.12): every cone in view shows all the time, as if the cone key were held.</summary>
        public bool AlwaysAllCones;
        /// <summary>Accessibility (SR.12): the cone key toggles the tactical view instead of being held.</summary>
        public bool ConesKeyToggles;
        /// <summary>Accessibility (SR.12): a guard's state is carried by the shape of his cone's origin arc as well as its
        /// colour (dashed when suspicious, wide when he knows), so colour is never the only channel.</summary>
        public bool ShapeCodedCones;
        /// <summary>Dev (§44.1): run the WASD Stealth Readability Test - freeze probes, cause questions after a
        /// detection, and a log of each map under readability/ in the save folder.</summary>
        public bool ReadabilityTest;
        /// <summary>The WASD Movement Test (§44): records wall-sticks, door stops, camera use and detections at cone
        /// edges, asks two questions when a map ends, and keeps a log of each map under movement/ in the save folder.</summary>
        public bool MovementTest;
        /// <summary>Settings layout version. 1: direct (WASD) control replaced click-to-move and the Nightplan (D114) -
        /// edge panning off and old key overrides dropped, since WASD now moves her rather than the camera.</summary>
        public int Version;
        public const int CurrentVersion = 2;

        static string PathFor => System.IO.Path.Combine(Vespertine.Save.SaveSystem.RootOverride ?? Vespertine.Save.SaveSystem.BaseDir, "settings.json");   // a smoke run keeps its settings in its own scratch root

        public static SettingsData Load()
        {
            try
            {
                if (File.Exists(PathFor))
                {
                    var s = JsonUtility.FromJson<SettingsData>(File.ReadAllText(PathFor));
                    if (s != null) { s.Migrate(); return s; }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("Settings load failed: " + e.Message); }
            return new SettingsData { Version = CurrentVersion };
        }

        public void Migrate()
        {
            if (Version < 1)
            {
                EdgePan = false;
                BindingOverrides = "";
            }
            // v2 (D119): Interact moved to E, rotate to Z/X, thralls hold/follow to H and Ctrl became sneak. Old
            // overrides were made against the old layout and would now collide, so they are dropped once.
            if (Version < 2) BindingOverrides = "";
            Version = CurrentVersion;
        }

        public void Save()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(PathFor)); File.WriteAllText(PathFor, JsonUtility.ToJson(this, true)); }
            catch (System.Exception e) { Debug.LogWarning("Settings save failed: " + e.Message); }
        }

        public void ApplyGraphics()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : TargetFps;
            if (!Application.isEditor)
            {
                var res = Screen.resolutions;
                if (ResolutionIndex >= 0 && ResolutionIndex < res.Length)
                    Screen.SetResolution(res[ResolutionIndex].width, res[ResolutionIndex].height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                else Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }
    }
}
