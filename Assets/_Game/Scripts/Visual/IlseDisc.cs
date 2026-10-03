using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Stealth;

namespace Vespertine.Visual
{
    /// <summary>
    /// Ilse's ground disc (SR.7) and its toe (SR.8): the stealth HUD under her feet, where a WASD player is looking.
    /// <list type="bullet">
    /// <item>A hollow bone ring while she is hidden; filled warm once the light the far band judges her by reaches 0.35
    /// (<see cref="StepRead.Exposed"/>). Violet while the masque holds, grey in mist, shade blue inside darkness she
    /// made: those override light. A second pale-gold ring while a light burns her.</item>
    /// <item>Watcher ticks on the rim, one per guard who has her in a band with line of sight (or whose meter on her is
    /// still above 0), pointing at him in his colour, as long as his meter. Dashed while she is in his grace fringe;
    /// faint while the meter decays.</item>
    /// <item>The rim takes a guard's colour, pulsing, within 1 m of his near sector.</item>
    /// <item>The toe: a wedge at the front of the disc for the step the move keys ask for (0.6 s ahead), from the same
    /// <see cref="Npc.SeenBand"/> that judges her: warm (into light no cone covers), a guard's colour (he would see her
    /// there), flashing (his near sector or touch range). On a pad it rumbles once when it turns to a guard.</item>
    /// </list>
    /// </summary>
    public class IlseDisc : MonoBehaviour
    {
        const float R = 0.55f, RimW = 0.05f, Lift = 0.06f, TickW = 0.08f, TickGap = 0.03f, WatchRange = 40f, ToeEvery = 0.05f;
        const int Segs = 40;

        static readonly Color Hidden = new Color(0.85f, 0.84f, 0.8f, 0.42f);
        static readonly Color Mist = Util.Hex("#9aa0aa");
        // overlay vertex colours go to the screen unconverted, so Ember is linearised to read orange, not the cream of a
        // relaxed guard's bone
        static readonly Color Warm = Mats.Pal.Ember.linear;

        Mesh _mesh;
        MeshRenderer _mr;
        readonly List<Vector3> _v = new List<Vector3>(512);
        readonly List<Color> _c = new List<Color>(512);
        readonly List<int> _t = new List<int>(1024);
        // ticks and toe draw over her body: from behind and above, she covers about a metre of ground ahead of her
        readonly List<int> _top = new List<int>(512);
        float _toeAt, _rumbleUntil;
        StepRead.Toe _toe;
        Npc _toeBy;
        Vector3 _toeDir;

        /// <summary>The toe's current answer and the guard it is about (null for the light): for tests and the gym.</summary>
        public StepRead.Toe Toe => _toe;
        public Npc ToeBy => _toeBy;
        public static IlseDisc Instance;

