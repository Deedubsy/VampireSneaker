using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Vespertine.Core;

namespace Vespertine.Audio
{
    public enum MusicMood { None, Menu, Calm, Tension, Alert }

    /// <summary>Pooled one-shots (2D/3D), ambience and adaptive music layers.</summary>
    public class AudioManager : MonoBehaviour
    {
        Dictionary<string, AudioClip> _clips;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _ambA, _ambB, _musCalm, _musTension, _musAlert, _musMenu, _heart;
        public MusicMood Mood = MusicMood.None;
        float _heartTimer;
        public float HeartRate; // beats per second, 0 = off
        public float HeartVolume;
        AudioListener _listener;

        // mission themes (Score): rendered on a worker thread, cached, swapped in under a short dip
        string _themeKey;
        readonly Dictionary<string, AudioClip[]> _themes = new Dictionary<string, AudioClip[]>();
        Task<Score.Layers> _render;
        // non-serialized: a play-mode script reload would otherwise bring a null array back as an empty one
        [System.NonSerialized] AudioClip[] _defaultMusic, _swapTo;
        float _musGain = 1f;
        public string ThemeKey => _themeKey;
        /// <summary>The theme now playing in the calm/tension/alert layers (null = the default drone).</summary>
        public string ThemePlaying { get; private set; }

