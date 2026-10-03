#if UNITY_EDITOR || DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Vespertine.Abilities;
using Vespertine.AI;
using Vespertine.Data;
using Vespertine.Player;
using Vespertine.Level;
using Vespertine.Progression;
using Vespertine.Stealth;

namespace Vespertine.Core
{
    /// <summary>
    /// Development check (P4): casts every active ability and thrall command in the running mission against live
    /// targets and asserts its effect (states, lights, blood, position). Start it with
    /// <c>DevAbilitySweep.Run()</c> from an editor eval; results are logged as one warning per ability and kept in
    /// <see cref="Report"/>. The campaign's skills and the player's god mode are restored afterwards.
    /// Best run in an Act I/II street mission with unwarded humans and lamps (M02, M05).
    /// </summary>
    public class DevAbilitySweep : MonoBehaviour
    {
        public static string Report = "";
        public static bool Running;
        readonly List<string> _lines = new List<string>();
        readonly HashSet<Npc> _used = new HashSet<Npc>();
        int _pass, _fail;

        int _part = 1;

        /// <summary>Part 1: every active ability and most passives. Part 6: the flag mods (X7). Part 7: footsteps, doors and the
        /// M01 gate (D119-D123). Parts 2-4 (restart the mission before each):
        /// Lingering Dark + Terror; Dread Feast + False Trail; direct control (move keys, a thrall under her hand, follow, climbing
        /// by a push), which need humans and lamps nothing has disturbed yet.
        /// False Trail needs hounds (M08); the others a street mission (M02).</summary>
        public static void Run(int part = 1)
        {
            if (Running || Game.Player == null || Game.Level == null) return;
            new GameObject("DevAbilitySweep").AddComponent<DevAbilitySweep>()._part = part;
        }

        void Start() { StartCoroutine(Sweep()); }

        Vampire P => Game.Player;

        void Log(string id, bool ok, string detail)
        {
            // "no target"/"no corpse": the mission ran out of calm humans, not an ability failure
            if (!ok && detail != null && detail.StartsWith("no ")) { _lines.Add("SKIP " + id + ": " + detail); return; }
            if (ok) _pass++; else _fail++;
            string line = (ok ? "PASS " : "FAIL ") + id + (string.IsNullOrEmpty(detail) ? "" : ": " + detail);
            _lines.Add(line);
            Debug.LogWarning("[sweep] " + line);
        }

        IEnumerator Sweep()
        {
            Running = true;
            var c = Game.Campaign;
            var nodes = new List<string>(c.Nodes);
            var gifts = new List<string>(c.Gifts);
            var loadout = new List<string>(c.Loadout);
            bool god = Vampire.GodMode, noTarget = Vampire.NoTarget;
            Vampire.GodMode = true;
            Vampire.NoTarget = true;
            foreach (var n in Skills.Nodes) if (!c.Nodes.Contains(n.Id)) c.Nodes.Add(n.Id);
            c.Nodes.Remove("dominion.beckon_mimic");   // tested separately below
            c.Nodes.Remove("shade.shroud");

            if (_part >= 2)
            {
                // parts 2-5 (each on a fresh mission): checks that need untouched humans and lamps
                if (_part == 2) { yield return Step(Lingering()); yield return Step(Terror()); }
                else if (_part == 3) { yield return Step(DreadFeast()); yield return Step(FalseTrail()); }
                else if (_part == 4) { yield return Step(ControlWalk()); yield return Step(ControlThrall()); yield return Step(ControlClimb()); }
                else if (_part == 5) yield return Step(DreadPresence());   // needs calm citizens (M06)
                else if (_part == 7)
                {
                    // D119-D123: footsteps, doors, and M01's wheel, valve and gate (run in M01)
                    yield return Step(Footsteps());
                    yield return Step(PlayerDoor());
                    yield return Step(NpcDoor());
                    yield return Step(Hiding());
                    yield return Step(M01Gate());
                }
                else
                {
                    // part 6 (X7): the one-line flag mods. Needs a gas group and a barred gap (M05, M10)
                    yield return Step(SoftLanding());
                    yield return Step(DropFeed());
                    yield return Step(Herding());
                    yield return Step(BlackMain());
                    yield return Step(BetweenBars());
                }
                goto done;
            }
            yield return Step(Pounce());
            yield return Step(MesmerizeThrallCommands());
            yield return Step(Rend());
            yield return Step(Hemorrhage());
            yield return Step(Puppet());
            yield return Step(LivingLie());
            yield return Step(Communion());
            yield return Step(Beckon());
            yield return Step(Snare());
            yield return Step(Smother());
            yield return Step(Dash());
            yield return Step(Gloom());
            yield return Step(Mist());
            yield return Step(Eclipse());
            yield return Step(Apex());
            yield return Step(Court());
            yield return Step(Mend());
            yield return Step(Nightblood());
            yield return Step(Vessel());
            yield return Step(FeedMods());
            yield return Step(Lethe());
            yield return Step(Silverblood());
            c.Nodes.Remove("predator.bound");
            bool leapWithout = (P.AreaMask() & (1 << NavAreas.Leap)) != 0;
            c.Nodes.Add("predator.bound");
            bool leapWith = (P.AreaMask() & (1 << NavAreas.Leap)) != 0;
            Log("predator.bound", leapWith && (P.Awakening >= 3 || !leapWithout), $"leap links: without {leapWithout}, with {leapWith} (awakening {P.Awakening})");
            yield return Step(HolyGround());
            yield return Step(AwakeningGates());
            int withCourt = P.MaxThralls;
            c.Nodes.Remove("dominion.court");
            int withSecond = P.MaxThralls;
            c.Nodes.Remove("dominion.thrall_second");
            int bare = P.MaxThralls;
            c.Nodes.Add("dominion.court"); c.Nodes.Add("dominion.thrall_second");
            Log("dominion.thrall_second/court", bare == 1 && withSecond == 2 && withCourt == 3, $"max thralls {bare}/{withSecond}/{withCourt}");

        done:
            c.Nodes.Clear(); c.Nodes.AddRange(nodes);
            c.Gifts.Clear(); c.Gifts.AddRange(gifts);
            c.Loadout.Clear(); c.Loadout.AddRange(loadout);
            Vampire.GodMode = god;
            Vampire.NoTarget = noTarget;
            Time.timeScale = 1f;
            Report = $"{_pass} passed, {_fail} failed\n" + string.Join("\n", _lines);
            Debug.LogWarning("[sweep] DONE " + _pass + " passed, " + _fail + " failed");
            Running = false;
            Destroy(gameObject);
        }

        /// <summary>Runs one check; an exception is a failure, not the end of the sweep.</summary>
        IEnumerator Step(IEnumerator body)
        {
            P.CancelAll();
            if (P.InMist) P.SetMist(false);
            P.Blood = P.MaxBlood;
            ClearCooldowns();
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (System.Exception e) { Log("exception", false, e.Message); break; }
                yield return cur;
            }
        }