        void Awake()
        {
            Instance = this;
            gameObject.layer = Layers.Overlay;
            _mesh = new Mesh { name = "ilse_disc" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.sharedMaterials = new[] { Mats.Overlay("ilse_disc", Color.white), Mats.Overlay("ilse_disc_top", Color.white, xray: true) };
            _mesh.subMeshCount = 2;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingOrder = 3;
            _mr.enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopRumble();
        }

        void LateUpdate()
        {
            var p = Game.Player;
            bool on = Game.InMission && p != null && !p.Dead && !p.Concealed && p.gameObject.activeInHierarchy
                      && (Game.Settings == null || Game.Settings.IlseDisc);
            if (!on)
            {
                if (_mr.enabled) _mr.enabled = false;
                _toe = StepRead.Toe.None; _toeBy = null;
                StopRumble();
                return;
            }
            var feet = p.Feet;
            transform.SetPositionAndRotation(feet + Vector3.up * Lift, Quaternion.identity);
            if (Time.unscaledTime >= _toeAt && !Game.AnyPause) { _toeAt = Time.unscaledTime + ToeEvery; JudgeToe(p); }
            if (Time.unscaledTime >= _rumbleUntil) StopRumble();
            Build(p);
            _mr.enabled = true;
        }

        // ------------------------------------------------------------------ the toe
        void JudgeToe(Player.Vampire p)
        {
            var before = _toe;
            var beforeBy = _toeBy;
            _toe = StepRead.Toe.None; _toeBy = null;
            var dir = p.MoveIntent; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f || p.Feeding != null || Game.Lights == null) return;
            dir.Normalize();
            _toeDir = dir;
            var feet = p.Feet;
            var next = feet + dir * StepRead.Reach(p.IntentSpeed);
            // the step stops where she would: at a wall, a ledge or a shut door
            if (UnityEngine.AI.NavMesh.Raycast(feet, next, out var hit, UnityEngine.AI.NavMesh.AllAreas)) next = hit.position;
            next.y = feet.y;
            if (Util.FlatDistance(next, feet) < 0.2f) return;
            bool leaves = Game.Level != null && Game.Level.InFoliage(next);
            float lightNext = Player.Vampire.JudgedLight(Game.Lights.LightAt(next) * (leaves ? 0.6f : 1f), leaves);
            float lightNow = p.SightLight;
            var toe = StepRead.ForLight(lightNow, lightNext);
            Npc by = null;
            float byD = float.MaxValue;
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                {
                    if (!Watcher(n)) continue;
                    float d = Util.FlatDistance(n.transform.position, feet);
                    if (d > n.Arch.Far * 1.6f + 2f) continue;
                    if (n.Overlooks(p, next, Util.FlatDistance(n.transform.position, next))) continue;
                    var bNext = n.SeenBand(next, lightNext);
                    if (bNext == DetectionMath.Band.None) continue;
                    var g = StepRead.ForGuard(n.SeenBand(feet, lightNow), bNext);
                    // the worst answer wins; between equals, the nearer guard's colour
                    if (g > toe || g == toe && g >= StepRead.Toe.Seen && d < byD) { toe = g; by = n; byD = d; }
                }
            _toe = toe; _toeBy = toe >= StepRead.Toe.Seen ? by : null;
            if (_toe >= StepRead.Toe.Seen && (before < StepRead.Toe.Seen || beforeBy != _toeBy)) Rumble(_toe == StepRead.Toe.Flash ? 0.45f : 0.25f);
        }

        static bool Watcher(Npc n) =>
            n && n.IsAlive && !n.Incapacitated && !n.IsThrall && !n.Friendly && n.CanSee && n.gameObject.activeInHierarchy;

        void Rumble(float strength)
        {
            var pad = Gamepad.current;
            if (pad == null || pad.leftStick.ReadValue().sqrMagnitude < 0.04f) return;
            pad.SetMotorSpeeds(strength * 0.6f, strength);
            _rumbleUntil = Time.unscaledTime + 0.12f;
        }

        void StopRumble()
        {
            if (_rumbleUntil <= 0f) return;
            _rumbleUntil = 0f;
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        // ------------------------------------------------------------------ drawing
        void Build(Player.Vampire p)
        {
            _v.Clear(); _c.Clear(); _t.Clear(); _top.Clear();
            var feet = p.Feet;
            bool exposed = StepRead.Exposed(p.SightLight);
            Color tint = Color.clear;
            if (p.MaskHolds) tint = Mats.Pal.Dominion;
            else if (p.InMist) tint = Mist;
            else if (Game.Lights != null && Game.Lights.InDarkness(feet)) tint = Mats.Pal.Shade;
            var warm = Warm;

            // near warning: the rim takes the nearest such guard's colour
            Npc warn = null; float warnD = float.MaxValue;
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                {
                    if (!Watcher(n)) continue;
                    float d = Util.FlatDistance(n.transform.position, feet);
                    if (d > 15f || d >= warnD) continue;
                    if (StepRead.NearWarning(n.Vision, n.transform.position, n.Forward, feet)) { warn = n; warnD = d; }
                }

            Color rim = tint.a > 0f ? A(tint, 0.85f) : exposed ? A(warm, 0.85f) : Hidden;
            if (warn) rim = A(ConeRenderer.StateColor(warn), 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)));
            if (exposed) Disc(R - RimW, A(tint.a > 0f ? tint : warm, 0.28f));
            // the warning thickens the rim too: a relaxed guard's bone is close to the hidden ring's
            Ring(R - (warn ? RimW * 2.6f : RimW), R, rim);
            if (Game.Lights != null && Game.Lights.BurnAt(feet) > 0f) Ring(R + 0.05f, R + 0.09f, A(Mats.Pal.Holy, 0.9f));

