using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShadowQuality = UnityEngine.ShadowQuality;
using Vespertine.AI;
using Vespertine.Audio;
using Vespertine.Controls;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Mission;
using Vespertine.Progression;
using Vespertine.Save;
using Vespertine.Stealth;
using Vespertine.UI;
using Vespertine.View;
using Vespertine.Visual;

namespace Vespertine.Core
{
    public enum RootState { Boot, MainMenu, Hub, Mission, Ending }

    /// <summary>
    /// The single object in the Main scene. Builds every system, owns the top-level state machine
    /// (main menu → Refuge → mission → debrief → Refuge … → ending) and the campaign's lifetime.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        public RootState State { get; private set; } = RootState.Boot;
        public const string Version = "0.3.0";

        Volume _volume;
        ColorAdjustments _colour;
        Bloom _bloom;
        Vignette _vignette;
        float _saveCampaignT;

        // ================================================================== boot
        void Awake()
        {
            if (Game.Root != null && Game.Root != this) { Destroy(gameObject); return; }
            Game.Root = this;
            Application.runInBackground = true;

            Game.Settings = SettingsData.Load();
            Game.Settings.ApplyGraphics();
            Game.Input = new GameInput();
            Game.Lights = new LightSystem();
            Game.Noise = new NoiseSystem();

            Child<TacticalCamera>("Camera", go => { go.tag = "MainCamera"; go.AddComponent<Camera>(); });
            // the ear has its own object: AudioManager parks it above the pivot each frame, and on the camera that moved
            // the camera too, so everything projecting to the screen in Update saw the wrong view until LateUpdate
            var ear = new GameObject("Ear");
            ear.transform.SetParent(transform, false);
            ear.AddComponent<AudioListener>();
            Child<LevelRuntime>("Level");
            Child<AIDirector>("AI");
            Child<MissionController>("Mission");
            Child<AudioManager>("Audio");
            Child<UIManager>("UI");
            Child<ConeRenderer>("Cones");
            Child<SenseRenderer>("Sense");
            Child<AuraRenderer>("Auras");
            Child<ExposureRims>("Rims");
            Child<IlseDisc>("Disc");
            Child<BoundaryGlint>("Glint");
            Child<IntentPaths>("Paths");
            Child<HoverWatchers>("Watchers");
            Child<ReadTestRunner>("ReadTest");
            Child<MoveTestRunner>("MoveTest");
            BuildPostProcessing();
            ApplyRenderSettings();
        }

