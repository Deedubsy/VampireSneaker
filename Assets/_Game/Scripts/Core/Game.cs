using UnityEngine;

namespace Vespertine.Core
{
    /// <summary>Static service locator for the single-scene game. Systems register themselves on creation.</summary>
    public static class Game
    {
        public static GameRoot Root;
        public static Level.LevelRuntime Level;
        public static Stealth.LightSystem Lights;
        public static Stealth.NoiseSystem Noise;
        public static Player.Vampire Player;
        public static AI.AIDirector AI;
        public static Mission.MissionController Mission;
        public static Progression.CampaignState Campaign;
        public static Save.SettingsData Settings;
        public static Audio.AudioManager Audio;
        public static UI.UIManager UI;
        public static Controls.GameInput Input;
        public static View.TacticalCamera Cam;

        public static bool InMission => Mission != null && Mission.Running;
        /// <summary>Is this Dossier countermeasure (cm_*) in force?</summary>
        public static bool Countermeasure(string id) => (Campaign != null && Campaign.Countermeasures.Contains(id)) || MissionCounters.Contains(id);

        /// <summary>Countermeasures in force for this mission only (M12 `dossier = all`: Vane's own fortress).</summary>
        public static readonly System.Collections.Generic.List<string> MissionCounters = new System.Collections.Generic.List<string>();

        /// <summary>Every countermeasure in force: the campaign's plus this mission's.</summary>
        public static System.Collections.Generic.List<string> ActiveCountermeasures
        {
            get
            {
                var l = new System.Collections.Generic.List<string>();
                if (Campaign != null) l.AddRange(Campaign.Countermeasures);
                foreach (var m in MissionCounters) if (!l.Contains(m)) l.Add(m);
                return l;
            }
        }

        // ---------- time control ----------
        static bool _menuPause, _tacticalPause, _modalPause, _closingPause;
        public static bool MenuPaused { get => _menuPause; set { _menuPause = value; ApplyTime(); } }
        public static bool TacticalPaused { get => _tacticalPause; set { _tacticalPause = value; ApplyTime(); } }
        public static bool ModalPaused { get => _modalPause; set { _modalPause = value; ApplyTime(); } }
        /// <summary>A won mission's closing lines play over a frozen world. Owned by the mission, so a menu opening and
        /// closing over them (which recomputes the menu and modal pauses) cannot thaw it (K4).</summary>
        public static bool ClosingPaused { get => _closingPause; set { _closingPause = value; ApplyTime(); } }
        public static bool AnyPause => _menuPause || _tacticalPause || _modalPause || _closingPause;
        public static float SlowMo = 1f;

        public static void ApplyTime()
        {
            Time.timeScale = AnyPause ? 0f : SlowMo;
        }

        public static void ResetPauses()
        {
            _menuPause = _tacticalPause = _modalPause = _closingPause = false;
            SlowMo = 1f;
            ApplyTime();
        }
    }
}