            // watcher ticks
            if (Game.AI != null)
                foreach (var n in Game.AI.Npcs)
                {
                    if (!Watcher(n)) continue;
                    if (!n.InSight && n.Detection <= 0.01f) continue;
                    var to = n.transform.position - feet; to.y = 0f;
                    if (to.sqrMagnitude > WatchRange * WatchRange || to.sqrMagnitude < 0.01f) continue;
                    var col = TickColor(n);
                    col.a = n.InSight ? 0.95f : 0.45f;
                    Tick(to.normalized, StepRead.TickLength(n.Detection), col, n.InGrace);
                }

            // the toe
            if (_toe != StepRead.Toe.None)
            {
                Color tc = _toe == StepRead.Toe.Warm ? A(warm, 0.9f) : _toeBy ? A(ConeRenderer.StateColor(_toeBy), 0.95f) : A(Mats.Pal.Alerted, 0.95f);
                if (_toe == StepRead.Toe.Flash) tc.a *= Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f ? 1f : 0.25f;
                ToeWedge(_toeDir, tc);
            }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(_t, 0);
            _mesh.SetTriangles(_top, 1);
            _mesh.RecalculateBounds();
        }

        /// <summary>The overhead meter's colour for a relaxed guard (amber to red with the meter), else his state colour.</summary>
        public static Color TickColor(Npc n) =>
            n.State == NpcState.Relaxed ? Color.Lerp(Mats.Pal.Suspicious, Mats.Pal.Alerted, Mathf.Clamp01(n.Detection)) : ConeRenderer.StateColor(n);

        static Color A(Color c, float a) { c.a = a; return c; }

        void Disc(float r, Color col)
        {
            int c0 = _v.Count;
            _v.Add(Vector3.zero); _c.Add(col);
            for (int i = 0; i <= Segs; i++)
            {
                float a = i * Mathf.PI * 2f / Segs;
                _v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); _c.Add(col);
            }
            for (int i = 0; i < Segs; i++) { _t.Add(c0); _t.Add(c0 + 2 + i); _t.Add(c0 + 1 + i); }
        }

        void Ring(float r0, float r1, Color col)
        {
            int o = _v.Count;
            for (int i = 0; i <= Segs; i++)
            {
                float a = i * Mathf.PI * 2f / Segs;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                _v.Add(d * r1); _c.Add(col);
                _v.Add(d * r0); _c.Add(col);
            }
            for (int i = 0; i < Segs; i++)
            {
                int k = o + i * 2;
                _t.Add(k); _t.Add(k + 2); _t.Add(k + 1);
                _t.Add(k + 1); _t.Add(k + 2); _t.Add(k + 3);
            }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int o = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            // both windings, so the strip shows whichever way it was laid
            _top.Add(o); _top.Add(o + 1); _top.Add(o + 2); _top.Add(o); _top.Add(o + 2); _top.Add(o + 3);
            _top.Add(o); _top.Add(o + 2); _top.Add(o + 1); _top.Add(o); _top.Add(o + 3); _top.Add(o + 2);
        }

        void Tick(Vector3 u, float len, Color col, bool dashed)
        {
            var w = new Vector3(-u.z, 0f, u.x) * (TickW * 0.5f);
            float r0 = R + TickGap;
            if (!dashed) { Quad(u * r0 - w, u * r0 + w, u * (r0 + len) + w, u * (r0 + len) - w, col); return; }
            const int dashes = 3;
            float step = len / (dashes * 2 - 1);
            for (int i = 0; i < dashes; i++)
            {
                float a = r0 + step * i * 2, b = a + step;
                Quad(u * a - w, u * a + w, u * b + w, u * b - w, col);
            }
        }

        void ToeWedge(Vector3 u, Color col)
        {
            var w = new Vector3(-u.z, 0f, u.x);
            int o = _v.Count;
            _v.Add(u * (R + 0.45f)); _v.Add(u * (R - 0.02f) + w * 0.24f); _v.Add(u * (R + 0.1f)); _v.Add(u * (R - 0.02f) - w * 0.24f);
            for (int i = 0; i < 4; i++) _c.Add(col);
            // a chevron: two triangles meeting at the tip, both windings
            _top.Add(o); _top.Add(o + 1); _top.Add(o + 2); _top.Add(o); _top.Add(o + 2); _top.Add(o + 1);
            _top.Add(o); _top.Add(o + 2); _top.Add(o + 3); _top.Add(o); _top.Add(o + 3); _top.Add(o + 2);
        }
    }
}