        T Child<T>(string name, System.Action<GameObject> pre = null) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            pre?.Invoke(go);
            return go.AddComponent<T>();
        }

        void Start()
        {
            if (Game.Level == null) Game.Level = GetComponentInChildren<LevelRuntime>();
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            GoToMainMenu();
            if (SmokeTest.Requested) gameObject.AddComponent<SmokeTest>();
        }

        void OnApplicationQuit()
        {
            if (Game.Campaign != null && State != RootState.Boot) SaveSystem.SaveCampaign(Game.Campaign);
            Game.Settings?.Save();
        }

        // ================================================================== rendering
        void BuildPostProcessing()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            _bloom = profile.Add<Bloom>(true);
            _bloom.intensity.Override(0.9f);
            _bloom.threshold.Override(0.95f);
            _bloom.scatter.Override(0.65f);
            _colour = profile.Add<ColorAdjustments>(true);
            _colour.postExposure.Override(1.4f);
            _colour.contrast.Override(8f);
            _colour.saturation.Override(-18f);
                        _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.32f);
            _vignette.smoothness.Override(0.5f);
            _vignette.color.Override(new Color(0.02f, 0f, 0.03f));
            var lift = profile.Add<LiftGammaGain>(true);
            lift.lift.Override(new Vector4(1f, 1f, 1.02f, 0f));
            lift.gain.Override(new Vector4(1.03f, 1f, 0.97f, 0f));
        }

        /// <summary>Applies the graphics part of Settings that needs scene objects (quality, shadows, post).</summary>
        public void ApplyRenderSettings()
        {
            var s = Game.Settings;
            if (s == null) return;
            s.ApplyGraphics();
            int q = Mathf.Clamp(s.QualityLevel, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
            QualitySettings.shadows = s.ShadowQuality == 0 ? ShadowQuality.Disable : s.ShadowQuality == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = s.ShadowQuality == 0 ? 0f : s.ShadowQuality == 1 ? 45f : 70f;
            }
            if (_volume != null)
            {
                _volume.enabled = s.PostProcessing;
                _colour.postExposure.Override(1.4f + s.Brightness);
            }
            var cam = Game.Cam != null ? Game.Cam.Cam : null;
            if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = s.PostProcessing;
            Game.UI?.ApplyScale();
        }

        /// <summary>Short red pulse on the vignette when the player is hurt or seen.</summary>
        public void PulseVignette(Color c, float strength)
        {
            if (_vignette == null) return;
            _pulse = Mathf.Max(_pulse, strength);
            _pulseColour = c;
        }
        float _pulse; Color _pulseColour;

        // ================================================================== frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (Game.Campaign != null && (State == RootState.Hub || State == RootState.Mission))
            {
                if (State == RootState.Hub || !Game.MenuPaused) Game.Campaign.PlayTime += dt;
                _saveCampaignT += dt;
            }

            // music follows the world
            var audio = Game.Audio;
            if (audio != null)
            {
                MusicMood mood = MusicMood.Menu;
                if (State == RootState.Mission && Game.InMission)
                {
                    int alarm = Game.AI != null ? Game.AI.Alarm : 0;
                    mood = alarm >= 2 ? MusicMood.Alert : alarm == 1 ? MusicMood.Tension : MusicMood.Calm;
                    if (Game.Mission.Ended) mood = Game.Mission.Won ? MusicMood.Calm : MusicMood.None;
                }
                else if (State == RootState.Hub) mood = MusicMood.Calm;
                else if (State == RootState.Ending) mood = MusicMood.Menu;
                audio.Mood = mood;
            }

            if (_vignette != null)
            {
                _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 1.5f);
                _vignette.intensity.Override(0.32f + _pulse * 0.25f);
                _vignette.color.Override(Color.Lerp(new Color(0.02f, 0f, 0.03f), _pulseColour, _pulse));
            }
        }

        // ================================================================== state transitions
        public void GoToMainMenu()
        {
            // quitting a test map from the pause menu ends it too
            if (Playtest && State == RootState.Mission) { Game.Mission?.Teardown(); SaveCampaign(); EndPlaytest(); }
            State = RootState.MainMenu;
            Game.Mission?.Teardown();
            Game.ResetPauses();
            Game.UI.SetHudVisible(false);
            ShowMenuBackdrop();
            Game.UI.ShowMainMenu();
        }

        void ShowMenuBackdrop()
        {
            // the main menu sits over a slow drift across the first map, if there is one
            var cam = Game.Cam;
            if (cam == null) return;
            cam.Locked = true;
            cam.Cam.backgroundColor = Util.Hex("#05060a");
            Game.Audio?.SetAmbience("amb_night", "amb_drip");
        }

        public void QuitToMenu()
        {
            if (State == RootState.Mission && Game.InMission && !Game.Mission.Ended)
            {
                Game.UI.Confirm("Quit to the main menu?", Game.Campaign != null && Game.Campaign.Ironblood
                        ? "Ironblood: leaving abandons this night."
                        : "Progress since your last save will be lost.",
                    "Quit", () => { Game.Mission.Abandon(); SaveCampaign(); GoToMainMenu(); }, "Stay", null);
                return;
            }
            if (State == RootState.Mission) Game.Mission.Teardown();
            SaveCampaign();
            GoToMainMenu();
        }

        public void QuitGame()
        {
            SaveCampaign();
            Game.Settings?.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SaveCampaign()
        {
            if (Game.Campaign != null) SaveSystem.SaveCampaign(Game.Campaign);
            _saveCampaignT = 0f;
        }

        /// <summary>Starts a fresh campaign in a profile slot (overwriting it).</summary>
        public void NewGame(int profile, Difficulty d, bool ironblood, bool startFirstMission = true)
        {
            SaveSystem.Profile = profile;
            SaveSystem.DeleteProfile(profile);
            var c = CampaignState.NewGame(d);
            c.Ironblood = ironblood;
            Game.Campaign = c;
            SaveSystem.SaveCampaign(c);
            if (startFirstMission) StartMission(Missions.All[0].Id);
            else ShowHub();
        }

        /// <summary>Loads a profile. Resumes its newest mission save unless <paramref name="toHub"/>.</summary>
        public void ContinueProfile(int profile, bool toHub = false)
        {
            var c = SaveSystem.LoadCampaign(profile);
            if (c == null) { Game.UI.Alert("Cannot load", "That campaign could not be read." + (SaveSystem.LastError != null ? "\n" + SaveSystem.LastError : "")); return; }
            SaveSystem.Profile = profile;
            Game.Campaign = c;
            if (!toHub)
            {
                var latest = SaveSystem.Latest(profile);
                if (latest != null && !c.Ironblood && Missions.Get(latest.MissionId) != null)
                {
                    LoadMissionSave(profile, latest.Slot);
                    return;
                }
                // a brand-new campaign that never finished night one goes straight back into it
                if (c.MissionIndex == 0 && c.Records.Count == 0) { StartMission(Missions.All[0].Id); return; }
            }
            ShowHub();
        }

        public void LoadMissionSave(int profile, string slot)
        {
            var s = SaveSystem.ReadMission(slot, profile);
            if (s == null) { Game.UI.Alert("Cannot load", "That save could not be read." + (SaveSystem.LastError != null ? "\n" + SaveSystem.LastError : "")); return; }
            if (SaveSystem.Profile != profile || Game.Campaign == null)
            {
                SaveSystem.Profile = profile;
                Game.Campaign = SaveSystem.LoadCampaign(profile) ?? Game.Campaign;
            }
            StartCoroutine(Transition(() =>
            {
                Game.UI.ClearScreens();
                Game.UI.HideModals();
                EnterMissionState();
                Game.Mission.LoadSave(s);
            }));
        }

        // ------------------------------------------------------------------ playtest maps (§44)
        /// <summary>A playtest map is up: the campaign is a throwaway one and every save goes to a sandbox folder.</summary>
        public bool Playtest { get; private set; }
        string _ptRoot;
        int _ptProfile;
        CampaignState _ptCampaign;

        /// <summary>Plays <paramref name="id"/> for a test on a fresh campaign, with saves redirected to <c>playtest/</c>
        /// in the save folder. The test logs still go beside the real saves. Ends after the debrief or on quitting.</summary>
        public void StartPlaytest(string id)
        {
            if (!Playtest)
            {
                _ptRoot = SaveSystem.RootOverride; _ptProfile = SaveSystem.Profile; _ptCampaign = Game.Campaign;
                Playtest = true;
            }
            SaveSystem.RootOverride = System.IO.Path.Combine(SaveSystem.BaseDir, "playtest");
            SaveSystem.Profile = 0;
            SaveSystem.DeleteProfile(0);
            Game.Campaign = CampaignState.NewGame(Difficulty.Hunter);
            StartMission(id);
        }

        void EndPlaytest()
        {
            if (!Playtest) return;
            SaveSystem.RootOverride = _ptRoot; SaveSystem.Profile = _ptProfile; Game.Campaign = _ptCampaign;
            _ptCampaign = null;
            Playtest = false;
        }

        public void StartMission(string id)
        {
            if (Game.Campaign == null) { Debug.LogWarning("[Root] StartMission without a campaign; creating one."); Game.Campaign = CampaignState.NewGame(Difficulty.Hunter); }
            if (Resources.Load<TextAsset>("Missions/" + id) == null) { Game.UI.Alert("Not available", "This mission's map is not in this build yet."); return; }
            SaveCampaign();
            StartCoroutine(Transition(() =>
            {
                Game.UI.ClearScreens();
                Game.UI.HideModals();
                EnterMissionState();
                if (!Game.Mission.Begin(id)) ShowHub();
            }));
        }

        void EnterMissionState()
        {
            State = RootState.Mission;
            Game.ResetPauses();
            Game.UI.SetHudVisible(true);
        }

        public void RestartMission()
        {
            if (Game.Mission == null || Game.Mission.Info == null) return;
            StartCoroutine(Transition(() =>
            {
                Game.UI.ClearScreens();
                Game.UI.HideModals();
                EnterMissionState();
                Game.Mission.Restart();
            }));
        }

        /// <summary>Back to the Refuge. With <paramref name="abandon"/> the current night's progress is discarded.</summary>
        public void ReturnToHub(bool abandon)
        {
            if (State == RootState.Mission && Game.Mission != null)
            {
                if (abandon && !Game.Mission.Won) Game.Mission.Abandon();
                else Game.Mission.Teardown();
            }
            if (Game.Campaign == null) { GoToMainMenu(); return; }
            SaveCampaign();
            StartCoroutine(Transition(ShowHub));
        }

        void ShowHub()
        {
            State = RootState.Hub;
            Game.Mission?.Teardown();
            Game.ResetPauses();
            Game.UI.SetHudVisible(false);
            ShowMenuBackdrop();
            Game.UI.ShowHub();
        }

        /// <summary>The debrief's Continue: the next night, or the ending after the last one.</summary>
        public void AfterDebrief(MissionResult r)
        {
            if (Playtest) { Game.Mission.Teardown(); SaveCampaign(); EndPlaytest(); GoToMainMenu(); return; }
            var c = Game.Campaign;
            bool last = r != null && Missions.IndexOf(r.MissionId) == Missions.All.Count - 1;
            if (last && c != null && !c.Flag("ending_seen"))
            {
                c.SetFlag("ending_seen");
                Game.Mission.Teardown();
                SaveCampaign();
                ShowEnding(EndingId());
                return;
            }
            // the first time a night is won, she dreams the day through before the Refuge
            var dream = r != null && r.Won && c != null ? Interludes.For(r.MissionId, c) : null;
            if (dream != null && Interludes.Record(dream, c))
            {
                Game.Mission.Teardown();
                SaveCampaign();
                StartCoroutine(Transition(() =>
                {
                    State = RootState.Hub;
                    Game.UI.ShowDream(dream, () => ReturnToHub(false));
                }));
                return;
            }
            ReturnToHub(false);
        }

        /// <summary>Which of the six endings the campaign has earned (choice made in the vault of M14).</summary>
        public string EndingId()
        {
            var c = Game.Campaign;
            if (c == null) return "dawn";
            string choice = c.Flag("abbess_free") ? "free" : c.Flag("abbess_consume") ? "consume" : "destroy";
            return c.Ending(choice);
        }

        public void ShowEnding(string id)
        {
            State = RootState.Ending;
            Game.Mission?.Teardown();
            Game.ResetPauses();
            if (Game.Campaign != null) { Game.Campaign.SetFlag("ending_" + id); SaveCampaign(); }
            Game.UI.ShowEnding(id);
        }

        IEnumerator Transition(System.Action mid)
        {
            Game.UI.FadeOut(0.25f);
            yield return new WaitForSecondsRealtime(0.28f);
            try { mid(); }
            catch (System.Exception e) { Debug.LogException(e); }
            yield return null;
            Game.UI.FadeIn(0.45f);
        }
    }
}