        void ClearCooldowns()
        {
            var f = typeof(Vampire).GetField("_cd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            (f?.GetValue(P) as System.Collections.IDictionary)?.Clear();
        }

        // ------------------------------------------------------------------ targets

        static bool Plain(Npc n) => n && n.gameObject.activeInHierarchy && n.IsAlive && !n.Incapacitated && !n.Friendly
            && n.State == NpcState.Relaxed && !n.Arch.Has(ArchFlags.Quadruped) && !n.Immune(out _) && n.Arch.Morale != Morale.Fearless
            && !Critical(n);

        /// <summary>Someone an objective needs alive (M05's Penrose, Tobias): killing them would fail the mission mid-sweep.</summary>
        static bool Critical(Npc n)
        {
            if (Game.Mission == null) return false;
            foreach (var o in Game.Mission.Objectives)
                if ((o.Type == "deliver" || o.Type == "protect" || o.Type == "escort") && o.Spec.Args.Count > 0 && o.Spec.Args[0] == n.Id) return true;
            return false;
        }

        /// <summary>The nearest unused plain human, with Ilse teleported a couple of metres behind them.</summary>
        Npc Target(float behind = 2.2f, System.Func<Npc, bool> extra = null)
        {
            Npc best = null; float bd = float.MaxValue;
            foreach (var n in Game.AI.Living())
            {
                if (!Plain(n) || _used.Contains(n) || (extra != null && !extra(n))) continue;
                float d = Vector3.Distance(n.transform.position, P.Feet);
                if (d < bd && StandBehind(n, behind, out _)) { bd = d; best = n; }
            }
            if (best == null) return null;
            _used.Add(best);
            StandBehind(best, behind, out var at);
            P.Teleport(at);
            P.FaceTowards(best.transform.position);
            return best;
        }

        bool StandBehind(Npc n, float dist, out Vector3 at)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            for (int i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0f, i * 45f, 0f) * -n.Forward;
                var want = n.transform.position + dir * dist;
                if (NavMesh.SamplePosition(want, out var h, 0.8f, filter) && Mathf.Abs(h.position.y - n.transform.position.y) < 0.6f)
                { at = h.position; return true; }
            }
            at = default;
            return false;
        }

        bool Cast(string id, Npc n = null, Vector3? point = null, GameLight light = null)
        {
            var a = Skills.Ability(id);
            return P.Cast(a, new PlayerAction { Kind = ActionKind.Ability, Ability = id, Npc = n, Point = point ?? (n ? n.transform.position : P.Feet), Light = light });
        }

        static IEnumerator Wait(float s) { float t = 0f; while (t < s) { t += Time.deltaTime; yield return null; } }

        IEnumerator WaitFor(System.Func<bool> cond, float timeout)
        {
            float t = 0f;
            while (t < timeout && !cond()) { t += Time.deltaTime; yield return null; }
        }

        // ------------------------------------------------------------------ checks

        /// <summary>The real interact key (E): whatever <see cref="Vampire.NearUse"/> picks.</summary>
        void InteractKey() =>
            typeof(Vampire).GetMethod("TryInteractKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(P, null);

        /// <summary>D119: a calm human 2.4 m away hears her walk but not her sneak. NoTarget stays on, so only sound can stir him.</summary>
        IEnumerator Footsteps()
        {
            // one calm human standing still (a post, or a pause on his beat): sneak beside him, then walk beside him
            var n = Target(2.4f, x => !x.Asleep && Game.Level.SurfaceAt(x.transform.position) != Surface.Water && x.Agent && x.Agent.velocity.magnitude < 0.1f);
            if (n == null) { Log("footsteps", false, "no target"); yield break; }
            string detail = n.Id + ": ";
            bool ok = true;
            for (int k = 0; k < 2; k++)
            {
                bool sneak = k == 0;
                // pace back and forth beside him at the same distance
                var tangent = Vector3.Cross(Vector3.up, (P.Feet - n.transform.position).Flat().normalized);
                P.DebugSneak = sneak; P.DebugRun = false;
                float maxD = 0f;
                for (int leg = 0; leg < 2; leg++)
                {
                    P.DebugMove = (leg == 0 ? tangent : -tangent) * 0.5f;
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime) { maxD = Mathf.Max(maxD, Util.FlatDistance(P.Feet, n.transform.position)); yield return null; }
                }
                P.DebugMove = null; P.DebugSneak = false;
                yield return Wait(0.2f);
                bool stirred = n.State != NpcState.Relaxed;
                detail += $"{(sneak ? "sneak" : "walk")} {n.State} (within {maxD:0.0} m); ";
                ok &= maxD < 3.1f && (sneak ? !stirred : stirred);
            }
            Log("footsteps", ok, detail);
        }

        /// <summary>D121: a shut door stops her; the interact key opens it; then she walks through.</summary>
        IEnumerator PlayerDoor()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            Door door = null; Vector3 side = Vector3.zero; float bd = float.MaxValue;
            foreach (var d in Door.Live)
            {
                if (!d || !d.isActiveAndEnabled || !d.Enabled || d.IsOpen || d.BlocksVampire || d.Occupied()) continue;
                var pass = d.EastWest ? Vector3.right : Vector3.forward;
                foreach (var sd in new[] { pass, -pass })
                {
                    bool both = NavMesh.SamplePosition(d.transform.position + sd * 1.4f, out var a, 0.35f, filter)
                             && NavMesh.SamplePosition(d.transform.position - sd * 1.4f, out var b, 0.35f, filter);
                    float dist = Vector3.Distance(d.transform.position, P.Feet);
                    if (both && dist < bd) { bd = dist; door = d; side = sd; }
                }
            }
            if (door == null) { _lines.Add("SKIP door.player: no shut door with floor both sides"); yield break; }
            var dp = door.transform.position;
            P.Teleport(dp + side * 1.4f);
            yield return Wait(0.2f);
            P.DebugMove = -side;
            yield return Wait(1.2f);
            P.DebugMove = null;
            float stoppedAt = Vector3.Dot(P.Feet - dp, side);
            var offered = P.NearUse;
            InteractKey();
            yield return WaitFor(() => !door.Shut, 2f);
            bool opened = door.IsOpen && !door.Shut;
            P.DebugMove = -side;
            yield return Wait(1.6f);
            P.DebugMove = null;
            float after = Vector3.Dot(P.Feet - dp, side);
            Log("door.player", stoppedAt > 0.3f && offered == door && opened && after < -0.5f,
                $"{door.Id}: stopped {stoppedAt:0.00} m short, offered {(offered ? offered.Id : "nothing")}, opened={opened}, then {-after:0.0} m beyond");
        }

        /// <summary>D122: beside a hiding place the interact key hides her and lets her out; carrying a body, it hides the body.</summary>
        IEnumerator Hiding()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            HideSpot spot = null; Vector3 stand = default; float bd = float.MaxValue;
            foreach (var h in Game.Level.All<HideSpot>())
            {
                if (!h.Enabled || h.Full || !h.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(h.transform.position, P.Feet);
                if (d < bd && NavMesh.SamplePosition(h.transform.position, out var hit, 1.4f, filter)) { bd = d; spot = h; stand = hit.position; }
            }
            if (spot == null) { _lines.Add("SKIP hiding: no hiding place"); yield break; }
            P.Teleport(stand);
            P.FaceTowards(spot.transform.position);
            yield return Wait(0.2f);
            var offered = P.NearUse;
            string verb = offered ? offered.Verb : "";
            InteractKey();
            yield return WaitFor(() => P.Concealed, 2f);
            bool inside = P.Concealed && P.InsideSpot == spot;
            InteractKey();
            yield return WaitFor(() => !P.Concealed, 2f);
            bool left = !P.Concealed;

            // a body: someone killed on the spot, carried to the hiding place
            var n = Target(1.5f, x => !Critical(x));
            if (n == null) { Log("hiding", false, "no target for a body"); yield break; }
            n.Die("sweep");
            yield return Wait(0.3f);
            bool carrying = P.StartCarry(n);
            P.Teleport(stand);
            P.FaceTowards(spot.transform.position);
            yield return Wait(0.2f);
            var bodyOffer = P.NearUse;
            string bodyVerb = bodyOffer ? bodyOffer.Verb : "";
            InteractKey();
            yield return WaitFor(() => P.Carrying == null, 3f);
            bool stored = P.Carrying == null && n.Hidden && spot.Bodies.Contains(n.Id);
            Log("hiding", offered == spot && verb == "Hide inside" && inside && left && carrying && bodyOffer == spot && bodyVerb == "Hide the body" && stored,
                $"{spot.Id}: offered '{verb}', inside={inside}, left={left}; body {n.Id}: carried={carrying}, offered '{bodyVerb}', hidden={stored}");
        }

        /// <summary>D121: a human walking to a spot beyond a shut door opens it, goes through, and shuts it behind him.</summary>
        IEnumerator NpcDoor()
        {
            Door door = null; Npc who = null; Vector3 goal = default; float bd = float.MaxValue;
            foreach (var d in Door.Live)
            {
                if (!d || !d.isActiveAndEnabled || d.IsOpen) continue;
                var pass = d.EastWest ? Vector3.right : Vector3.forward;
                foreach (var n in Game.AI.Living())
                {
                    if (!Plain(n) || !n.Agent || !n.Agent.isOnNavMesh) continue;
                    float dist = Vector3.Distance(n.transform.position, d.transform.position);
                    if (dist > 16f || dist >= bd) continue;
                    float s = Mathf.Sign(Vector3.Dot(n.transform.position - d.transform.position, pass));
                    var filter = new NavMeshQueryFilter { agentTypeID = n.Agent.agentTypeID, areaMask = n.Agent.areaMask };
                    if (!NavMesh.SamplePosition(d.transform.position - pass * s * 2f, out var h, 0.6f, filter)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(n.transform.position, h.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    // the path must run through this doorway, not round by another
                    bool through = false;
                    for (int i = 1; i < path.corners.Length && !through; i++)
                    {
                        var c = (path.corners[i - 1] + path.corners[i]) * 0.5f;
                        through = Vector3.Distance(path.corners[i], d.transform.position) < 1.2f || Vector3.Distance(c, d.transform.position) < 1.2f
                            || Mathf.Sign(Vector3.Dot(path.corners[i] - d.transform.position, pass)) != Mathf.Sign(Vector3.Dot(path.corners[i - 1] - d.transform.position, pass))
                               && Util.FlatDistance(path.corners[i], d.transform.position) < 3f;
                    }
                    if (!through) continue;
                    bd = dist; door = d; who = n; goal = h.position;
                }
            }
            if (door == null) { _lines.Add("SKIP door.npc: no calm human near a shut door"); yield break; }
            P.Teleport(P.Feet);   // stand still, wherever she is
            who.EnterInvestigating(goal, false, false);
            yield return WaitFor(() => door.IsOpen, 20f);
            bool opened = door.IsOpen;
            yield return WaitFor(() => Util.FlatDistance(who.transform.position, goal) < 1.2f, 15f);
            bool arrived = Util.FlatDistance(who.transform.position, goal) < 1.2f;
            yield return WaitFor(() => !door.IsOpen, 6f);
            Log("door.npc", opened && arrived && !door.IsOpen,
                $"{who.Id} through {door.Id}: opened={opened}, arrived={arrived}, shut after={!door.IsOpen}");
        }

        /// <summary>D123: M01's gate valve is sealed until the wheel is taken; turning it calls the rushers and the gate rises over
        /// a few seconds, after which she can walk through.</summary>
        IEnumerator M01Gate()
        {
            var L = Game.Level;
            var wheel = L.Get<Interactable>("wheel");
            var v = L.Get<Valve>("v1");
            var g = L.Get<Gate>("g1");
            var rush = L.Get<Npc>("o_rush1");
            if (!wheel || !v || !g) { _lines.Add("SKIP m01.gate: not M01"); yield break; }
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            bool sealedFirst = v.Sealed && !v.PlayerCan && !string.IsNullOrEmpty(v.Unavailable);
            bool gateSays = !string.IsNullOrEmpty(g.Unavailable) && !g.PlayerCan;
            bool rushHidden = rush && !rush.gameObject.activeInHierarchy;

            // take the wheel with the interact key
            if (!NavMesh.SamplePosition(wheel.transform.position, out var wh, 1.5f, filter)) { Log("m01.gate", false, "no floor by the wheel"); yield break; }
            P.Teleport(wh.position);
            P.FaceTowards(wheel.transform.position);
            yield return Wait(0.2f);
            InteractKey();
            yield return WaitFor(() => wheel.Used, 4f);
            bool took = wheel.Used;
            bool unsealed = !v.Sealed && v.PlayerCan;

            // fit and turn it at the valve
            if (!NavMesh.SamplePosition(v.transform.position, out var vh, 1.5f, filter)) { Log("m01.gate", false, "no floor by the valve"); yield break; }
            P.Teleport(vh.position);
            P.FaceTowards(v.transform.position);
            yield return Wait(0.2f);
            var offered = P.NearUse;
            InteractKey();
            yield return WaitFor(() => g.Open, 6f);
            bool turned = g.Open;
            bool risingSlowly = !g.Clear;
            bool rushCame = rush && rush.gameObject.activeInHierarchy;
            yield return WaitFor(() => g.Clear, 4f);
            bool clear = g.Clear;

            // walk through, from the valve's side
            var inside = g.Pass * Mathf.Sign(Vector3.Dot(v.transform.position - g.transform.position, g.Pass));
            P.Teleport(g.transform.position + inside * 1.5f);
            yield return Wait(0.2f);
            P.DebugMove = -inside;
            // stepping out completes the escape and the debrief stops game time, so this wait runs on real time
            float rt = 0f;
            while (rt < 1.6f && Time.timeScale > 0f) { rt += Time.unscaledDeltaTime; yield return null; }
            P.DebugMove = null;
            float beyond = -Vector3.Dot(P.Feet - g.transform.position, inside);
            bool escaped = false;
            foreach (var o in Game.Mission.Objectives) if (o.Type == "escape" && o.Complete) escaped = true;
            Log("m01.gate", sealedFirst && gateSays && rushHidden && took && unsealed && offered == v && turned && risingSlowly && rushCame && clear && (beyond > 0.5f || escaped),
                $"sealed first={sealedFirst}, gate explains={gateSays}, rushers hidden={rushHidden}, took wheel={took}, valve unsealed={unsealed}, " +
                $"offered {(offered ? offered.Id : "nothing")}, gate open={turned}, rose slowly={risingSlowly}, rushers came={rushCame}, clear={clear}, {beyond:0.0} m beyond, escaped={escaped}");
        }

        IEnumerator Pounce()
        {
            var n = Target(5f);
            if (!n) { Log("predator.pounce", false, "no target"); yield break; }
            bool cast = Cast("predator.pounce", n);
            yield return WaitFor(() => P.Feeding == n, 2f);
            Log("predator.pounce", cast && P.Feeding == n, cast ? "feed started=" + (P.Feeding == n) : "cast refused");
            yield return WaitFor(() => P.Feeding == null, 12f);
            yield return Wait(0.3f);
            Log("feed.sip", n.State == NpcState.Dazed, n.State.ToString());
        }

        IEnumerator MesmerizeThrallCommands()
        {
            var n = Target(4f);
            if (!n) { Log("dominion.mesmerize", false, "no target"); yield break; }
            bool cast = Cast("dominion.mesmerize", n);
            yield return null;
            Log("dominion.mesmerize", cast && n.State == NpcState.Mesmerised, n.State.ToString());

            cast = Cast("dominion.thrall", n);
            yield return null;
            bool thrall = cast && n.State == NpcState.Thrall && P.Thralls.Contains(n);
            Log("dominion.thrall", thrall, n.State.ToString());
            if (!thrall) yield break;

            // False Orders: the nearest living human of any faction is sent to hold a point
            Npc g = null; float bd = float.MaxValue;
            foreach (var o in Game.AI.Living())
                if (Plain(o) && !_used.Contains(o) && o != n) { float d = Vector3.Distance(o.transform.position, n.transform.position); if (d < bd) { bd = d; g = o; } }
            if (!g) { Log("dominion.false_orders", false, "no second human"); yield break; }
            _used.Add(g);
            var sendTo = g.transform.position + g.Forward * 3f;
            n.OrderFalse(g, sendTo);
            yield return WaitFor(() => g.State == NpcState.Holding, 30f);
            Log("dominion.false_orders", g.State == NpcState.Holding, $"{g.Id} {g.State} ({bd:0} m away)");

            n.OrderStrike(g);
            yield return WaitFor(() => !g.IsAlive, 30f);
            Log("dominion.puppet_strike", !g.IsAlive, $"{g.Id} {g.State}");
            n.ReleaseFromThrall(false);
            yield return null;
            Log("thrall.release", n.State != NpcState.Thrall && !P.Thralls.Contains(n), n.State.ToString());
        }

        Npc _rendBody, _hemoBody;

        IEnumerator Rend()
        {
            var n = Target(1.2f);
            if (!n) { Log("predator.rend", false, "no target"); yield break; }
            bool cast = Cast("predator.rend", n);
            yield return Wait(0.6f);
            _rendBody = n;
            Log("predator.rend", cast && !n.IsAlive, n.State + " by " + n.KilledBy);
        }

        IEnumerator Hemorrhage()
        {
            var n = Target(6f);
            if (!n) { Log("sanguis.hemorrhage", false, "no target"); yield break; }
            bool cast = Cast("sanguis.hemorrhage", n);
            yield return WaitFor(() => !n.IsAlive, 6f);
            _hemoBody = n;
            Log("sanguis.hemorrhage", cast && !n.IsAlive, n.State + " by " + n.KilledBy);
        }

        IEnumerator Puppet()
        {
            var n = _rendBody;
            if (!n || n.IsAlive) { Log("sanguis.puppet", false, "no corpse"); yield break; }
            P.Teleport(n.transform.position + Vector3.right * 1.5f);
            yield return null;
            bool cast = Cast("sanguis.puppet", n);
            yield return Wait(0.5f);
            Log("sanguis.puppet", cast && n.IsCorpsePuppet && n.State != NpcState.Dead, n.State.ToString());
        }

        IEnumerator LivingLie()
        {
            var n = _rendBody;
            if (!n || !n.IsCorpsePuppet) { Log("dominion.living_lie", false, "no puppet"); yield break; }
            var c = Game.Campaign;
            c.Nodes.Remove("dominion.living_lie");
            bool without = P.ThrallSlotAvailable(1, out var why, n);
            c.Nodes.Add("dominion.living_lie");
            bool with = P.ThrallSlotAvailable(1, out _, n);
            Log("dominion.living_lie", !without && with, $"puppet false orders: without {without} ({why}), with {with}");
        }

        /// <summary>Awakening 9: a citizen who sees her panics without a scream.</summary>
        IEnumerator DreadPresence()
        {
            var c = Game.Campaign;
            int vitae = c.Vitae;
            int screams = 0;
            System.Action<Vector3, float, NoiseKind> count = (p, r, k) => { if (k == NoiseKind.Scream) screams++; };
            Game.Noise.OnNoise += count;
            var res = new StringBuilder();
            bool ok = true;
            foreach (int lvl in new[] { 9, 8 })   // silent first: an L8 scream would panic the other citizens
            {
                var n = Target(4f, o => o.Arch.Morale == Morale.Civilian);
                if (n == null) { Game.Noise.OnNoise -= count; c.Vitae = vitae; Log("awakening.dread_presence", false, "no civilian"); yield break; }
                c.Vitae = CampaignState.VitaeThresholds[lvl - 1];
                screams = 0;
                n.Spot(P.Feet);
                yield return null;
                bool fled = n.State == NpcState.Panicked;
                ok &= fled && (lvl >= 9 ? screams == 0 : screams > 0);
                res.Append($"L{lvl}: {n.Id} {n.State}, screams {screams}; ");
            }
            Game.Noise.OnNoise -= count;
            c.Vitae = vitae;
            Log("awakening.dread_presence", ok, res.ToString());
        }

        IEnumerator Communion()
        {
            var n = _hemoBody;
            if (!n || n.IsAlive) { Log("sanguis.communion", false, "no corpse"); yield break; }
            P.Teleport(n.transform.position + Vector3.right * 1.5f);
            yield return null;
            P.Blood = P.MaxBlood * 0.5f;
            ClearCooldowns();
            float before = P.Blood - Skills.Ability("sanguis.communion").Cost;
            bool cast = Cast("sanguis.communion");
            yield return null;
            Log("sanguis.communion", cast && n.Drained && P.Blood > before, $"blood {before:0} -> {P.Blood:0}, drained={n.Drained}");
        }

        IEnumerator Beckon()
        {
            var n = Target(8f);
            if (!n) { Log("dominion.beckon", false, "no target"); yield break; }
            var pt = Vector3.Lerp(n.transform.position, P.Feet, 0.5f);
            // as the aim does: the point must be somewhere a human can walk
            if (NavMesh.SamplePosition(pt, out var hh, 1.5f, new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask })) pt = hh.position;
            float d0 = Util.FlatDistance(n.transform.position, pt);
            bool cast = Cast("dominion.beckon", n, pt);
            yield return Wait(3f);
            float d1 = Util.FlatDistance(n.transform.position, pt);
            yield return WaitFor(() => n.State != NpcState.Distracted, 10f);   // walks there, looks about, goes back Wary
            Log("dominion.beckon", cast && d1 < d0 - 1f && n.Wary, $"{d0:0.0} -> {d1:0.0} m, then {n.State}, wary={n.Wary}");

            // Familiar Voice: the same whisper leaves no suspicion behind
            var m = Target(8f);
            if (!m) { Log("dominion.beckon_mimic", false, "no target"); yield break; }
            Game.Campaign.Nodes.Add("dominion.beckon_mimic");
            ClearCooldowns();
            P.Blood = P.MaxBlood;
            var pm = Vector3.Lerp(m.transform.position, P.Feet, 0.5f);
            if (NavMesh.SamplePosition(pm, out var hm, 1.5f, new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask })) pm = hm.position;
            cast = Cast("dominion.beckon", m, pm);
            yield return WaitFor(() => m.State != NpcState.Distracted, 14f);
            Log("dominion.beckon_mimic", cast && !m.Wary, $"then {m.State}, wary={m.Wary}");
            Game.Campaign.Nodes.Remove("dominion.beckon_mimic");
        }

        IEnumerator Snare()
        {
            var n = Target(4f);
            if (!n) { Log("sanguis.snare", false, "no target"); yield break; }
            bool cast = Cast("sanguis.snare", null, n.transform.position);
            yield return WaitFor(() => n.State == NpcState.Dazed, 2f);
            Log("sanguis.snare", cast && n.State == NpcState.Dazed, n.State.ToString());
        }

        IEnumerator Smother()
        {
            GameLight best = null; float bd = float.MaxValue;
            foreach (var l in Game.Lights.All)
                if (l && l.CanSmother) { float d = Vector3.Distance(l.transform.position, P.Feet); if (d < bd) { bd = d; best = l; } }
            if (!best) { Log("shade.smother", false, "no light"); yield break; }
            bool cast = Cast("shade.smother", null, null, best);
            yield return null;
            Log("shade.smother", cast && !best.On, best.name);
            best.SetOn(true);
        }

        /// <summary>Shadow Dash (D155): 6 m along her facing, one charge spent; Umbral Step's third charge.</summary>
        IEnumerator Dash()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            for (int i = 0; i < 24; i++)
            {
                var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                if (NavMesh.Raycast(P.Feet, P.Feet + dir * 6.5f, out _, filter)) continue;
                P.DevRefillDash();
                int before = P.DashCharges;
                var from = P.Feet;
                P.transform.rotation = Quaternion.LookRotation(dir);
                P.DebugDash = true;
                yield return Wait(0.5f);
                float d = Util.FlatDistance(P.Feet, from);
                Log("shade.dash", d > 5.4f && d < 6.6f && before == 3 && P.DashCharges == 2, $"{d:0.0} m, charges {before} -> {P.DashCharges}");
                yield break;
            }
            Log("shade.dash", false, "no clear 6.5 m of floor");
        }

        IEnumerator Gloom()
        {
            var n = Target(8f, o => o.Arch.Morale != Morale.Fearless);
            if (!n) { Log("shade.gloom", false, "no target"); yield break; }
            Game.Campaign.Nodes.Add("shade.shroud");
            var p = n.transform.position;
            bool cast = Cast("shade.gloom", null, p);
            yield return null;
            float lit = Game.Lights.LightAt(p);
            Log("shade.gloom", cast && FindAnyObjectByType<GloomSphere>() != null && lit < DetectionMath.SuspiciousAt, $"light inside {lit:0.00}");
            yield return WaitFor(() => n.Asleep, 5f);
            Log("shade.shroud", n.Asleep, n.State + " asleep=" + n.Asleep);
            Game.Campaign.Nodes.Remove("shade.shroud");
        }

        IEnumerator Mist()
        {
            bool cast = Cast("shade.mist");
            yield return Wait(1f);
            float b0 = P.Blood;
            yield return Wait(1f);
            Log("shade.mist", cast && P.InMist && P.Blood < b0, $"inMist={P.InMist}, upkeep {b0 - P.Blood:0.0}/s");
            P.SetMist(false);
        }

        IEnumerator Eclipse()
        {
            int before = 0;
            foreach (var l in Game.Lights.All) if (l && l.On && l.Kind != LightKind.Moon && Vector3.Distance(l.transform.position, P.Feet) < 25f) before++;
            bool cast = Cast("shade.eclipse");
            yield return null;
            int after = 0;
            foreach (var l in Game.Lights.All) if (l && l.On && l.Kind != LightKind.Moon && Vector3.Distance(l.transform.position, P.Feet) < 25f) after++;
            Log("shade.eclipse", cast && before > 0 && after == 0, $"{before} lights -> {after}");
        }

        IEnumerator Apex()
        {
            bool cast = Cast("predator.apex");
            yield return null;
            Log("predator.apex", cast && P.ApexActive, "active=" + P.ApexActive);
            yield return Wait(8.5f);
        }

        IEnumerator Court()
        {
            var n = Target(3f);
            if (!n) { Log("dominion.court", false, "no target"); yield break; }
            int near = 0;
            foreach (var o in Game.AI.Living()) if (Plain(o) && Vector3.Distance(o.transform.position, P.Feet) < 10f) near++;
            bool cast = Cast("dominion.court");
            yield return null;
            int mez = 0;
            foreach (var o in Game.AI.Living()) if (o.State == NpcState.Mesmerised && Vector3.Distance(o.transform.position, P.Feet) < 10f) mez++;
            Log("dominion.court", cast && mez >= near && mez > 0, $"{mez} of {near} mesmerised");
        }

        IEnumerator Mend()
        {
            P.HP = P.MaxHP * 0.2f;
            bool cast = Cast("sanguis.mend");
            yield return null;
            Log("sanguis.mend", cast && P.HP >= P.MaxHP * 0.69f, $"{P.HP:0}/{P.MaxHP:0}");
        }

        IEnumerator Nightblood()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            // a dark spot with nobody near: Court's mesmerised walk up to her, and a lantern among them lights her
            bool dark = false;
            for (int i = 0; i < 72 && !dark; i++)
            {
                var want = P.Feet + Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward * (6f + 4f * (i / 24));
                if (!NavMesh.SamplePosition(want, out var h, 1f, filter) || Game.Lights.LightAt(h.position) >= DetectionMath.SuspiciousAt * 0.5f) continue;
                bool alone = true;
                foreach (var o in Game.AI.Living()) if (Vector3.Distance(o.transform.position, h.position) < 8f) { alone = false; break; }
                if (alone) { P.Teleport(h.position); dark = true; }
            }
            if (!dark && P.InDark) dark = true;
            yield return Wait(0.5f);
            P.HP = P.MaxHP * 0.3f;
            float hp0 = P.HP, b0 = P.Blood;
            yield return Wait(3f);
            // ordinary regen buys HP with blood one for one; Nightblood's 1 HP/s on top of it is free
            float free = (P.HP - hp0) - (b0 - P.Blood);
            Log("shade.nightblood", P.InDark && free > 2.4f && free < 3.6f, $"dark={P.InDark} (light {P.Light:0.00}), {free:0.0} free HP in 3 s");
            P.HP = P.MaxHP;
        }

        float FeedDur()
        {
            var f = typeof(Vampire).GetField("_feedDur", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null ? (float)f.GetValue(P) : -1f;
        }

        int StainsNear(Vector3 p)
        {
            int k = 0;
            foreach (var st in Evidence.Stains) if (st && !st.IsTrail && Vector3.Distance(st.transform.position, p) < 3f) k++;
            return k;
        }

        /// <summary>Gorge + Stalker shorten a feed from behind; Clean Feeder leaves no stain on a drain, and without it one is left.</summary>
        IEnumerator FeedMods()
        {
            var n = Target(1f);
            if (!n) { Log("predator.gorge", false, "no target"); yield break; }
            P.FaceTowards(n.transform.position);
            int s0 = StainsNear(n.transform.position);
            bool began = P.BeginFeed(n, true, true);
            float dur = FeedDur();
            bool behind = n.AngleTo(P.transform.position) > 100f;
            float want = 3.2f * 0.6f * (behind ? 0.7f : 1f);
            Log("predator.gorge+stalker", began && Mathf.Abs(dur - want) < 0.01f, $"drain {dur:0.00} s (expected {want:0.00}, from behind={behind})");
            yield return WaitFor(() => P.Feeding == null, 8f);
            yield return Wait(0.3f);
            Log("sanguis.clean", !n.IsAlive && StainsNear(n.transform.position) == s0, $"{n.State}, stains {s0} -> {StainsNear(n.transform.position)}");

            Game.Campaign.Nodes.Remove("sanguis.clean");
            var m = Target(1f);
            if (!m) { Log("feed.drain.stain", false, "no target"); Game.Campaign.Nodes.Add("sanguis.clean"); yield break; }
            int m0 = StainsNear(m.transform.position);
            P.BeginFeed(m, true, true);
            yield return WaitFor(() => P.Feeding == null, 8f);
            yield return Wait(0.3f);
            Log("feed.drain.stain", !m.IsAlive && StainsNear(m.transform.position) > m0, $"{m.State}, stains {m0} -> {StainsNear(m.transform.position)}");
            Game.Campaign.Nodes.Add("sanguis.clean");
        }

        /// <summary>Lethe: a mesmerised human wakes Relaxed, not Wary.</summary>
        IEnumerator Lethe()
        {
            // someone not already Wary: earlier steps leave bodies, and finding one makes the whole street Wary
            var n = Target(4f, x => !x.Wary);
            if (!n) { Log("dominion.mesmerize_forget", false, "no unwary target"); yield break; }
            bool cast = Cast("dominion.mesmerize", n);
            P.Teleport(P.Feet - (n.transform.position - P.Feet).normalized * 6f);
            yield return WaitFor(() => n.State != NpcState.Mesmerised, 12f);
            yield return Wait(0.5f);
            Log("dominion.mesmerize_forget", cast && !n.Wary && n.State == NpcState.Relaxed, $"{n.State}, wary={n.Wary}");
        }

        IEnumerator Silverblood()
        {
            Vampire.GodMode = false;
            P.HP = P.MaxHP;
            float bonus = P.BonusHP;
            P.BonusHP = 0f;
            bool hidden = P.Concealed;
            P.Damage(10f, true, null, false, true);
            float lost = P.MaxHP - P.HP;
            Vampire.GodMode = true;
            P.HP = P.MaxHP;
            P.BonusHP = bonus;
            Log("sanguis.silverblood", Mathf.Abs(lost - 5f) < 0.01f, $"silver 10 -> {lost:0.0} (concealed={hidden})");
            yield break;
        }

        /// <summary>Dread Feast: a human who can see her when a drain ends flees instead of raising the alarm.</summary>
        IEnumerator DreadFeast()
        {
            // two plain humans a few metres apart: one to drain, one to watch
            Npc w = null, v = null;
            foreach (var a in Game.AI.Living())
            {
                if (!Plain(a) || _used.Contains(a)) continue;
                foreach (var b in Game.AI.Living())
                {
                    if (b == a || !Plain(b) || _used.Contains(b)) continue;
                    float d = Vector3.Distance(a.transform.position, b.transform.position);
                    if (d > 1.2f && d < 12f && StandBehind(b, 1f, out _)) { w = a; v = b; break; }
                }
                if (w) break;
            }
            if (!w) { _lines.Add("SKIP predator.dread_feast: no pair of plain humans here"); yield break; }
            _used.Add(w); _used.Add(v);
            StandBehind(v, 1f, out var at);
            P.Teleport(at);
            yield return null;
            bool sees = false;
            P.BeginFeed(v, true, true);
            // she settles behind the victim; the witness walks over towards her (and so faces her) as the drain ends
            yield return null;
            w.EnterDistracted(P.Feet, 10f, false);
            float t = 0f;
            while (P.Feeding != null && t < 8f)
            {
                sees = w.CouldSee(P.Feet, Mathf.Max(P.Light, 0.5f));
                t += Time.deltaTime;
                yield return null;
            }
            yield return null;
            Log("predator.dread_feast", sees && w.State == NpcState.Panicked, $"witness could see={sees}, {w.State}");
        }

        /// <summary>Direct control (D114): the move keys carry her the way they point, at glide speed, and stop her crisply.</summary>
        IEnumerator ControlWalk()
        {
            var start = P.Feet;
            var dir = Vector3.zero;
            // a direction with a few metres of open floor ahead
            for (int i = 0; i < 8 && dir == Vector3.zero; i++)
            {
                var d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
                if (!NavMesh.Raycast(start, start + d * 4f, out _, filter)) dir = d;
            }
            if (dir == Vector3.zero) { _lines.Add("SKIP control.wasd: no open floor around her"); yield break; }
            P.DebugMove = dir; P.DebugRun = false;
            yield return Wait(1f);
            float speed = P.DirectSpeed;
            P.DebugMove = Vector3.zero;
            yield return Wait(0.4f);
            var moved = P.Feet - start; moved.y = 0f;
            float along = Vector3.Dot(moved, dir);
            bool stopped = !P.Moving;
            P.DebugMove = null;
            Log("control.wasd", along > 1.2f && Vector3.Angle(moved, dir) < 20f && speed > 1.5f && stopped,
                $"moved {along:0.0} m along, {Vector3.Angle(moved, dir):0} deg off, {speed:0.0} m/s, stopped={stopped}");
        }

        /// <summary>A thrall taken by its portrait walks with the move keys while Ilse holds; it strikes on its own
        /// order; follow and hold toggle; control returns to Ilse.</summary>
        IEnumerator ControlThrall()
        {
            var t = Target(4f);
            if (!t) { Log("control.thrall", false, "no thrall candidate"); yield break; }
            Cast("dominion.mesmerize", t);
            ClearCooldowns();
            Cast("dominion.thrall", t);
            yield return null;
            if (t.State != NpcState.Thrall) { Log("control.thrall", false, "thrall failed: " + t.State); yield break; }
            P.SelectThrall(t);
            bool camFollows = Game.Cam && Game.Cam.Follow == t.transform;
            var ilse = P.Feet;
            var t0 = t.transform.position;
            var dir = (ilse - t0); dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? -dir.normalized : Vector3.forward;
            P.DebugMove = dir;
            yield return Wait(1.2f);
            P.DebugMove = Vector3.zero;
            yield return Wait(0.3f);
            var tm = t.transform.position - t0; tm.y = 0f;
            float ilseMoved = Vector3.Distance(ilse, P.Feet);
            P.DebugMove = null;
            Log("control.thrall.move", camFollows && tm.magnitude > 0.8f && ilseMoved < 0.2f,
                $"camera follows={camFollows}, thrall moved {tm.magnitude:0.0} m, Ilse moved {ilseMoved:0.00} m");

            t.OrderFollow();
            bool follow = t.Order == ThrallOrder.Follow;
            t.OrderHold();
            bool hold = t.Order == ThrallOrder.None;
            Log("control.thrall.follow", follow && hold, $"follow={follow}, hold={hold}");

            Npc b = null; float bd = float.MaxValue;
            foreach (var o in Game.AI.Living())
                if (Plain(o) && !_used.Contains(o)) { float d = Vector3.Distance(o.transform.position, t.transform.position); if (d < bd) { bd = d; b = o; } }
            if (b)
            {
                _used.Add(b);
                Vampire.GiveOrder(t, ThrallOrder.Strike, b.transform.position, b, null);
                yield return WaitFor(() => !b.IsAlive || t.State != NpcState.Thrall, 30f);
                Log("control.thrall.strike", !b.IsAlive, $"{b.Id} {b.State} ({bd:0} m from the thrall)");
            }
            else _lines.Add("SKIP control.thrall.strike: no strike target");
            P.SelectThrall(null);
            bool back = P.SelectedThrall == null && Game.Cam && Game.Cam.Follow == P.transform;
            Log("control.thrall.back", back, "control back with Ilse=" + back);
            if (t.State == NpcState.Thrall) t.ReleaseFromThrall(false);
        }

        /// <summary>Pushing into a climbable wall for a moment takes her up it; she ends on top, back under the keys.</summary>
        IEnumerator ControlClimb()
        {
            var nav = Game.Level.Nav;
            NavLink link = null;
            int mask = P.AreaMask();
            foreach (var l in nav.Links)
            {
                if (l.Agent != NavAreas.VampireAgent || (l.Kind != LinkKind.Climb && l.Kind != LinkKind.ClimbAny && l.Kind != LinkKind.Ladder)) continue;
                if ((mask & (1 << NavBuilder.AreaFor(l.Kind))) == 0 || l.End.y - l.Start.y < 2f) continue;
                link = l; break;
            }
            if (link == null) { _lines.Add("SKIP control.climb: no climb up here"); yield break; }
            var span = link.End - link.Start; span.y = 0f;
            var head = span.sqrMagnitude > 0.04f ? span.normalized : Vector3.forward;
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            var at = NavMesh.SamplePosition(link.Start - head * 0.5f, out var h, 1f, filter) ? h.position : link.Start;
            P.Teleport(at);
            yield return null;
            P.DebugMove = head;
            yield return WaitFor(() => P.Busy, 2f);
            bool started = P.Busy;
            yield return WaitFor(() => !P.Busy, 8f);
            P.DebugMove = null;
            yield return Wait(0.2f);
            float rose = P.Feet.y - at.y;
            Log("control.climb", started && rose > 1.5f && !P.Busy, $"{link.Kind} started={started}, rose {rose:0.0} m");
        }

        /// <summary>Terror: a human who is spotting her while she feeds flees instead of raising the alarm.
        /// Perception is switched back on for this one (NoTarget off, god mode still on).</summary>
        IEnumerator Terror()
        {
            Npc w = null, v = null;
            foreach (var a in Game.AI.Living())
            {
                if (!Plain(a) || _used.Contains(a) || a.Arch.Faction == Faction.Vigil) continue;
                foreach (var b in Game.AI.Living())
                {
                    if (b == a || !Plain(b) || _used.Contains(b)) continue;
                    float d = Vector3.Distance(a.transform.position, b.transform.position);
                    // a witness who can actually see her there: within near range, or the spot is lit
                    if (d > 1.2f && d < 8f && StandBehind(b, 1f, out var spot)
                        && (Util.FlatDistance(a.transform.position, spot) < a.Vision.NearRange * 0.9f || Game.Lights.LightAt(spot) >= a.Vision.LitThreshold))
                    { w = a; v = b; break; }
                }
                if (w) break;
            }
            if (!w) { _lines.Add("SKIP predator.terror: no pair of plain humans here"); yield break; }
            _used.Add(w); _used.Add(v);
            StandBehind(v, 1f, out var at);
            P.Teleport(at);
            yield return null;
            Vampire.NoTarget = false;
            // Gorge and Stalker cut a sip to 0.67 s, shorter than a witness's sight tick in the dark
            var c = Game.Campaign;
            c.Nodes.Remove("predator.gorge"); c.Nodes.Remove("predator.stalker");
            P.BeginFeed(v, false, true);
            c.Nodes.Add("predator.gorge"); c.Nodes.Add("predator.stalker");
            bool saw = false;
            float t = 0f;
            while (P.Feeding != null && t < 6f && w.State != NpcState.Panicked)
            {
                var to = (P.Feet - w.transform.position).Flat();
                if (to.sqrMagnitude > 0.01f) w.transform.rotation = Quaternion.LookRotation(to);
                w.Detection = Mathf.Max(w.Detection, 0.5f);
                saw |= w.SeesPlayer;
                t += Time.deltaTime;
                yield return null;
            }
            Vampire.NoTarget = true;
            Log("predator.terror", saw && w.State == NpcState.Panicked, $"witness saw={saw}, {w.State} (fed {t:0.0} s, {Vector3.Distance(w.transform.position, P.Feet):0.0} m, light {P.Light:0.00}, could see {w.CouldSee(P.Feet, Mathf.Max(P.Light, 0.5f))})");
        }

        /// <summary>Lingering Dark: a lamp she smothered cannot be relit for a minute; without it a lamplighter relights it.</summary>
        IEnumerator Lingering()
        {
            // a real lamplighter (only they are sent to relight) and the nearest lit lamp they can walk to
            var n = Target(3f, x => x.Arch.Has(ArchFlags.Relights));
            GameLight l = null; float bd = float.MaxValue;
            var path = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask };
            if (n)
                foreach (var x in Game.Lights.All)
                {
                    if (!x || !x.CanSmother || !x.On) continue;
                    float d = Vector3.Distance(x.transform.position, n.transform.position);
                    if (d >= bd || !NavMesh.SamplePosition(x.transform.position, out var hit, 2.5f, filter)) continue;
                    if (!NavMesh.CalculatePath(n.transform.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    bd = d; l = x;
                }
            if (!l || !n) { _lines.Add($"SKIP shade.smother_lingering: no lamplighter with a lamp in reach (light={l != null}, lamplighter={n != null})"); yield break; }
            Cast("shade.smother", null, null, l);
            P.Teleport(P.Feet - (l.transform.position - P.Feet).normalized * 8f); // out of the way: she is not what this tests
            n.BeginRelight(l);
            float near = float.MaxValue, walk = 0f;
            while (n.State == NpcState.Relighting && near > 1.7f && walk < 40f)
            {
                near = Util.FlatDistance(n.transform.position, l.transform.position);
                walk += Time.deltaTime;
                yield return null;
            }
            yield return WaitFor(() => n.State != NpcState.Relighting, 40f);
            Log("shade.smother_lingering", !l.On && near <= 1.7f, $"{l.name} on={l.On}, {n.Id} {n.State}, reached={near <= 1.7f}");

            Game.Campaign.Nodes.Remove("shade.smother_lingering");
            n.BeginRelight(l);
            yield return WaitFor(() => n.State != NpcState.Relighting, 40f);
            Log("relight", l.On, $"{l.name} on={l.On}, {n.Id} {n.State} {Util.FlatDistance(n.transform.position, l.transform.position):0.0}m from it, broken={l.Broken}");
            Game.Campaign.Nodes.Add("shade.smother_lingering");
            l.SetOn(true);
        }

        /// <summary>False Trail: a rune lures calm hounds (missions with hounds only).</summary>
        IEnumerator FalseTrail()
        {
            Npc h = null;
            foreach (var o in Game.AI.Living())
                if (o.Arch.Has(ArchFlags.Smell) && o.State == NpcState.Relaxed && !o.Friendly) { h = o; break; }
            if (!h) { _lines.Add("SKIP sanguis.false_trail: no calm hound here"); yield break; }
            var at = h.transform.position + h.Forward * 6f;
            if (NavMesh.SamplePosition(at, out var hit, 2f, new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask })) at = hit.position;
            P.Teleport(at + Vector3.right * 2f);
            yield return null;
            bool cast = Cast("sanguis.snare", null, at);
            yield return WaitFor(() => h.State == NpcState.Distracted, 4f);
            Log("sanguis.false_trail", cast && h.State == NpcState.Distracted, $"{h.Id} {h.State}");
        }

        /// <summary>A priest's aura (6 m) suppresses Shade/Dominion/Sanguis but not Predator, and does not burn (K6/D57).</summary>
        IEnumerator HolyGround()
        {
            Npc pr = null;
            foreach (var o in Game.AI.Living()) if (o.Arch.Has(ArchFlags.HolyAura) && !o.Incapacitated) { pr = o; break; }
            if (!pr) { _lines.Add("SKIP holy_aura: no priest here"); yield break; }
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(pr.transform.position - pr.Forward * 3f, out var h, 1.5f, filter)) { _lines.Add("SKIP holy_aura: no ground by the priest"); yield break; }
            P.Teleport(h.position);
            yield return null;
            Vampire.GodMode = false;
            P.HP = P.MaxHP;
            bool mist = P.CanCast(Skills.Ability("shade.mist"), out var why);
            bool pounce = P.CanCast(Skills.Ability("predator.pounce"), out _);
            yield return Wait(1f);
            float hp = P.HP;
            Vampire.GodMode = true;
            float burn = Game.Lights.BurnAt(P.Feet);
            Log("holy_aura", !mist && why == "Holy ground" && pounce && (burn > 0f || hp >= P.MaxHP - 0.01f),
                $"mist {(mist ? "allowed" : why)}, pounce allowed={pounce}, hp {hp:0.0}/{P.MaxHP:0} (light burn {burn:0.00})");
            P.HP = P.MaxHP;
        }

        /// <summary>A8: roof-leaps open at Awakening 3 (or Bound), any-wall climbing at 4, and the agent actually uses
        /// the new mask.</summary>
        IEnumerator AwakeningGates()
        {
            var c = Game.Campaign;
            int vitae = c.Vitae;
            bool bound = c.Nodes.Remove("predator.bound");
            var sb = new StringBuilder();
            bool ok = true;
            for (int lvl = 1; lvl <= 5; lvl++)
            {
                c.Vitae = lvl == 1 ? 0 : CampaignState.VitaeThresholds[lvl - 1];
                int m = P.AreaMask();
                bool leap = (m & (1 << NavAreas.Leap)) != 0, any = (m & (1 << NavAreas.ClimbAny)) != 0;
                ok &= P.Awakening == lvl && leap == (lvl >= 3) && any == (lvl >= 4);
                sb.Append($"L{lvl}:{(leap ? "leap" : "-")}/{(any ? "wall" : "-")} ");
            }
            yield return null;   // TickMovement copies the mask to the agent every frame
            bool applied = (P.Agent.areaMask & (1 << NavAreas.ClimbAny)) != 0;
            c.Vitae = vitae;
            if (bound) c.Nodes.Add("predator.bound");
            Log("awakening.gates", ok && applied, sb + $"agent mask applied={applied}");
        }

        // ------------------------------------------------------------------ part 6: flag mods (X7, K9)

        static readonly System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        /// <summary>Pounce makes a Body noise where Ilse lands, unless Soft Landing is taken.</summary>
        IEnumerator SoftLanding()
        {
            int body = 0;
            void On(Vector3 p, float r, string tag) { if (tag == nameof(NoiseKind.Body)) body++; }
            GameEvents.NoiseMade += On;
            var counts = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) Game.Campaign.Nodes.Remove("predator.pounce_silent"); else Game.Campaign.Nodes.Add("predator.pounce_silent");
                P.CancelAll(); ClearCooldowns(); P.Blood = P.MaxBlood;
                var n = Target(5f);
                if (!n) { GameEvents.NoiseMade -= On; Log("predator.pounce_silent", false, "no target"); yield break; }
                body = 0;
                Cast("predator.pounce", n);
                yield return WaitFor(() => P.Feeding == n, 2f);
                counts[pass] = body;
                P.CancelAll();
                yield return Wait(0.2f);
            }
            GameEvents.NoiseMade -= On;
            Log("predator.pounce_silent", counts[0] > 0 && counts[1] == 0, $"body noises: without {counts[0]}, with {counts[1]}");
        }

        /// <summary>Awakening 7: finishing a jump beside the human a pending Feed is aimed at starts the drain at
        /// once. Drives EndLink with a synthetic jump that lands 1.5 m behind them.</summary>
        IEnumerator DropFeed()
        {
            var c = Game.Campaign;
            int vitae = c.Vitae;
            var results = new bool[2];
            for (int pass = 0; pass < 2; pass++)
            {
                c.Vitae = pass == 0 ? CampaignState.VitaeThresholds[5] : CampaignState.VitaeThresholds[6];
                P.CancelAll();
                var n = Target(1.5f);
                if (!n) { c.Vitae = vitae; Log("awakening.drop_feed", false, "no target"); yield break; }
                var end = P.Feet;
                var vt = typeof(Vampire);
                vt.GetField("_pending", Priv).SetValue(P, new PlayerAction { Kind = ActionKind.Feed, Npc = n, Point = n.transform.position });
                vt.GetField("_linkKind", Priv).SetValue(P, Vespertine.Level.LinkKind.Jump);
                vt.GetField("_linkPts", Priv).SetValue(P, new[] { end + Vector3.up * 3f, end });
                vt.GetMethod("EndLink", Priv).Invoke(P, null);
                yield return null;
                results[pass] = P.Feeding == n;
                yield return Wait(0.4f);   // below Awakening 7 the held feed resolves as an ordinary one: let its lunge land first
                P.CancelAll();
                yield return Wait(0.2f);
            }
            int awk = P.Awakening;
            c.Vitae = vitae;
            Log("awakening.drop_feed", !results[0] && results[1], $"feed on landing: awakening 6 {results[0]}, awakening 7 {results[1]}");
        }

        /// <summary>Herding: a panicked human re-aims their flight directly away from Ilse, whatever scared them.</summary>
        IEnumerator Herding()
        {
            var tf = typeof(Npc).GetField("_target", Priv);
            if (tf == null) { Log("predator.herd", false, "Npc._target not found"); yield break; }
            var res = new bool[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) Game.Campaign.Nodes.Remove("predator.herd"); else Game.Campaign.Nodes.Add("predator.herd");
                var n = Target(6f);
                if (!n) { Log("predator.herd", false, "no target"); yield break; }
                var decoy = n.transform.position + (n.transform.position - P.Feet).Flat().normalized * 20f;
                n.EnterPanic(decoy, true);
                yield return WaitFor(() => Util.FlatDistance((Vector3)tf.GetValue(n), P.transform.position) < 0.5f, 4f);
                res[pass] = n.State == NpcState.Panicked && Util.FlatDistance((Vector3)tf.GetValue(n), P.transform.position) < 0.5f;
            }
            Log("predator.herd", !res[0] && res[1], $"flees from Ilse: without {res[0]}, with {res[1]}");
        }

        /// <summary>Black Main: smothering one gas lamp puts out every lamp on its main, and only those.</summary>
        IEnumerator BlackMain()
        {
            var groups = new Dictionary<string, List<GameLight>>();
            foreach (var l in Game.Lights.All)
                if (l && l.Kind == LightKind.GasLamp && l.CanSmother && l.On && !string.IsNullOrEmpty(l.LightGroup))
                { if (!groups.TryGetValue(l.LightGroup, out var g)) groups[l.LightGroup] = g = new List<GameLight>(); g.Add(l); }
            List<GameLight> main = null;
            foreach (var g in groups.Values) if (g.Count >= 2 && (main == null || g.Count > main.Count)) main = g;
            if (main == null) { Log("shade.smother_chain", false, "no gas main with two lamps"); yield break; }
            var others = new List<GameLight>();
            foreach (var l in Game.Lights.All) if (l && l.On && !main.Contains(l)) others.Add(l);

            Game.Campaign.Nodes.Remove("shade.smother_chain");
            Cast("shade.smother", null, null, main[0]);
            yield return null;
            int offWithout = 0; foreach (var l in main) if (!l.On) offWithout++;
            foreach (var l in main) l.SetOn(true);
            ClearCooldowns(); P.Blood = P.MaxBlood;

            Game.Campaign.Nodes.Add("shade.smother_chain");
            bool cast = Cast("shade.smother", null, null, main[0]);
            yield return null;
            int offWith = 0; foreach (var l in main) if (!l.On) offWith++;
            int collateral = 0; foreach (var l in others) if (!l.On) collateral++;
            foreach (var l in main) l.SetOn(true);
            Log("shade.smother_chain", cast && offWithout == 1 && offWith == main.Count && collateral == 0,
                $"main '{main[0].LightGroup}' of {main.Count}: without {offWithout} out, with {offWith} out, others out {collateral}");
        }

        /// <summary>Between Bars: a dash into bars stops at them without the node and carries her through with it.</summary>
        IEnumerator BetweenBars()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas };
            foreach (var col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.gameObject.layer != Layers.Bars || !col.enabled || col.isTrigger) continue;
                var b = col.bounds;
                var thin = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                float half = Vector3.Scale(b.extents, thin).magnitude;
                var c = new Vector3(b.center.x, b.min.y, b.center.z);
                if (!NavMesh.SamplePosition(c - thin * (half + 1.4f), out var from, 0.6f, filter)) continue;
                if (!NavMesh.SamplePosition(c + thin * (half + 1.4f), out var to, 0.6f, filter)) continue;
                if (Mathf.Abs(from.position.y - to.position.y) > 0.4f) continue;
                var eye = from.position + Vector3.up * 1.6f; var tgt = to.position + Vector3.up * 0.5f;
                if (Physics.Linecast(eye, tgt, Layers.WallMask, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Linecast(eye, tgt, 1 << Layers.Bars, QueryTriggerInteraction.Ignore)) continue;
                var side = new bool[2];
                for (int k = 0; k < 2; k++)
                {
                    if (k == 0) Game.Campaign.Nodes.Remove("shade.dash_bars"); else Game.Campaign.Nodes.Add("shade.dash_bars");
                    P.Teleport(from.position);
                    yield return null;
                    P.DevRefillDash();
                    P.transform.rotation = Quaternion.LookRotation(thin);
                    P.DebugDash = true;
                    yield return Wait(1.2f);
                    side[k] = Vector3.Dot(P.Feet - c, thin) > 0f;
                }
                Log("shade.dash_bars", !side[0] && side[1], $"through '{col.name}': without {side[0]}, with {side[1]}");
                yield break;
            }
            Log("shade.dash_bars", false, "no barred gap with floor on both sides");
        }

        IEnumerator Vessel()
        {
            Game.Campaign.Nodes.Remove("sanguis.vessel");
            float without = P.MaxBlood;
            Game.Campaign.Nodes.Add("sanguis.vessel");
            float with = P.MaxBlood;
            Log("sanguis.vessel", Mathf.Approximately(with - without, 50f), $"max blood {without:0} -> {with:0}");
            yield break;
        }
    }
}
#endif
