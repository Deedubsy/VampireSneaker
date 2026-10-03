using System.Collections.Generic;
using UnityEngine;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// Vision cones (SR.5). Which cones show:
    /// <list type="bullet">
    /// <item>In normal play, the ones that matter now (QW15, SR.6; <see cref="ConeContext"/>): guards aware of her, and
    /// guards whose seeing region is within 4 m of her or of where she will be in 1.5 s. At most 4, held 1.5 s, faded
    /// in over 0.25 s. Guards past the budget, or close but not yet qualifying, show their near sector alone.</item>
    /// <item>Middle-click a human to pin (toggle) his cone, up to 3 (the oldest drops); hovering a human shows his.</item>
    /// <item>Hold Alt to show every cone in view (on Apex, only cones within 20 m of Ilse).</item>
    /// <item>Merciful counts 6 m as close; on Apex, unaware guards show their near sector alone unless pinned or hovered.</item>
    /// </list>
    /// What is drawn (QW7, QW19): the near sector is a solid fill, stronger toward the guard, ending in a bright solid
    /// arc, the line not to cross. The far band is sampled for light every 0.5 m along each ray: solid where she would
    /// be lit, faint where the dark hides her. A thin arc marks its end. Walls clip the cone. A 1.4 m touch circle shows
    /// around any hostile, seeing guard within 4 m of Ilse. Rebuilt at 20 Hz per visible cone.
    /// The grace fringe (D138), where she fills at half rate, is drawn at half strength and dashed (alternate rays): the
    /// outer 10° on either side, the outer metre inside the near edge over dark ground, and the outer metre of the far
    /// band. The near edge itself is always solid. Lit ground comes from the Exposure Field (D137).
    /// State styles (SR.5): Searching (lost her) is red at 60% with a dashed end; a blinded guard is grey and dashed; a
    /// mesmerised or dazed guard (shown only by Alt or inspection) is a violet outline with no fill.
    /// A short arc at the guard's feet says whose cone it is. Where cones overlap, the guard whose meter is rising on her
    /// owns it: his cone draws on top at full strength and the others at half (<see cref="ConeContext.Owner"/>). Within
    /// 8 m of her, the dark far band is hatched (the overlay shader's _HATCH lines) and its sides outlined, so the region
    /// still reads where it hides her.
    /// </summary>
    public class ConeRenderer : MonoBehaviour
    {
        public static ConeRenderer Instance;
        /// <summary>Alt is held: every cone in range shows (the tactical tier, SR.12).</summary>
        public bool AllShown { get; private set; }
        const int Rays = 36, FarSamples = 20;
        // verts per ray: origin arc (inner, outer), fill start, grace start twice (the fringe begins with a step, not a
        // ramp), near-inner, edge-inner, edge, far samples, end-inner, end
        const int O1 = 1, A0 = 2, G0 = 3, G1 = 4, NI = 5, EI = 6, E = 7, F = 8, K = F + FarSamples + 2;
        const float Rebuild = 0.05f, EdgeW = 0.12f, EndW = 0.06f, TouchShowWithin = 4f, LightCell = 0.5f, LightCacheLife = 0.25f;
        const float OriginIn = 0.25f, OriginOut = 0.5f, HatchWithin = 8f;
        // the deck: the part of a cone on a raised surface past the wall that stops its ray (a roof seen from the street)
        const int DeckSamples = 30;
        const float DeckStep = 0.5f, DeckLift = 0.07f, DeckRaise = 1f;
        const int CircleSegs = 40;

        /// <summary>Pinned guards, oldest first (SR.12).</summary>
        public readonly List<Npc> Inspected = new List<Npc>();

        class Cone
        {
            public Npc Npc;
            public Mesh Mesh;
            public MeshRenderer Mr;
            public float NextBuild, Fade;
            public ConeContext.Show Mode, BuiltMode;
            public bool Want, Instant, Yield, BuiltYield;
            public readonly Vector3[] V = new Vector3[(Rays + 1) * K];
            public readonly Color[] C = new Color[(Rays + 1) * K];
            /// <summary>uv2.x: how much of a vertex's fill is hatch lines (the dark far band near her) rather than a fill.</summary>
            public readonly Vector2[] H = new Vector2[(Rays + 1) * K];
            public Mesh Deck;
            public MeshRenderer DeckMr;
            public readonly Vector3[] DV = new Vector3[(Rays + 1) * DeckSamples];
            public readonly Color[] DC = new Color[(Rays + 1) * DeckSamples];
            public readonly bool[] DOn = new bool[(Rays + 1) * DeckSamples];
            public readonly List<int> DTris = new List<int>();
            /// <summary>Which far samples drew as the lit far fill, and which rays drew a far band (read back by the truth sweep).</summary>
            public readonly bool[] Lit = new bool[(Rays + 1) * FarSamples];
            public readonly bool[] FarOn = new bool[Rays + 1];
        }

        class Circle
        {
            public Mesh Mesh;
            public MeshRenderer Mr;
            public readonly Color[] C = new Color[(CircleSegs + 1) * 3];
        }

        readonly Dictionary<Npc, Cone> _active = new Dictionary<Npc, Cone>();
        readonly List<Cone> _free = new List<Cone>();
        readonly List<Npc> _drop = new List<Npc>();
        readonly Dictionary<Npc, ConeContext.Show> _want = new Dictionary<Npc, ConeContext.Show>();
        readonly Dictionary<Npc, float> _heldUntil = new Dictionary<Npc, float>();
        readonly Dictionary<Npc, float> _contact = new Dictionary<Npc, float>();
        readonly Dictionary<Npc, (Vector3 Fwd, float Rate, float At)> _turn = new Dictionary<Npc, (Vector3, float, float)>();
        readonly List<Npc> _candNpc = new List<Npc>();
        readonly List<ConeContext.Candidate> _cand = new List<ConeContext.Candidate>();
        ConeContext.Show[] _pick = new ConeContext.Show[16];
        bool[] _rising = new bool[8];
        float[] _meter = new float[8];
        readonly List<Cone> _shown = new List<Cone>();
        readonly List<Circle> _circles = new List<Circle>();
        Material _mat;
        int[] _tris;
        Vector3 _lastFeet, _vel;
        bool _haveFeet, _altLatched;

        // light at ground cells, shared by every cone, refreshed 4 times a second (lanterns move, lamps go out)
        static readonly Dictionary<long, float> LightCache = new Dictionary<long, float>();
        static float _lightCacheAt;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool IsInspected(Npc n) => n && Inspected.Contains(n);

        /// <summary>The Spotted picture (SR.10): until <see cref="SpotUntil"/> (unscaled) this guard's cone draws at full
        /// strength, at once and on top, whatever the contextual rules say; the others yield.</summary>
        public Npc Spotter;
        public float SpotUntil;
        bool Spotting => Spotter && Spotter.IsAlive && !Spotter.Incapacitated && Time.unscaledTime < SpotUntil + BoundaryGlint.Hold;

        /// <summary>Is this guard's cone (full, or near sector alone) on screen right now? For tests and the debrief.</summary>
        public ConeContext.Show Showing(Npc n) => n && _active.TryGetValue(n, out var c) && c.Want ? c.Mode : ConeContext.Show.None;

        public void Toggle(Npc n)
        {
            if (!n || !n.IsAlive) return;
            if (ConeContext.TogglePin(Inspected, n)) Game.Audio?.Play2D("ui_click", 0.35f, 1.3f);
        }

        public void ClearAll()
        {
            Inspected.Clear();
            foreach (var c in _active.Values) Release(c);
            _active.Clear();
            _heldUntil.Clear();
        }

        void Update()
        {
            var inp = Game.Input;
            if (!Game.InMission || Game.AI == null || inp == null)
            {
                if (_active.Count > 0) ClearAll();
                HideCircles(0);
                LightCache.Clear();   // the next mission's cells are another map's
                if (!Game.InMission) { _altLatched = false; Inspected.Clear(); _heldUntil.Clear(); _contact.Clear(); _turn.Clear(); _haveFeet = false; }
                return;
            }

            bool uiFree = Game.UI == null || !Game.UI.BlocksGameplay;
            if (uiFree && inp.Inspect.WasPressedThisFrame() && Game.Player != null)
            {
                var h = Game.Player.HoverNpc;
                if (h && h.IsAlive && !h.IsThrall) Toggle(h);
                else if (!h) Inspected.Clear();
            }
            Inspected.RemoveAll(n => !n || !n.IsAlive || n.Incapacitated);

            if (Time.unscaledTime - _lightCacheAt > LightCacheLife) { LightCache.Clear(); _lightCacheAt = Time.unscaledTime; }
            TrackPlayer();

            _want.Clear();
            foreach (var n in Inspected) _want[n] = ConeContext.Show.Full;
            var hover = uiFree && Game.Player != null ? Game.Player.HoverNpc : null;
            if (hover && (Seeing(hover) || Entranced(hover))) _want[hover] = ConeContext.Show.Full;
            bool spot = Spotting;
            if (spot) _want[Spotter] = ConeContext.Show.Full; else Spotter = null;

            // the cone key (Alt): held, or toggled (SR.12 accessibility); "always show all" draws every cone without the
            // rest of the tactical view
            var st = Game.Settings;
            bool hotkey = st == null || st.ShowAllConesHotkey, toggles = st != null && st.ConesKeyToggles;
            bool alt = ConeKey(ref _altLatched, hotkey, toggles, uiFree, inp.ShowCones.IsPressed(), inp.ShowCones.WasPressedThisFrame());
            bool all = alt || uiFree && st != null && st.AlwaysAllCones;
            AllShown = alt;
            if (all)
            {
                bool limited = !Difficulties.Current.AllCones;
                var pp = Game.Player ? Game.Player.Feet : Vector3.zero;
                foreach (var n in Game.AI.Npcs)
                {
                    if (!(Seeing(n) || Entranced(n)) || _want.ContainsKey(n)) continue;
                    if (limited && Util.FlatDistance(n.transform.position, pp) > 20f) continue;
                    if (!OnScreen(n.transform.position)) continue;
                    _want[n] = ConeContext.Show.Full;
                }
            }
            bool instant = alt;
            if (Game.Settings == null || Game.Settings.ContextualCones) Contextual();

            // match cones to npcs
            foreach (var kv in _want)
            {
                if (!_active.TryGetValue(kv.Key, out var cone))
                {
                    cone = _free.Count > 0 ? Pop() : NewCone();
                    cone.Npc = kv.Key; cone.Fade = 0f; cone.NextBuild = 0f;
                    _active[kv.Key] = cone;
                }
                cone.Want = true;
                cone.Mode = kv.Value;
                cone.Instant = instant || kv.Key == hover || Inspected.Contains(kv.Key) || spot && kv.Key == Spotter;
            }
            Ownership();
            _drop.Clear();
            float dt = Time.unscaledDeltaTime;
            foreach (var kv in _active)
            {
                var cone = kv.Value;
                if (!_want.ContainsKey(kv.Key)) cone.Want = false;
                if (!kv.Key || !kv.Key.IsAlive) { _drop.Add(kv.Key); continue; }
                float target = cone.Want ? 1f : 0f;
                float before = cone.Fade;
                cone.Fade = cone.Instant && cone.Want ? 1f : Mathf.MoveTowards(cone.Fade, target, dt / ConeContext.FadeTime);
                if (cone.Fade <= 0f && !cone.Want) { _drop.Add(kv.Key); continue; }
                cone.Mr.enabled = true;
                cone.DeckMr.enabled = cone.DTris.Count > 0;
                bool fading = !Mathf.Approximately(before, cone.Fade);
                bool moving = !Game.AnyPause && cone.Npc.Agent && cone.Npc.Agent.velocity.sqrMagnitude > 0.01f;
                if (fading || cone.Mode != cone.BuiltMode || cone.Yield != cone.BuiltYield || Time.unscaledTime >= cone.NextBuild
                    || moving && Time.unscaledTime >= cone.NextBuild - Rebuild * 0.5f)
                {
                    cone.NextBuild = Time.unscaledTime + Rebuild;
                    Build(cone);
                }
            }
            foreach (var n in _drop) { Release(_active[n]); _active.Remove(n); }

            TouchCircles();
        }

        /// <summary>The rising guard's cone draws on top at full strength; the others yield (SR.5).</summary>
        void Ownership()
        {
            _shown.Clear();
            foreach (var c in _active.Values) if (c.Want && c.Npc) _shown.Add(c);
            if (_rising.Length < _shown.Count) { _rising = new bool[_shown.Count * 2]; _meter = new float[_shown.Count * 2]; }
            for (int i = 0; i < _shown.Count; i++)
            {
                var n = _shown[i].Npc;
                _rising[i] = n.SeesPlayer && Seeing(n);
                _meter[i] = n.Detection;
            }
            int owner = ConeContext.Owner(_rising, _meter, _shown.Count);
            if (Spotting) owner = _shown.FindIndex(c => c.Npc == Spotter);
            foreach (var c in _active.Values) { c.Yield = false; c.Mr.sortingOrder = c.DeckMr.sortingOrder = 0; }
            if (owner < 0) return;
            for (int i = 0; i < _shown.Count; i++)
            {
                _shown[i].Yield = i != owner;
                _shown[i].Mr.sortingOrder = _shown[i].DeckMr.sortingOrder = i == owner ? 1 : 0;
            }
        }

        /// <summary>A cone as built, read back for the truth sweep (dev, <see cref="Core.DevConeCheck"/>): per ray, where the
        /// near fill ends, where the cone ends, where the far samples sit and which drew lit.</summary>
        public sealed class Drawn
        {
            public Vector3 Origin, Fwd;
            public float Half;
            public readonly float[] Near = new float[Rays + 1], Reach = new float[Rays + 1], FarEnd = new float[Rays + 1];
            public readonly bool[] FarOn = new bool[Rays + 1];
            public readonly bool[] Lit = new bool[(Rays + 1) * FarSamples];
            public const int RayCount = Rays, Samples = FarSamples;
        }

        /// <summary>Builds this guard's full cone now, off screen, and reads back what it would draw. Null if he can't see.</summary>
        public Drawn DrawFor(Npc n)
        {
            if (!Seeing(n)) return null;
            // the light cache lags a carried lantern by up to its life (0.35 m at a walk): the sweep judges the drawing, fresh
            LightCache.Clear(); _lightCacheAt = Time.unscaledTime;
            var c = _free.Count > 0 ? Pop() : NewCone();
            c.Npc = n; c.Mode = ConeContext.Show.Full; c.Fade = 1f; c.Yield = false;
            Build(c);
            var d = new Drawn { Origin = n.transform.position, Fwd = n.Forward, Half = n.Vision.HalfAngle };
            d.Fwd.y = 0f; d.Fwd.Normalize();
            for (int i = 0; i <= Rays; i++)
            {
                int k = i * K;
                d.Near[i] = Util.FlatDistance(c.V[k + E], d.Origin);
                d.Reach[i] = Util.FlatDistance(c.V[k + K - 1], d.Origin);
                d.FarEnd[i] = Util.FlatDistance(c.V[k + K - 2], d.Origin);
                d.FarOn[i] = c.FarOn[i];
            }
            System.Array.Copy(c.Lit, d.Lit, c.Lit.Length);
            Release(c);
            return d;
        }

        Cone Pop() { var c = _free[_free.Count - 1]; _free.RemoveAt(_free.Count - 1); return c; }

        void Release(Cone c)
        {
            if (c.Mr) c.Mr.enabled = false;
            if (c.DeckMr) c.DeckMr.enabled = false;
            c.Npc = null; c.Want = false; c.Fade = 0f;
            _free.Add(c);
        }

        void TrackPlayer()
        {
            var p = Game.Player;
            if (!p) { _haveFeet = false; return; }
            float dt = Time.deltaTime;
            if (_haveFeet && dt > 0f)
            {
                var v = (p.Feet - _lastFeet) / dt; v.y = 0f;
                if (v.sqrMagnitude > 100f) v = Vector3.zero;   // a teleport or a leap landing
                _vel = Vector3.Lerp(_vel, v, Mathf.Clamp01(dt * 8f));
            }
            _lastFeet = p.Feet; _haveFeet = true;
        }

        /// <summary>A human who can see her: not asleep, down, enthralled, friendly or held by Dominion.</summary>
        public static bool Seeing(Npc n) =>
            n && n.IsAlive && n.CanSee && !n.Incapacitated && !n.IsThrall && !n.Friendly
            && n.State != NpcState.Mesmerised && n.State != NpcState.Dazed && n.gameObject.activeInHierarchy;

        /// <summary>Held by Dominion or dazed: he can't detect, but Alt or inspection still draws his cone, as an outline.</summary>
        static bool Entranced(Npc n) =>
            n && n.IsAlive && !n.IsThrall && (n.State == NpcState.Mesmerised || n.State == NpcState.Dazed) && n.gameObject.activeInHierarchy;

        static bool Aware(Npc n) =>
            n.Detection > 0.01f || n.State == NpcState.Suspicious || n.State == NpcState.Investigating
            || n.State == NpcState.Searching || n.State == NpcState.Alerted;

        /// <summary>The contextual cones (SR.6): measure every seeing guard near her and let <see cref="ConeContext"/> rank them.</summary>
        void Contextual()
        {
            var p = Game.Player;
            if (!p) return;
            var feet = p.Feet;
            var ahead = feet + _vel * ConeContext.LookAhead;
            bool lit = p.Light >= DetectionMath.ExposedAt;
            float now = Time.time;
            var diff = Difficulties.Current;
            float nearDanger = diff.ConeNear;
            _candNpc.Clear(); _cand.Clear(); _contact.Clear();
            foreach (var n in Game.AI.Npcs)
            {
                if (!Seeing(n)) continue;
                var pos = n.transform.position;
                float dist = Util.FlatDistance(pos, feet);
                var v = n.Vision;
                if (dist > Mathf.Max(ConeContext.AwareRange, v.FarRange + nearDanger + 2f)) continue;
                var fwd = n.Forward;
                // high above a guard who doesn't look up, only his near sector can see her
                bool farReaches = ConeContext.FarReaches(feet.y - pos.y, v.LooksUp);
                float near = Mathf.Min(DetectionMath.DistanceToSector(pos, fwd, v.HalfAngle, v.NearRange, feet),
                                       DetectionMath.DistanceToSector(pos, fwd, v.HalfAngle, v.NearRange, ahead));
                float far = Mathf.Min(DetectionMath.DistanceToSector(pos, fwd, v.HalfAngle, v.FarRange, feet),
                                      DetectionMath.DistanceToSector(pos, fwd, v.HalfAngle, v.FarRange, ahead));
                // lit, the whole cone sees her; in the dark the far band counts only when she is about to touch its outline
                float gap = farReaches ? Mathf.Min(near, lit ? far : far + 2f) : near;
                bool aware = Aware(n);
                // time to contact (SR.6): his detecting region, carried along by his walk and his current turn
                float yawRate = YawRate(n, fwd);
                var gv = n.Agent && n.Agent.enabled && !Game.AnyPause ? n.Agent.velocity : Vector3.zero;
                float contact = ConeContext.TimeToContact(pos, gv, fwd, yawRate, v.HalfAngle,
                    farReaches && lit ? v.FarRange : v.NearRange, feet, _vel);
                _contact[n] = contact;
                var c = new ConeContext.Candidate
                {
                    Gap = gap, Distance = dist, Aware = aware,
                    NearClose = dist <= v.NearRange + ConeContext.NearOnlyExtra,
                    Soon = contact <= ConeContext.ContactHorizon, ContactIn = contact,
                };
                if (ConeContext.Qualifies(c, nearDanger)) _heldUntil[n] = now + ConeContext.Hold;
                else if (_heldUntil.TryGetValue(n, out var until) && now < until) c.Held = true;
                _candNpc.Add(n); _cand.Add(c);
            }
            if (_pick.Length < _cand.Count) _pick = new ConeContext.Show[_cand.Count * 2];
            ConeContext.Pick(_cand, _pick, diff.ConesAwareOnly ? 0 : ConeContext.Budget, nearDanger);
            for (int i = 0; i < _candNpc.Count; i++)
            {
                if (_pick[i] == ConeContext.Show.None) continue;
                var n = _candNpc[i];
                if (_want.TryGetValue(n, out var have) && have == ConeContext.Show.Full) continue;
                _want[n] = _pick[i];
            }
        }

        /// <summary>Seconds until this guard's detecting region reaches her (SR.6), or infinity: measured for guards near
        /// her by the contextual pass. The HUD gives an off-screen guard a pip when it is within the horizon.</summary>
        public float ContactIn(Npc n) => n && _contact.TryGetValue(n, out var t) ? t : float.PositiveInfinity;

        /// <summary>His turn rate, degrees a second (positive clockwise from above), smoothed over a few frames.</summary>
        float YawRate(Npc n, Vector3 fwd)
        {
            float t = Time.time, rate = 0f;
            // a guard not measured last frame (out of range, or the game paused) starts again from still
            if (_turn.TryGetValue(n, out var was) && t > was.At && t - was.At < 0.2f)
            {
                float dt = t - was.At;
                float now = Vector3.SignedAngle(was.Fwd, fwd, Vector3.up) / dt;
                rate = Mathf.Lerp(was.Rate, Mathf.Clamp(now, -720f, 720f), Mathf.Clamp01(dt * 10f));
            }
            else if (was.At == t) rate = was.Rate;
            _turn[n] = (fwd, rate, t);
            return rate;
        }

        static bool OnScreen(Vector3 p)
        {
            if (Game.Cam == null || Game.Cam.Cam == null) return true;
            var v = Game.Cam.Cam.WorldToViewportPoint(p);
            return v.z > 0 && v.x > -0.2f && v.x < 1.2f && v.y > -0.2f && v.y < 1.2f;
        }

        Material Mat => _mat ? _mat : _mat = Mats.Overlay("cone", Color.white);
        /// <summary>The cone fill's own material: the overlay with the hatch on (uv2 weights it per vertex).</summary>
        Material HatchMat
        {
            get
            {
                if (_hatchMat) return _hatchMat;
                _hatchMat = new Material(Mat) { name = "cone_hatch" };
                _hatchMat.EnableKeyword("_HATCH");
                return _hatchMat;
            }
        }
        Material _hatchMat;

        Cone NewCone()
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(transform, false);
            go.layer = Layers.Overlay;
            var c = new Cone { Mesh = new Mesh { name = "cone" } };
            c.Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = c.Mesh;
            c.Mr = go.AddComponent<MeshRenderer>();
            c.Mr.sharedMaterial = HatchMat;
            c.Mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.Mr.receiveShadows = false;

            // topology: per pair of neighbouring rays, a strip between consecutive verts along the ray for the origin
            // arc, the near fill (fill start to grace start, then grace start to near-inner), the near edge, each far
            // step and the end arc
            if (_tris == null)
            {
                var tris = new List<int>();
                for (int i = 0; i < Rays; i++)
                {
                    int a = i * K, b = (i + 1) * K;
                    void Quad(int j)
                    {
                        tris.Add(a + j); tris.Add(a + j + 1); tris.Add(b + j + 1);
                        tris.Add(a + j); tris.Add(b + j + 1); tris.Add(b + j);
                    }
                    Quad(0); Quad(A0); Quad(G1); Quad(EI);
                    for (int j = F; j < F + FarSamples - 1; j++) Quad(j);
                    Quad(K - 2);
                }
                _tris = tris.ToArray();
            }
            c.Mesh.vertices = c.V;
            c.Mesh.colors = c.C;
            c.Mesh.triangles = _tris;

            var dgo = new GameObject("ConeDeck");
            dgo.transform.SetParent(go.transform, false);
            dgo.layer = Layers.Overlay;
            c.Deck = new Mesh { name = "cone deck" };
            c.Deck.MarkDynamic();
            dgo.AddComponent<MeshFilter>().sharedMesh = c.Deck;
            c.DeckMr = dgo.AddComponent<MeshRenderer>();
            c.DeckMr.sharedMaterial = Mat;
            c.DeckMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.DeckMr.receiveShadows = false;
            c.DeckMr.enabled = false;
            return c;
        }

        public static Color StateColor(Npc n)
        {
            switch (n.State)
            {
                case NpcState.Alerted: case NpcState.Panicked: case NpcState.Searching: return Mats.Pal.Alerted;
                case NpcState.Suspicious: case NpcState.Investigating: return Blind(n) ? Grey : Mats.Pal.Suspicious;
                case NpcState.Mesmerised: case NpcState.Dazed: return Mats.Pal.Dominion;
                default: return Blind(n) ? Grey : n.Wary ? Color.Lerp(Mats.Pal.Relaxed, Mats.Pal.Suspicious, 0.35f) : Mats.Pal.Relaxed;
            }
        }

        static readonly Color Grey = Util.Hex("#8a8d96");

        /// <summary>The cone key (SR.12): held, or (<paramref name="toggles"/>) each press latches the tactical view on or
        /// off. Off while a menu has the input; the latch survives the menu and drops when the key is disabled.</summary>
        public static bool ConeKey(ref bool latched, bool hotkey, bool toggles, bool uiFree, bool held, bool pressed)
        {
            if (!hotkey || !toggles) latched = false;
            else if (uiFree && pressed) latched = !latched;
            return hotkey && uiFree && (toggles ? latched : held);
        }

        /// <summary>An edge's width: doubled in high-contrast mode (SR.12).</summary>
        public static float EdgeWidth(float w, bool highContrast) => highContrast ? w * 2f : w;

        /// <summary>The Suspicious cone's slow pulse: strength 0.7..1 at 0.8 Hz.</summary>
        public static float Pulse(float t) => 0.85f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * 0.8f);

        /// <summary>Shape-coded states (SR.12 colour-blind mode): the origin arc is small and solid for an unaware guard;
        /// wide and broken for a suspicious or investigating one; wide and solid for one who knows (spotted, hunting,
        /// searching, panicked).</summary>
        public enum OriginShape { Plain, Dashed, Wide }
        const float OriginWideIn = 0.1f, OriginWideOut = 0.85f;

        public static OriginShape OriginShapeOf(NpcState s)
        {
            switch (s)
            {
                case NpcState.Suspicious: case NpcState.Investigating: return OriginShape.Dashed;
                case NpcState.Alerted: case NpcState.Searching: case NpcState.Panicked: return OriginShape.Wide;
                default: return OriginShape.Plain;
            }
        }
        static bool Blind(Npc n) => Game.AI != null && Game.AI.Blinded(n);

        /// <summary>Lost her: the Searching sweep, drawn at 60% and dashed (SR.5).</summary>
        static bool Lost(Npc n) => n.State == NpcState.Searching;

        /// <summary>Light at the 0.5 m ground cell holding <paramref name="p"/>, from the Exposure Field (D137), cached for
        /// a quarter second.</summary>
        static float CellLight(LightSystem lights, Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x / LightCell), iz = Mathf.FloorToInt(p.z / LightCell), iy = Mathf.FloorToInt(p.y);
            long key = ((long)(ix & 0xFFFFF) << 40) | ((long)(iz & 0xFFFFF) << 20) | (long)(iy & 0xFFFFF);
            if (LightCache.TryGetValue(key, out var l)) return l;
            var at = new Vector3((ix + 0.5f) * LightCell, p.y, (iz + 0.5f) * LightCell);
            var field = lights.Field;
            l = field != null ? field.LightAt(at) : lights.LightAt(at);
            LightCache[key] = l;
            return l;
        }

        /// <summary>The light guards would judge her by standing here: the cell's light, dimmed and hidden among leaves as
        /// hers is (<see cref="Player.Vampire.JudgedLight"/>), so a lamp-lit hedge draws dark.</summary>
        static float GroundLight(LightSystem lights, Vector3 p)
        {
            float l = CellLight(lights, p);
            bool leaves = Game.Level != null && Game.Level.InFoliage(p);
            return leaves ? Player.Vampire.JudgedLight(l * 0.6f, true) : l;
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        void Build(Cone c)
        {
            var n = c.Npc;
            var v = n.Vision;
            c.BuiltMode = c.Mode;
            c.BuiltYield = c.Yield;
            bool hc = Game.Settings != null && Game.Settings.HighContrastCones;
            bool full = c.Mode == ConeContext.Show.Full;
            var baseCol = StateColor(n);
            // detection fill warms the colour toward suspicion/alert
            if (n.State == NpcState.Relaxed && n.Detection > 0.01f) baseCol = Color.Lerp(baseCol, Mats.Pal.Suspicious, Mathf.Clamp01(n.Detection / DetectionMath.SuspiciousAt));
            float f = c.Fade * (Lost(n) ? 0.6f : 1f) * (c.Yield ? ConeContext.Yield : 1f);
            // Suspicious: a slow pulse (SR.5)
            if (n.State == NpcState.Suspicious) f *= Pulse(Time.unscaledTime);
            var shape = Game.Settings != null && Game.Settings.ShapeCodedCones ? OriginShapeOf(n.State) : OriginShape.Plain;
            bool big = shape != OriginShape.Plain;
            float oIn = big ? OriginWideIn : OriginIn, oOut = big ? OriginWideOut : OriginOut;
            bool dashed = Lost(n) || Blind(n);
            bool outline = Entranced(n);
            // blended in linear space, so small alphas read strong: near ≈ 35–45% on screen, lit far ≈ 25%, dark far ≈ 10%
            float apexA = (hc ? 0.38f : 0.25f) * f, nearA = (hc ? 0.22f : 0.14f) * f, edgeA = (hc ? 1f : 0.85f) * f;
            float farLitA = (hc ? 0.1f : 0.065f) * f, farDarkA = (hc ? 0.02f : 0.01f) * f, endA = (hc ? 0.3f : 0.2f) * f;
            // the hatch lines' own alpha: lines cover about a third of the ground, so they average fainter than the lit fill
            float hatchA = (hc ? 0.1f : 0.05f) * f;
            if (!full) { farLitA = farDarkA = endA = hatchA = 0f; }
            if (outline) { apexA = nearA = farLitA = farDarkA = hatchA = 0f; }
            System.Array.Clear(c.H, 0, c.H.Length);
            float originA = edgeA * 0.7f;
            // high contrast doubles the edge widths (SR.12)
            float edgeW = EdgeWidth(EdgeW, hc), endW = EdgeWidth(EndW, hc);
            var her = Game.Player ? Game.Player.Feet : new Vector3(1e6f, 0f, 1e6f);
            float yGround = n.transform.position.y + 0.07f;
            var origin = n.transform.position;
            var eye = origin + Vector3.up * 1.1f;
            var fwd = n.Forward; fwd.y = 0; fwd.Normalize();
            float baseYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            float half = v.HalfAngle;
            var lights = Game.Lights;
            var o = new Vector3(origin.x, yGround, origin.z);
            float widen = Difficulties.Current.Grace;
            float graceAng = half - DetectionMath.GraceAngle * widen, graceDepth = DetectionMath.GraceDepth * widen;
            for (int i = 0; i <= Rays; i++)
            {
                float off = -half + (2f * half) * i / Rays;
                float yaw = baseYaw + off;
                // grace: half strength, and every other ray fainter still, so the fringe reads dashed
                float dash = (i & 1) == 0 ? DetectionMath.GraceRate : 0.2f;
                float side = Mathf.Abs(off) > graceAng + 0.01f ? dash : 1f;
                var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                float range = full ? v.FarRange : v.NearRange;
                float reach = range;
                bool blocked = Physics.Raycast(eye, dir, out var hit, range, Layers.VisionBlockMask, QueryTriggerInteraction.Ignore);
                if (blocked) reach = hit.distance + 0.15f;
                DeckRay(c, i, blocked ? hit.distance : range, range, origin, dir, baseCol, side, dash, graceDepth, nearA, farLitA);
                float near = Mathf.Min(reach, v.NearRange);
                bool edge = reach >= v.NearRange - 0.01f;     // a wall short of the near edge: no line to draw
                int k = i * K;
                float inner = Mathf.Max(oOut, near - (edge ? edgeW : 0f));
                // the outer metre inside the near edge is fringe where the ground is dark (lit, the far band carries on)
                float gs = Mathf.Clamp(v.NearRange - graceDepth, oOut, inner);
                bool nearFringe = edge && !(full && lights != null && GroundLight(lights, o + dir * v.NearRange) >= v.LitThreshold);
                c.V[k + 0] = o + dir * oIn;
                c.V[k + O1] = o + dir * oOut;
                c.V[k + A0] = c.V[k + O1];
                c.V[k + G0] = o + dir * gs;
                c.V[k + G1] = c.V[k + G0];
                c.V[k + NI] = o + dir * inner;
                c.V[k + EI] = c.V[k + NI];
                c.V[k + E] = o + dir * near;
                float gA = Mathf.Lerp(apexA, nearA, (gs - oOut) / Mathf.Max(0.01f, inner - oOut)) * side;
                // the origin arc: whose cone this is (dashed when shape-coded and suspicious)
                c.C[k + 0] = A(baseCol, shape == OriginShape.Dashed && (i / 6 & 1) != 0 ? originA * 0.1f : originA);
                c.C[k + O1] = c.C[k + 0];
                c.C[k + A0] = A(baseCol, apexA * side);
                c.C[k + G0] = A(baseCol, gA);
                c.C[k + G1] = A(baseCol, nearFringe ? gA * dash : gA);
                c.C[k + NI] = A(baseCol, nearA * side * (nearFringe ? dash : 1f));
                // the near edge: always solid
                c.C[k + EI] = A(baseCol, edge ? edgeA : 0f);
                c.C[k + E] = c.C[k + EI];

                // far band: a light sample every ~0.5 m from the near edge to the end arc
                float farEnd = Mathf.Max(near, reach - endW);
                bool far = full && reach > near + endW + 0.05f;
                bool rim = i == 0 || i == Rays;
                c.FarOn[i] = far;
                for (int s = 0; s < FarSamples; s++)
                {
                    float t = near + (farEnd - near) * s / (FarSamples - 1);
                    var p = o + dir * t;
                    c.V[k + F + s] = p;
                    float a = 0f;
                    if (far)
                    {
                        float l = lights != null ? GroundLight(lights, p) : 0f;
                        bool lit = l >= v.LitThreshold;
                        c.Lit[i * FarSamples + s] = lit;
                        a = lit ? farLitA : farDarkA;
                        // near her, dark ground is hatched (lines drawn by the shader in world space, weighted by uv2) and the
                        // sides outlined, so the band reads as "he looks this way, but you're in the dark"
                        bool close = Util.FlatDistance(p, her) <= HatchWithin;
                        if (!lit && !outline && close) { a = hatchA; c.H[k + F + s] = new Vector2(1f, 0f); }
                        a *= side;
                        // the sides: outlined near her, and always on an outline-only (entranced) cone
                        if (rim && (close || outline)) { a = Mathf.Max(a, endA * ((s & 1) == 0 ? 0.6f : 0.25f)); c.H[k + F + s] = default; }
                        if (t > v.FarRange - graceDepth) a *= dash;
                    }
                    if (!far) c.Lit[i * FarSamples + s] = false;
                    c.C[k + F + s] = A(baseCol, a);
                }
                c.V[k + K - 2] = o + dir * farEnd;
                c.V[k + K - 1] = o + dir * reach;
                // the end is a soft rule (grace); dashed outright when he has lost her or is blinded
                var ec = A(baseCol, far ? endA * (dashed && (i & 1) == 1 ? 0.15f : 1f) : 0f);
                c.C[k + K - 2] = ec; c.C[k + K - 1] = ec;
            }
            c.Mesh.vertices = c.V;
            c.Mesh.colors = c.C;
            c.Mesh.uv2 = c.H;
            c.Mesh.RecalculateBounds();
            BuildDeck(c);
        }

        /// <summary>
        /// One ray of the deck. Past the wall that stopped the ray, every 0.5 m on a walkable surface at least 1 m above his
        /// feet, it asks the rule that judges her: is the point in his band (<see cref="DetectionMath.Classify"/>: the
        /// near sector at any height, the far band only where she would be lit and, above him, only if he looks up), and
        /// does his eye reach her body there (1.0 or 1.6 m up)? Where both hold, the deck draws: the near fill, or the lit
        /// far fill. Where the answer changes between two samples, the boundary is found by bisection and the unseen vertex
        /// moved onto it, so the fill stops where sight stops instead of fading over the next half metre. The roof edge
        /// hides more of a roof the farther back it is, so the first sample hidden from his eye ends the ray.
        /// </summary>
        void DeckRay(Cone c, int i, float wallAt, float range, Vector3 origin, Vector3 dir, Color col, float side, float dash,
                     float graceDepth, float nearA, float farLitA)
        {
            var lvl = Game.Level;
            int dk = i * DeckSamples;
            bool on = wallAt < range - 0.01f && lvl != null && lvl.Grid != null && (nearA > 0f || farLitA > 0f);
            bool seenAny = false, prevOn = false;
            float prevT = 0f, prevA = 0f;
            for (int s = 0; s < DeckSamples; s++)
            {
                float t = wallAt + 0.05f + DeckStep * s;
                c.DOn[dk + s] = false;
                c.DC[dk + s] = default;
                c.DV[dk + s] = origin + dir * t;
                if (!on || t > range) { on = false; continue; }
                float a = DeckLook(c.Npc, origin, dir, t, side, dash, graceDepth, nearA, farLitA, out bool valid, out bool hidden);
                if (!valid) { if (seenAny) on = false; prevOn = false; continue; }
                if (prevOn && (a > 0f) != (prevA > 0f))
                {
                    // bisect for where the answer changes, and put the unseen vertex there
                    float lo = prevT, hi = t;
                    for (int b = 0; b < 4; b++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        float am = DeckLook(c.Npc, origin, dir, mid, side, dash, graceDepth, nearA, farLitA, out bool vm, out _);
                        if (vm && (am > 0f) == (prevA > 0f)) lo = mid; else hi = mid;
                    }
                    if (a > 0f) Place(c, dk + s - 1, origin, dir, hi, default);   // unseen to seen: move the previous one up
                    else { t = lo; a = 0f; }                                       // seen to unseen: this one stops at the edge
                }
                Place(c, dk + s, origin, dir, t, A(col, a));
                c.DOn[dk + s] = true;
                prevOn = true; prevT = t; prevA = a;
                if (a > 0f) seenAny = true;
                else if (seenAny && hidden) on = false;   // the edge hides everything farther back
            }
        }

        static void Place(Cone c, int k, Vector3 origin, Vector3 dir, float t, Color col)
        {
            var q = origin + dir * t;
            var lvl = Game.Level;
            var cell = lvl.CellOf(q);
            q.y = lvl.Grid.Top(cell.x, cell.y) + DeckLift;
            c.DV[k] = q;
            c.DC[k] = col;
        }

        /// <summary>The deck's answer at <paramref name="t"/> along a ray: the fill alpha where she would be seen there, else
        /// 0. <paramref name="valid"/> is false off a raised walkable surface; <paramref name="hidden"/> is true when the
        /// point is in his band but his eye can't reach her body.</summary>
        float DeckLook(Npc n, Vector3 origin, Vector3 dir, float t, float side, float dash, float graceDepth, float nearA,
                       float farLitA, out bool valid, out bool hidden)
        {
            valid = hidden = false;
            var lvl = Game.Level;
            var q = origin + dir * t;
            var cell = lvl.CellOf(q);
            if (!lvl.Grid.WalkTop(cell.x, cell.y)) return 0f;
            float top = lvl.Grid.Top(cell.x, cell.y);
            if (top - origin.y < DeckRaise) return 0f;
            valid = true;
            q.y = top;
            var v = n.Vision;
            float light = Game.Lights != null ? CellLight(Game.Lights, q) : 0f;
            var band = DetectionMath.Classify(v, origin, n.Forward, q, light);
            if (band == DetectionMath.Band.None) return 0f;
            if (!n.LineOfSight(q + Vector3.up * 1f) && !n.LineOfSight(q + Vector3.up * 1.6f)) { hidden = true; return 0f; }
            float a = (band == DetectionMath.Band.Far ? farLitA * 1.6f : nearA) * side;
            bool fringe = band == DetectionMath.Band.Far ? t > v.FarRange - graceDepth
                        : t > v.NearRange - graceDepth && light < v.LitThreshold;
            return fringe ? a * dash : a;
        }

        /// <summary>Quads between neighbouring deck samples that all lie on a raised surface.</summary>
        static void BuildDeck(Cone c)
        {
            c.DTris.Clear();
            for (int i = 0; i < Rays; i++)
                for (int s = 0; s < DeckSamples - 1; s++)
                {
                    int a = i * DeckSamples + s, b = a + DeckSamples;
                    if (!(c.DOn[a] && c.DOn[a + 1] && c.DOn[b] && c.DOn[b + 1])) continue;
                    if (c.DC[a].a + c.DC[a + 1].a + c.DC[b].a + c.DC[b + 1].a <= 0f) continue;
                    c.DTris.Add(a); c.DTris.Add(a + 1); c.DTris.Add(b + 1);
                    c.DTris.Add(a); c.DTris.Add(b + 1); c.DTris.Add(b);
                }
            c.Deck.Clear();
            if (c.DTris.Count == 0) { c.DeckMr.enabled = false; return; }
            c.Deck.vertices = c.DV;
            c.Deck.colors = c.DC;
            c.Deck.SetTriangles(c.DTris, 0);
            c.Deck.RecalculateBounds();
            c.DeckMr.enabled = c.Mr.enabled;
        }

        // ---- touch circles: the 1.4 m peripheral zone that sees her from any side ----

        void TouchCircles()
        {
            var p = Game.Player;
            int used = 0;
            if (p)
            {
                var feet = p.Feet;
                bool hc = Game.Settings != null && Game.Settings.HighContrastCones;
                foreach (var n in Game.AI.Npcs)
                {
                    if (!Seeing(n)) continue;
                    var pos = n.transform.position;
                    float d = Util.FlatDistance(pos, feet);
                    if (d > TouchShowWithin || Mathf.Abs(pos.y - feet.y) > 2.5f) continue;
                    float a = Mathf.Clamp01((TouchShowWithin - d) / 1f);
                    var circle = used < _circles.Count ? _circles[used] : NewCircle();
                    used++;
                    circle.Mr.enabled = true;
                    var t = circle.Mr.transform;
                    t.position = new Vector3(pos.x, pos.y + 0.075f, pos.z);
                    float r = n.Vision.Peripheral;
                    t.localScale = new Vector3(r, 1f, r);
                    Tint(circle, StateColor(n), a, hc);
                }
            }
            HideCircles(used);
        }

        void HideCircles(int from)
        {
            for (int i = from; i < _circles.Count; i++) if (_circles[i].Mr.enabled) _circles[i].Mr.enabled = false;
        }

        /// <summary>A unit disc: centre fan plus a rim band, scaled to the guard's peripheral radius.</summary>
        Circle NewCircle()
        {
            var go = new GameObject("Touch");
            go.transform.SetParent(transform, false);
            go.layer = Layers.Overlay;
            var c = new Circle { Mesh = new Mesh { name = "touch" } };
            var verts = new Vector3[(CircleSegs + 1) * 3];
            var tris = new List<int>();
            for (int i = 0; i <= CircleSegs; i++)
            {
                float ang = i * Mathf.PI * 2f / CircleSegs;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts[i * 3 + 0] = Vector3.zero;
                verts[i * 3 + 1] = dir * 0.93f;
                verts[i * 3 + 2] = dir;
                if (i == CircleSegs) break;
                int a = i * 3, b = (i + 1) * 3;
                tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(a + 1); tris.Add(b + 2); tris.Add(a + 2);
            }
            c.Mesh.vertices = verts;
            c.Mesh.colors = c.C;
            c.Mesh.triangles = tris.ToArray();
            c.Mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = c.Mesh;
            c.Mr = go.AddComponent<MeshRenderer>();
            c.Mr.sharedMaterial = Mat;
            c.Mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.Mr.receiveShadows = false;
            _circles.Add(c);
            return c;
        }

        void Tint(Circle c, Color col, float a, bool hc)
        {
            float fill = (hc ? 0.16f : 0.09f) * a, rim = (hc ? 1f : 0.8f) * a;
            for (int i = 0; i <= CircleSegs; i++)
            {
                c.C[i * 3 + 0] = A(col, fill);
                c.C[i * 3 + 1] = A(col, rim);
                c.C[i * 3 + 2] = A(col, rim);
            }
            c.Mesh.colors = c.C;
        }
    }
}