        void Awake()
        {
            Game.Audio = this;
            float t0 = Time.realtimeSinceStartup;
            _clips = Synth.BuildAll();
            Debug.Log($"[Audio] synthesised {_clips.Count} clips in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
            for (int i = 0; i < 24; i++) _pool.Add(NewSource("sfx" + i));
            _ambA = Loop("amb_night"); _ambB = Loop("amb_drip");
            _musCalm = Loop("music_drone"); _musTension = Loop("music_tension"); _musAlert = Loop("music_alert"); _musMenu = Loop("music_menu");
            _defaultMusic = new[] { _musCalm.clip, _musTension.clip, _musAlert.clip };
            SwapMusic(_defaultMusic, null);
            _heart = NewSource("heart");
            for (int i = 0; i < FireVoices; i++)
            {
                var f = NewSource("fire" + i);
                f.clip = Get("amb_fire");
                f.loop = true;
                f.volume = 0f;
                f.spatialBlend = 1f;
                f.minDistance = 1.5f; f.maxDistance = 14f;
                f.time = i * 1.37f;
                _fire[i] = f;
            }
        }

        // ------------------------------------------------------------------ positional fires
        // the nearest lit braziers and bonfires to the camera's focus each hold one looping crackle voice
        const int FireVoices = 4;
        public const float FireRange = 16f;
        readonly AudioSource[] _fire = new AudioSource[FireVoices];
        readonly Stealth.GameLight[] _fireOf = new Stealth.GameLight[FireVoices];
        readonly List<Stealth.GameLight> _fireNear = new List<Stealth.GameLight>();
        float _fireScan;

        void TickFires(float dt)
        {
            bool live = Game.InMission && Game.Lights != null && Game.Cam && !Game.AnyPause;
            if (live && (_fireScan -= dt) <= 0f)
            {
                _fireScan = 0.5f;
                var c = Game.Cam.Pivot;
                _fireNear.Clear();
                foreach (var l in Game.Lights.All)
                    if (l && l.On && (l.Kind == Stealth.LightKind.Brazier || l.Kind == Stealth.LightKind.Fire) && Util.FlatDistance(l.transform.position, c) < FireRange)
                        _fireNear.Add(l);
                _fireNear.Sort((a, b) => Util.FlatDistance(a.transform.position, c).CompareTo(Util.FlatDistance(b.transform.position, c)));
                for (int i = 0; i < FireVoices; i++)
                {
                    var want = i < _fireNear.Count ? _fireNear[i] : null;
                    // a voice keeps its fire while that fire is still among the nearest, so it never jumps mid-crackle
                    if (_fireOf[i] && _fireNear.Contains(_fireOf[i])) continue;
                    if (want && System.Array.IndexOf(_fireOf, want) >= 0) want = null;
                    _fireOf[i] = want;
                    if (want) { _fire[i].volume = 0f; _fire[i].transform.position = want.transform.position + Vector3.up; }
                }
                for (int i = 0; i < FireVoices; i++)
                    if (!_fireOf[i])
                        foreach (var l in _fireNear)
                            if (System.Array.IndexOf(_fireOf, l) < 0) { _fireOf[i] = l; _fire[i].volume = 0f; _fire[i].transform.position = l.transform.position + Vector3.up; break; }
            }
            for (int i = 0; i < FireVoices; i++)
            {
                var f = _fire[i];
                bool on = live && _fireOf[i] && _fireOf[i].On;
                float target = on ? Amb * 0.55f * (_fireOf[i].Kind == Stealth.LightKind.Fire ? 1.3f : 1f) : 0f;
                if (on && !f.isPlaying) f.Play();
                f.volume = Mathf.MoveTowards(f.volume, target, dt * 0.8f);
                if (!on && f.volume <= 0.001f && f.isPlaying) { f.Pause(); _fireOf[i] = null; }
            }
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 3f; s.maxDistance = 40f;
            return s;
        }

        AudioSource Loop(string clip)
        {
            var s = NewSource("loop_" + clip);
            s.clip = Get(clip);
            s.loop = true;
            s.volume = 0;
            s.spatialBlend = 0;
            s.ignoreListenerPause = true;
            return s;
        }

        public AudioClip Get(string name)
        {
            if (_clips != null && _clips.TryGetValue(name, out var c)) return c;
            return null;
        }

        float Sfx => Game.Settings != null ? Game.Settings.MasterVolume * Game.Settings.SfxVolume : 0.7f;
        float Mus => Game.Settings != null ? Game.Settings.MasterVolume * Game.Settings.MusicVolume : 0.5f;
        float Amb => Game.Settings != null ? Game.Settings.MasterVolume * Game.Settings.AmbienceVolume : 0.5f;

        AudioSource Free()
        {
            foreach (var s in _pool) if (!s.isPlaying) return s;
            var n = NewSource("sfx" + _pool.Count);
            _pool.Add(n);
            return n;
        }

        public void Play2D(string name, float vol = 1f, float pitch = 1f)
        {
            var c = Get(name);
            if (!c) return;
            var s = Free();
            s.transform.localPosition = Vector3.zero;
            s.spatialBlend = 0f;
            s.pitch = pitch;
            s.ignoreListenerPause = true;
            s.PlayOneShot(c, vol * Sfx);
        }

        public void PlayAt(string name, Vector3 pos, float vol = 1f, float pitch = 1f, float maxDist = 40f)
        {
            var c = Get(name);
            if (!c) return;
            var s = Free();
            s.transform.position = pos;
            s.spatialBlend = 0.85f;
            s.maxDistance = maxDist;
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.ignoreListenerPause = false;
            s.PlayOneShot(c, vol * Sfx);
        }

        public void PlayVariant(string prefix, int count, Vector3 pos, float vol, float maxDist = 20f)
        {
            PlayAt(prefix + Random.Range(0, count), pos, vol, 1f, maxDist);
        }

        public void SetAmbience(string a, string b)
        {
            _ambA.clip = Get(a) ?? _ambA.clip;
            _ambB.clip = string.IsNullOrEmpty(b) ? null : Get(b);
            if (_ambA.clip && !_ambA.isPlaying) _ambA.Play();
            if (_ambB.clip) _ambB.Play(); else _ambB.Stop();
        }

        public void StopAmbience() { _ambA.Stop(); _ambB.Stop(); }

        /// <summary>Selects the mission's score by its `music =` key. Unknown or empty keys fall back to the default
        /// drone. The first use of a theme renders it in the background, and the old music plays until it is ready.</summary>
        public void SetTheme(string key)
        {
            if (!Score.Has(key)) key = null;
            if (key == _themeKey) return;
            _themeKey = key;
            if (key == null) { _swapTo = _defaultMusic; return; }
            if (_themes.TryGetValue(key, out var c)) { _swapTo = c; return; }
            _render = Task.Run(() => Score.Render(key));
        }

        void PollTheme()
        {
            if (_render == null || !_render.IsCompleted) return;
            var t = _render;
            _render = null;
            if (t.IsFaulted) { Debug.LogException(t.Exception); return; }
            var l = t.Result;
            if (l == null) return;
            var clips = new[] { Clip("theme_" + l.Key, l.Calm), Clip("theme_" + l.Key + "_t", l.Tension), Clip("theme_" + l.Key + "_a", l.Alert) };
            _themes[l.Key] = clips;
            if (l.Key == _themeKey) _swapTo = clips;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Score.Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>The three layers start on the same DSP tick and never stop (a silent layer just sits at zero
        /// volume), so tension and alert always land on the calm layer's beat.</summary>
        void SwapMusic(AudioClip[] c, string key)
        {
            if (c == null || c.Length < 3) return;
            _musCalm.clip = c[0]; _musTension.clip = c[1]; _musAlert.clip = c[2];
            double at = AudioSettings.dspTime + 0.06;
            _musCalm.PlayScheduled(at); _musTension.PlayScheduled(at); _musAlert.PlayScheduled(at);
            ThemePlaying = key;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            PollTheme();
            if (_swapTo != null)
            {
                _musGain = Mathf.MoveTowards(_musGain, 0f, dt * 3f);
                if (_musGain <= 0f) { SwapMusic(_swapTo, _swapTo == _defaultMusic ? null : _themeKey); _swapTo = null; }
            }
            else _musGain = Mathf.MoveTowards(_musGain, 1f, dt * 1.5f);
            Fade(_musMenu, Mood == MusicMood.Menu ? 0.55f * Mus : 0, dt);
            FadeLayer(_musCalm, Mood == MusicMood.Calm || Mood == MusicMood.Tension ? 0.35f * Mus * _musGain : 0, dt);
            FadeLayer(_musTension, Mood == MusicMood.Tension ? 0.45f * Mus * _musGain : 0, dt);
            FadeLayer(_musAlert, Mood == MusicMood.Alert ? 0.5f * Mus * _musGain : 0, dt * 2f);
            TickFires(dt);
            float ambTarget = Game.InMission ? Amb * 0.6f : 0f;
            Fade(_ambA, ambTarget, dt * 0.5f);
            Fade(_ambB, ambTarget * 0.8f, dt * 0.5f);

            // keep listener at camera pivot height for sensible 3D attenuation
            if (!_listener)
            {
                _listener = FindAnyObjectByType<AudioListener>();
            }
            if (_listener && Game.Cam)
            {
                _listener.transform.position = Game.Cam.Pivot + Vector3.up * 8f;
                _listener.transform.rotation = Quaternion.Euler(0, Game.Cam.Yaw, 0);
            }

            if (HeartRate > 0 && HeartVolume > 0.01f && !Game.AnyPause)
            {
                _heartTimer -= Time.deltaTime;
                if (_heartTimer <= 0)
                {
                    _heartTimer = 1f / HeartRate;
                    _heart.spatialBlend = 0;
                    _heart.PlayOneShot(Get("heartbeat"), HeartVolume * Sfx);
                }
            }
        }

        static void FadeLayer(AudioSource s, float target, float dt)
        {
            s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.5f);
        }

        static void Fade(AudioSource s, float target, float dt)
        {
            if (!s.clip) return;
            if (target > 0.001f && !s.isPlaying) s.Play();
            s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.5f);
            if (s.volume <= 0.0005f && target <= 0.001f && s.isPlaying) s.Pause();
        }
    }
}
