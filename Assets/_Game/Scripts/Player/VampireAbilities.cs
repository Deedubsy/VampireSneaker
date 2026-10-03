using System.Collections.Generic;
using UnityEngine;
using Pose = Vespertine.Visual.Pose;
using UnityEngine.AI;
using Vespertine.Abilities;
using Vespertine.AI;
using Vespertine.Core;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Stealth;
using Vespertine.Visual;

namespace Vespertine.Player
{
    public partial class Vampire
    {
        // ---------------------------------------------------------------- ability state
        readonly Dictionary<string, float> _cd = new Dictionary<string, float>();
        float _apexUntil;
        public bool ApexActive => Time.time < _apexUntil;
        public bool Sensing;
        readonly List<SnareRune> _runes = new List<SnareRune>();

        // aiming (targeted abilities and thrall commands)
        public AbilityDef Aiming;
        public int AimStage;
        public Npc AimNpc;
        public Npc AimThrall;
        public string AimError;
        public Vector3 AimFrom => AimThrall ? AimThrall.transform.position : Feet;

        public float CooldownLeft(string id) => _cd.TryGetValue(id, out var t) ? Mathf.Max(0f, t - Time.time) : 0f;

        public float CostOf(AbilityDef a)
        {
            float c = a.Cost * Difficulties.Current.AbilityCost;
            if (Humour == "scalding" && a.Id.StartsWith("sanguis.")) c *= 0.5f;
            if (Humour == "lucid" && a.Id.StartsWith("dominion.")) c *= 0.5f;
            return Mathf.Round(c);
        }

        /// <summary>How far a priest's holy aura reaches (drawn on the ground by <see cref="Visual.AuraRenderer"/>).</summary>
        public const float HolyAuraRadius = 6f;

        public bool HolyBlocked(AbilityDef a)
        {
            if (!a.Holy) return false;
            if (a.Id.StartsWith("sanguis.") && Has("sanguis.silverblood")) return false;
            if (Game.Lights != null && Game.Lights.BurnAt(Feet) > 0f) return true;
            if (Game.AI != null)
                foreach (var n in Game.AI.Living())
                    if (n.Arch.Has(ArchFlags.HolyAura) && !n.Incapacitated && Vector3.Distance(n.transform.position, Feet) < HolyAuraRadius) return true;
            return false;
        }

        public bool CanCast(AbilityDef a, out string why)
        {
            why = null;
            if (a == null) { why = "Empty"; return false; }
            if (Dead) { why = "Dead"; return false; }
            if (!Has(a.Id)) { why = "Not learned"; return false; }
            if (CooldownLeft(a.Id) > 0f) { why = "Recovering"; return false; }
            if (Blood < CostOf(a) || (a.Upkeep > 0f && Blood < 1f)) { why = "Not enough blood"; return false; }
            if (HolyBlocked(a)) { why = "Holy ground"; return false; }
            if (InMist && a.Id != "shade.mist" && a.Id != "sanguis.sense") { why = "Not as mist"; return false; }
            if (Carrying != null && (a.Id == "predator.pounce" || a.Id == "predator.rend" || a.Id == "dominion.thrall" || a.Id == "shade.mist")) { why = "Hands full"; return false; }
            if (Concealed && a.Targeting != Targeting.Self && a.Targeting != Targeting.Hold) { why = "Hidden"; return false; }
            return true;
        }

        public AbilityDef SlotAbility(int i)
        {
            var lo = Game.Campaign?.Loadout;
            return lo != null && i < lo.Count ? Skills.Ability(lo[i]) : null;
        }

        void PressAbilitySlot(int i)
        {
            var a = SlotAbility(i);
            if (a == null) return;
            if (!CanCast(a, out var why)) { Say($"{a.Name}: {why}"); Game.Audio?.Play2D("ui_error", 0.5f); return; }
            BeginAbility(a);
        }

        public void BeginAbility(AbilityDef a)
        {
            switch (a.Targeting)
            {
                case Targeting.Self:
                    Cast(a, new PlayerAction { Kind = ActionKind.Ability, Ability = a.Id }); return;
                case Targeting.Toggle:
                    if (a.Id == "shade.mist") { if (InMist) SetMist(false); else Cast(a, null); } return;
                case Targeting.Hold: return; // handled while held
                default:
                    Aiming = a;
                    AimStage = 0;
                    AimNpc = null;
                    AimThrall = null;
                    Game.Audio?.Play2D("ui_click", 0.4f);
                    return;
            }
        }

        public void CancelAim() { Aiming = null; AimStage = 0; AimNpc = null; AimThrall = null; AimError = null; }

        /// <summary>While aiming, consumes clicks. Returns true when input was handled.</summary>
        bool TickAimInput()
        {
            if (Aiming == null) return false;
            var inp = Game.Input;
            AimError = ValidateAim(out _, out _, out _);
            if (inp.Cancel.WasPressedThisFrame()) { CancelAim(); return true; }
            for (int i = 0; i < inp.Abilities.Length; i++)
                if (inp.Abilities[i].WasPressedThisFrame()) { var a = SlotAbility(i); if (a == Aiming && AimThrall == null) { CancelAim(); return true; } }
            if (inp.Click.WasPressedThisFrame() && (Game.UI == null || !Game.UI.PointerOverUI))
            {
                var err = ValidateAim(out var npc, out var light, out var point);
                if (err != null) { Say(err); Game.Audio?.Play2D("ui_error", 0.5f); return true; }
                ConfirmAim(npc, light, point);
            }
            return true;
        }

        /// <summary>Checks the current hover against the ability being aimed, including range from where she stands
        /// (she will not walk there: P7). Returns an error or null.</summary>
        public string ValidateAim(out Npc npc, out GameLight light, out Vector3 point)
        {
            var err = ValidateAimTarget(out npc, out light, out point);
            if (err != null || AimStage == 1 || Aiming == null || (Aiming.ThrallCommand && AimThrall)) return err;
            return RangeProblem(Aiming, new PlayerAction { Kind = ActionKind.Ability, Ability = Aiming.Id, Npc = npc, Light = light, Point = point });
        }

        string ValidateAimTarget(out Npc npc, out GameLight light, out Vector3 point)
        {
            npc = null; light = null; point = HoverPoint;
            var a = Aiming;
            if (a == null) return "";
            bool secondStage = AimStage == 1;
            if (secondStage)
            {
                if (!HoverValid) return "Choose a place";
                var origin = AimNpc ? AimNpc.transform.position : Feet;
                float max = a.Id == "dominion.beckon" ? 14f : 30f;
                if (Util.FlatDistance(origin, HoverPoint) > max) return "Too far from them";
                if (!NavMesh.SamplePosition(HoverPoint, out var h, 1.5f, new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavAreas.HumanMask })) return "They cannot walk there";
                point = h.position;
                return null;
            }
            switch (a.Targeting)
            {
                case Targeting.Npc:
                {
                    var n = HoverNpc;
                    if (n == null) return "Choose a human";
                    if (n.Rescue || n.IsUndead) return "Not them";
                    npc = n;
                    if (a.ThrallCommand)
                    {
                        if (!n.IsAlive || n == AimThrall || n.State == NpcState.Thrall) return "Choose another human";
                        if (a.Id == "dominion.false_orders" && AimThrall && n.Arch.Faction != AimThrall.Arch.Faction) return "They would not take orders from this one";
                        if (a.Id == "dominion.false_orders" && n.Arch.Faction == Faction.None) return "No one gives them orders";
                        if (a.Id == "dominion.false_orders" && AimThrall && AimThrall.IsCorpsePuppet && !Has("dominion.living_lie")) return "The dead cannot give orders (Living Lie)";
                        return null;
                    }
                    if (!n.IsAlive) return "Already dead";
                    if (n.State == NpcState.Thrall) return "Already yours";
                    if (a.Dominion && n.Immune(out var why)) return why;
                    if (a.Dominion && n.Arch.Has(ArchFlags.Quadruped) && a.Id != "dominion.mesmerize") return "Beasts do not listen";
                    if (a.Id == "predator.rend" && n.Incapacitated) return "Feed instead";
                    if (a.Id == "dominion.thrall" && !(n.Helpless || (!n.Aware && n.Detection < 0.35f))) return "They must be helpless or unaware";
                    if (a.Id == "predator.pounce" && n.Arch.Has(ArchFlags.Quadruped) && !n.Helpless) return "Too wary";
                    if (a.Id == "predator.pounce" && n.Arch.Has(ArchFlags.Armored) && n.AngleTo(Feet) < 100f) return "Armoured - pounce from behind";
                    return null;
                }
                case Targeting.Corpse:
                {
                    var n = HoverNpc;
                    if (n == null || n.State != NpcState.Dead || n.Carried || n.Disposed) return "Choose a corpse";
                    if (n.IsCorpsePuppet) return "Already risen";
                    if (n.Arch.Has(ArchFlags.Quadruped)) return "Not a beast";
                    npc = n;
                    return null;
                }
                case Targeting.Light:
                {
                    var l = HoverLight;
                    if (l == null) return "Choose a light";
                    if (!l.CanSmother) return l.Kind == LightKind.Sunstone ? "Sunstone cannot be smothered" : "It cannot be put out";
                    light = l;
                    point = l.transform.position;
                    return null;
                }
                case Targeting.Point:
                {
                    if (!HoverValid) return "Choose a place";
                    point = HoverPoint;
                    if (a.Id == "sanguis.snare" && !NavMesh.SamplePosition(point, out _, 1f, new NavMeshQueryFilter { agentTypeID = NavAreas.HumanAgent, areaMask = NavMesh.AllAreas })) return "No one walks there";
                    return null;
                }
            }
            return null;
        }

        void ConfirmAim(Npc npc, GameLight light, Vector3 point)
        {
            var a = Aiming;
            if (AimStage == 0 && (a.Id == "dominion.beckon" || a.Id == "dominion.false_orders"))
            {
                AimNpc = npc;
                AimStage = 1;
                Game.Audio?.Play2D("ui_click", 0.4f);
                return;
            }
            var thrall = AimThrall;
            var target = AimStage == 1 ? AimNpc : npc;
            CancelAim();
            if (a.ThrallCommand && thrall)
            {
                if (a.Id == "dominion.false_orders") IssueThrall(thrall, ThrallOrder.FalseOrders, point, target, null);
                else if (a.Id == "dominion.puppet_strike") IssueThrall(thrall, ThrallOrder.Strike, point, target, null);
                Game.Audio?.Play2D("whisper", 0.5f);
                Game.Campaign?.AddHabit(Progression.Habits.Dominion, 0.5f);
                return;
            }
            bool drain = Game.Input != null && Game.Input.Drain.IsPressed();
            Act(new PlayerAction { Kind = ActionKind.Ability, Ability = a.Id, Npc = target, Light = light, Point = point, Drain = drain, Rush = Rushy });
        }

        bool CrossesWater(Vector3 a, Vector3 b)
        {
            if (Game.Level == null) return false;
            float d = Util.FlatDistance(a, b);
            for (float t = 0.5f; t < d; t += 0.75f)
                if (Game.Level.IsCanal(Vector3.Lerp(a, b, t / d))) return true;
            return false;
        }

        // ---------------------------------------------------------------- range: cast now or say why (no auto-walk, P7)
        /// <summary>Why the ability cannot reach its target from where she stands, or null.</summary>
        public string RangeProblem(AbilityDef a, PlayerAction act)
        {
            Vector3 tp;
            if (act.Npc) tp = act.Npc.transform.position;
            else if (act.Light) tp = act.Light.transform.position;
            else tp = act.Point;
            float range = a.Range;
            if (range <= 0f) return null;
            if (a.Id == "predator.pounce" && Feet.y - tp.y >= 3f) range = 12f;
            float dist = a.Targeting == Targeting.Light && act.Light ? Vector3.Distance(Feet + Vector3.up * 1.5f, act.Light.SourcePos) : Vector3.Distance(Feet, tp);
            if (dist > range) return $"Too far ({dist:0.0} m, reach {range:0} m)";
            bool needLos = a.Id == "shade.smother" || a.Id == "dominion.mesmerize" || a.Id == "sanguis.hemorrhage" || a.Id == "predator.pounce";
            if (needLos && !HasLos(a, act)) return "No line of sight";
            return null;
        }

        bool ResolveAbility(PlayerAction act)
        {
            var a = Skills.Ability(act.Ability);
            if (a == null) return false;
            if (act.Npc && (act.Npc.Disposed || act.Npc.Carried)) return false;
            var why = RangeProblem(a, act);
            if (why != null) { Say($"{a.Name}: {why}"); return false; }
            Stop();
            return Cast(a, act);
        }

        bool HasLos(AbilityDef a, PlayerAction act)
        {
            var eye = Feet + Vector3.up * 1.6f;
            Vector3 to = act.Light ? act.Light.SourcePos : act.Npc ? act.Npc.transform.position + Vector3.up * 1.4f : act.Point + Vector3.up * 0.5f;
            int mask = Layers.SightMask;
            if (act.Light)
            {
                // the lamp's own housing can sit inside a wall face; stop just short of it
                var dir = to - eye;
                to = eye + dir * Mathf.Max(0f, 1f - 0.45f / Mathf.Max(0.01f, dir.magnitude));
            }
            return !Physics.Linecast(eye, to, mask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- casting
        void Pay(AbilityDef a)
        {
            Blood = Mathf.Max(0f, Blood - CostOf(a));
            if (a.Cooldown > 0f) _cd[a.Id] = Time.time + a.Cooldown;
            GameEvents.RaiseAbilityUsed(a.Id);
            var c = Game.Campaign;
            if (c == null) return;
            if (a.Id.StartsWith("dominion.")) c.AddHabit(Progression.Habits.Dominion, 1f);
            else if (a.Id == "shade.mist") c.AddHabit(Progression.Habits.Mist, 1f);
            else if (a.Id == "shade.smother" || a.Id == "shade.eclipse") c.AddHabit(Progression.Habits.Snuff, 1f);
            else if (a.Id == "sanguis.snare" || a.Id == "sanguis.hemorrhage" || a.Id == "sanguis.puppet" || a.Id == "sanguis.communion") c.AddHabit(Progression.Habits.Blood, 1f);
        }

        public bool Cast(AbilityDef a, PlayerAction act)
        {
            if (!CanCast(a, out var why)) { Say($"{a.Name}: {why}"); return false; }
            var n = act?.Npc;
            var feet = Feet;
            switch (a.Id)
            {
                case "predator.pounce":
                {
                    if (n == null || !n.IsAlive || n.State == NpcState.Victim) return false;
                    var land = n.transform.position - n.Forward * 0.6f;
                    if (NavMesh.SamplePosition(land, out var lh, 1f, new NavMeshQueryFilter { agentTypeID = NavAreas.VampireAgent, areaMask = NavMesh.AllAreas })) land = lh.position;
                    else land = n.transform.position;
                    // aim already checks; Cast checks again because quick-cast and thrall paths skip aiming (P6)
                    if (CrossesWater(feet, land)) { Say("Running water."); return false; }
                    n.Snare(1.2f);
                    Pay(a);
                    bool drain = act.Drain;
                    if (!Has("predator.pounce_silent")) Game.Noise?.Emit(land, 4f, NoiseKind.Body, this);
                    ManualLeap(land, 0.45f, 1.4f, () => BeginFeed(n, drain, true));
                    return true;
                }
                case "predator.rend":
                {
                    if (n == null || !n.IsAlive) return false;
                    Pay(a);
                    FaceTowards(n.transform.position);
                    Channel(0.25f, Pose.Feed, () =>
                    {
                        if (!n || !n.IsAlive) return;
                        Fx.BloodBurst(n.transform.position + Vector3.up * 1.3f, 1.3f);
                        Game.Audio?.PlayAt("growl", feet, 0.8f);
                        n.TakeHit(99, "rend", Feet);
                        Game.Noise?.Emit(Feet, 8f, NoiseKind.Body, this);
                        Game.Campaign?.AddHabit(Progression.Habits.Lethal, 1f);
                        if (ApexActive) AddBlood(15f);
                    });
                    return true;
                }
                case "predator.apex":
                    Pay(a);
                    _apexUntil = Time.time + 8f;
                    Game.AI?.SlowHumans(0.35f, 8f);
                    AI.Squad.NotifyApex(feet);
                    Fx.Ring(feet, 20f, Mats.Pal.BloodBright, 1.2f);
                    Game.Audio?.Play2D("growl", 0.9f, 0.7f);
                    return true;

                case "shade.smother":
                {
                    var l = act.Light;
                    if (l == null || !l.CanSmother) return false;
                    Pay(a);
                    if (Has("shade.smother_chain") && l.Kind == LightKind.GasLamp && !string.IsNullOrEmpty(l.LightGroup) && Game.Lights != null)
                    {
                        foreach (var o in Game.Lights.All.ToArray())
                            if (o.On && o.LightGroup == l.LightGroup && o.CanSmother) { o.SetOn(false, true); Fx.Smoke(o.SourcePos, new Color(0.15f, 0.15f, 0.2f, 0.7f), 0.8f, 1.6f); }
                        Game.UI?.Toast("The gas main dies.");
                    }
                    else l.SetOn(false, true);
                    Fx.Smoke(l.SourcePos, new Color(0.12f, 0.12f, 0.18f, 0.8f), 1f, 1.6f);
                    return true;
                }
                case "shade.gloom":
                {
                    Pay(a);
                    var p = act.Point;
                    GloomSphere.Create(p, a.Radius, 15f, Has("shade.shroud"));
                    Game.Audio?.PlayAt("cast", p, 0.5f, 0.7f);
                    return true;
                }
                case "shade.mist":
                    Pay(a);
                    SetMist(true);
                    return true;
                case "shade.eclipse":
                {
                    Pay(a);
                    var dead = new List<GameLight>();
                    if (Game.Lights != null)
                        foreach (var l in Game.Lights.All.ToArray())
                            if (l.On && l.Kind != LightKind.Moon && Vector3.Distance(l.transform.position, feet) < 25f) { l.SetOn(false, true); dead.Add(l); }
                    EclipseTimer.Create(dead, 30f);
                    Game.AI?.AddBlindZone(feet, 25f, 8f);
                    Fx.Ring(feet, 50f, new Color(0.05f, 0.05f, 0.15f, 0.9f), 1.5f);
                    Game.Audio?.Play2D("cast", 1f, 0.5f);
                    return true;
                }

                case "dominion.beckon":
                    string bw = null;
                    if (n == null || n.Immune(out bw)) { Say(bw ?? "No one"); return false; }
                    if (!n.Beckon(act.Point)) { Say("They will not come."); return false; }
                    Pay(a);
                    Game.Audio?.PlayAt("whisper", n.transform.position, 0.6f);
                    Fx.Ring(act.Point, 1.2f, Mats.Pal.Dominion, 0.8f);
                    return true;
                case "dominion.mesmerize":
                    string mw = null;
                    if (n == null || n.Immune(out mw)) { Say(mw ?? "No one"); return false; }
                    if (!n.Mesmerise(10f)) { Say("Their mind slips away from her."); return false; }
                    Pay(a);
                    Game.Audio?.PlayAt("whisper", n.transform.position, 0.7f, 0.8f);
                    Fx.Ring(n.transform.position, 2f, Mats.Pal.Dominion, 0.6f);
                    return true;
                case "dominion.thrall":
                    if (n == null) return false;
                    if (Thralls.Count >= MaxThralls) { Say($"She can hold only {MaxThralls} thrall{(MaxThralls > 1 ? "s" : "")}."); return false; }
                    if (n.Immune(out var tw)) { Say(tw); return false; }
                    if (!n.MakeThrall()) { Say("Their will holds."); return false; }
                    Pay(a);
                    Thralls.Add(n);
                    Game.UI?.Toast($"{n.DisplayName} is hers. Click the portrait or press [{Controls.GameInput.Key(Game.Input?.CycleThrall)}] to take control.");
                    Game.Audio?.PlayAt("whisper", n.transform.position, 0.8f, 0.6f);
                    return true;
                case "dominion.court":
                {
                    Pay(a);
                    int count = 0;
                    if (Game.AI != null)
                        foreach (var o in new List<Npc>(Game.AI.Living()))
                            if (Vector3.Distance(o.transform.position, feet) < 10f && o.Mesmerise(8f)) count++;
                    Fx.Ring(feet, 20f, Mats.Pal.Dominion, 1.2f);
                    Game.Audio?.Play2D("whisper", 1f, 0.5f);
                    return true;
                }

                case "sanguis.mend":
                    Pay(a);
                    Heal(MaxHP * 0.5f);
                    Fx.Ring(feet, 2f, Mats.Pal.BloodBright, 0.6f);
                    Game.Audio?.PlayAt("feed", feet, 0.5f, 1.3f);
                    return true;
                case "sanguis.snare":
                {
                    Pay(a);
                    _runes.RemoveAll(r => !r);
                    while (_runes.Count >= 2) { var old = _runes[0]; _runes.RemoveAt(0); if (old) Destroy(old.gameObject); }
                    _runes.Add(SnareRune.Create(act.Point));
                    Game.Audio?.PlayAt("cast", act.Point, 0.4f, 1.2f);
                    return true;
                }
                case "sanguis.hemorrhage":
                    if (n == null || !n.Hemorrhage()) { Say("Their blood will not answer."); return false; }
                    Pay(a);
                    Game.Campaign?.AddHabit(Progression.Habits.Lethal, 1f);
                    Fx.BloodStream(n.transform.position + Vector3.up * 1.4f, n.transform.position);
                    return true;
                case "sanguis.puppet":
                    if (n == null || !n.RaisePuppet(40f)) { Say("The body will not rise."); return false; }
                    Pay(a);
                    Game.Audio?.PlayAt("cast", n.transform.position, 0.5f, 0.6f);
                    return true;
                case "sanguis.communion":
                {
                    int bodies = 0;
                    float gain = 0f;
                    if (Game.Level != null)
                        foreach (var o in new List<Npc>(Game.Level.All<Npc>()))
                        {
                            if (!o.gameObject.activeInHierarchy || o.Disposed || o.Carried || o.Hidden || o.Drained || o.IsCorpsePuppet) continue;
                            if (Vector3.Distance(o.transform.position, feet) > 12f) continue;
                            if (o.State == NpcState.Dead) { gain += 10f; o.Drained = true; bodies++; }
                            else if (o.State == NpcState.Dazed) { gain += BloodValue(o.Arch.Blood); o.Drained = true; o.Die("drain"); bodies++; Game.Campaign?.AddHabit(Progression.Habits.Lethal, 1f); }
                            else continue;
                            Fx.BloodStream(o.transform.position + Vector3.up * 0.3f, feet + Vector3.up * 1.5f);
                        }
                    if (bodies == 0) { Say("No blood to call."); return false; }
                    Pay(a);
                    AddBlood(gain * Difficulties.Current.BloodGain);
                    Game.Campaign?.AddVitae(Mathf.RoundToInt(gain));
                    Fx.Ring(feet, 24f, Mats.Pal.Blood, 1.4f);
                    Game.Audio?.Play2D("feed", 1f, 0.6f);
                    return true;
                }
            }
            return false;
        }

        // A leap outside of the navmesh link system (Pounce). The agent stops driving the transform until landing.
        System.Action _afterLeap;
        void ManualLeap(Vector3 to, float duration, float arc, System.Action after)
        {
            Stop();
            var from = Feet;
            _link = new NavLink { Kind = LinkKind.Leap, Start = from, End = to };
            _linkKind = LinkKind.Leap;
            _linkPts = new[] { from, to };
            _linkTimes = new[] { 0f, duration };
            _linkDur = duration;
            _linkArc = arc;
            _linkT = 0f;
            _linkPose = Pose.Run;
            _afterLeap = after;
            if (Agent) Agent.updatePosition = false;
            FaceTowards(to);
            Game.Audio?.PlayAt("swish", from, 0.6f);
        }

        void AfterManualLeap(Vector3 end)
        {
            if (Agent && !Agent.updatePosition)
            {
                Agent.Warp(end);
                Agent.updatePosition = true;
            }
            var a = _afterLeap;
            _afterLeap = null;
            a?.Invoke();
        }

        // ---------------------------------------------------------------- mist
        public void SetMist(bool on)
        {
            if (InMist == on) return;
            InMist = on;
            if (on)
            {
                if (Carrying != null) DropBody(false);
                if (Concealed) LeaveHideSpot();
                Rig?.SetVisible(false);
                Game.Audio?.PlayAt("mist", Feet, 0.6f);
                Fx.Smoke(Feet + Vector3.up, new Color(0.6f, 0.6f, 0.7f, 0.5f), 3f, 1.2f);
            }
            else
            {
                Rig?.SetVisible(!Concealed);
                Game.Audio?.PlayAt("mist", Feet, 0.4f, 1.4f);
                if (Agent && Agent.enabled && Agent.isOnNavMesh && Agent.hasPath)
                {
                    var dest = Agent.destination;
                    Agent.areaMask = AreaMask();
                    if (!GoTo(dest, false)) Stop();
                }
            }
        }

        // ---------------------------------------------------------------- per-frame upkeep
        void TickAbilities(float dt)
        {
            if (InMist)
            {
                Blood -= 3f * dt;
                if (Blood <= 0f) { Blood = 0f; SetMist(false); Say("Too little blood to hold the mist."); }
                else if (Game.AI != null)
                    foreach (var n in Game.AI.Living())
                        if (n.CarriesCenser && Vector3.Distance(n.transform.position, Feet) < Npc.CenserRadius)
                        { SetMist(false); Say("Garlic smoke forces her out of the mist!"); Damage(5f, false, n, true, false); break; }
            }

            bool want = false;
            if (Has("sanguis.sense") && Game.Input != null && SelectedThrall == null)
            {
                want = Game.Input.BloodSense.IsPressed();
                var lo = Game.Campaign?.Loadout;
                if (lo != null)
                    for (int i = 0; i < lo.Count && i < Game.Input.Abilities.Length; i++)
                        if (lo[i] == "sanguis.sense" && Game.Input.Abilities[i].IsPressed()) want = true;
            }
            if (want && !Sensing && HolyBlocked(Skills.Ability("sanguis.sense"))) { want = false; }
            if (want)
            {
                bool free = Humour == "fever" || (Has("sanguis.sense_free") && !Moving);
                if (!free) Blood -= 1f * dt;
                if (Blood <= 0f) { Blood = 0f; want = false; }
            }
            if (want && !Sensing) Game.Audio?.Play2D("heartbeat", 0.4f);
            Sensing = want;
        }

        // ---------------------------------------------------------------- thralls
        public readonly List<Npc> Thralls = new List<Npc>();
        public Npc SelectedThrall;
        public int MaxThralls => Has("dominion.court") ? 3 : Has("dominion.thrall_second") ? 2 : 1;

        public void ReleaseThrall(Npc n)
        {
            Thralls.Remove(n);
            if (SelectedThrall == n) SelectThrall(null);
        }

        /// <summary>Takes direct control of a thrall (null: back to Ilse). Whoever is let go holds where they stand;
        /// the camera goes with the one now under her hand.</summary>
        public void SelectThrall(Npc n)
        {
            if (n != null && (n.State != NpcState.Thrall || !Thralls.Contains(n))) n = null;
            var prev = SelectedThrall;
            if (prev) { prev.DirectMove = Vector3.zero; prev.DirectRun = false; }
            SelectedThrall = n;
            CancelAim();
            // Ilse waits while a thrall moves: a walk she was on stops, a feed or a climb finishes
            if (n != null && !Busy) { _pending = null; Stop(); }
            _moveIn = Vector3.zero;
            if (Game.Cam) Game.Cam.FollowTarget(n != null ? n.transform : transform);
            if (prev != n) Game.Audio?.Play2D("ui_click", 0.4f);
        }

        /// <summary>T: Ilse, then each thrall in turn, then Ilse again.</summary>
        void CycleThrall()
        {
            Thralls.RemoveAll(t => !t || t.State != NpcState.Thrall);
            if (Thralls.Count == 0) { Say("Ilse has no thralls."); return; }
            int i = SelectedThrall == null ? -1 : Thralls.IndexOf(SelectedThrall);
            SelectThrall(i + 1 < Thralls.Count ? Thralls[i + 1] : null);
        }

        /// <summary>X with Ilse in hand: every thrall falls in behind her, or (if all already do) holds.</summary>
        void ToggleFollowAll()
        {
            Thralls.RemoveAll(t => !t || t.State != NpcState.Thrall);
            if (Thralls.Count == 0) { Say("Ilse has no thralls."); return; }
            bool allFollow = Thralls.TrueForAll(t => t.Order == ThrallOrder.Follow);
            foreach (var t in Thralls) { if (allFollow) t.OrderHold(); else t.OrderFollow(); }
            Say(allFollow ? "Thralls: hold" : "Thralls: follow me");
        }

        /// <summary>Thrall command slots while a thrall is under her hand: 1 Distract, 2 False Orders, 3 Puppet Strike, 4 Release.</summary>
        public static readonly string[] ThrallSlots = { "Distract", "False Orders", "Puppet Strike", "Release" };

        public bool ThrallSlotAvailable(int i, out string why, Npc thrall = null)
        {
            why = null;
            var t = thrall ? thrall : SelectedThrall;
            if (i == 1 && !Has("dominion.false_orders")) { why = "Requires False Orders"; return false; }
            if (i == 1 && t && t.IsCorpsePuppet && !Has("dominion.living_lie")) { why = "The dead cannot give orders (Living Lie)"; return false; }
            if (i == 2 && !Has("dominion.puppet_strike")) { why = "Requires Puppet Strike"; return false; }
            return i >= 0 && i < ThrallSlots.Length;
        }

        /// <summary>Who a thrall would speak to: the human under the cursor, else the nearest one within a few steps of the thrall.</summary>
        public Npc ThrallTarget(Npc t)
        {
            if (HoverNpc != null && HoverNpc != t && HoverNpc.IsAlive && !HoverNpc.Incapacitated && HoverNpc.State != NpcState.Thrall) return HoverNpc;
            return Game.AI?.NearestNpc(t.transform.position, 3f, n => n != t && n.IsAlive && !n.Incapacitated && !n.Hidden && n.State != NpcState.Thrall && n.State != NpcState.Victim);
        }

        Interactable ThrallUseTarget(Npc t)
        {
            if (HoverUse != null && HoverUse.ThrallCan && HoverUse.Enabled) return HoverUse;
            Interactable best = null; float bd = 2.6f;
            if (Game.Level != null)
                foreach (var u in Game.Level.All<Interactable>())
                {
                    if (!u.Enabled || !u.ThrallCan || !u.ShowMarker || !u.gameObject.activeInHierarchy) continue;
                    float d = Vector3.Distance(u.transform.position, t.transform.position);
                    if (d < bd) { bd = d; best = u; }
                }
            return best;
        }

        void HandleThrallInput()
        {
            var t = SelectedThrall;
            if (!t || t.State != NpcState.Thrall) { SelectThrall(null); return; }
            var inp = Game.Input;
            // the thrall walks with the move keys (read in ReadMoveInput); the mouse uses things and picks who to speak to
            if (inp.Click.WasPressedThisFrame() && (Game.UI == null || !Game.UI.PointerOverUI))
            {
                if (HoverUse != null && HoverUse.ThrallCan && HoverUse.Enabled) { IssueThrall(t, ThrallOrder.Interact, HoverUse.transform.position, null, HoverUse); Fx.Ring(HoverUse.transform.position, 1f, Mats.Pal.Thrall, 0.4f); }
                else if (HoverNpc != null && HoverNpc != t && HoverNpc.IsAlive && HoverNpc.State != NpcState.Thrall) { IssueThrall(t, ThrallOrder.Distract, HoverNpc.transform.position, HoverNpc, null); Fx.Ring(HoverNpc.transform.position, 1f, Mats.Pal.Thrall, 0.4f); }
            }
            if (inp.Interact.WasPressedThisFrame())
            {
                var u = ThrallUseTarget(t);
                if (u != null) { IssueThrall(t, ThrallOrder.Interact, u.transform.position, null, u); Fx.Ring(u.transform.position, 1f, Mats.Pal.Thrall, 0.4f); }
                else Say(t.DisplayName + " finds nothing to use here.");
            }
            if (inp.ThrallFollow.WasPressedThisFrame())
            {
                if (t.Order == ThrallOrder.Follow) { t.OrderHold(); Say(t.DisplayName + " holds."); }
                else { IssueThrall(t, ThrallOrder.Follow, Feet, null, null); Say(t.DisplayName + " follows Ilse."); }
            }
            for (int i = 0; i < inp.Abilities.Length && i < ThrallSlots.Length; i++)
            {
                if (!inp.Abilities[i].WasPressedThisFrame()) continue;
                if (!ThrallSlotAvailable(i, out var why)) { Say(why); continue; }
                switch (i)
                {
                    case 0:
                    {
                        var n = ThrallTarget(t);
                        if (n == null) { Say("No one near " + t.DisplayName + " to distract."); break; }
                        IssueThrall(t, ThrallOrder.Distract, n.transform.position, n, null);
                        Fx.Ring(n.transform.position, 1f, Mats.Pal.Thrall, 0.4f);
                        break;
                    }
                    case 1: Aiming = Skills.Ability("dominion.false_orders"); AimStage = 0; AimThrall = t; break;
                    case 2: Aiming = Skills.Ability("dominion.puppet_strike"); AimStage = 0; AimThrall = t; break;
                    case 3: t.ReleaseFromThrall(false); break;
                }
            }
        }

        /// <summary>Gives a thrall an order now.</summary>
        public static void GiveOrder(Npc t, ThrallOrder order, Vector3 pos, Npc target, Interactable use)
        {
            if (!t || t.State != NpcState.Thrall) return;
            switch (order)
            {
                case ThrallOrder.Move: t.OrderMove(pos); break;
                case ThrallOrder.Follow: t.OrderFollow(); break;
                case ThrallOrder.Interact: if (use) t.OrderInteract(use); break;
                case ThrallOrder.Distract: if (target) t.OrderDistract(target); break;
                case ThrallOrder.Strike: if (target) t.OrderStrike(target); break;
                case ThrallOrder.FalseOrders: if (target) t.OrderFalse(target, pos); break;
            }
        }

        void IssueThrall(Npc t, ThrallOrder order, Vector3 pos, Npc target, Interactable use) => GiveOrder(t, order, pos, target, use);

        void TickThralls(float dt)
        {
            for (int i = Thralls.Count - 1; i >= 0; i--)
            {
                var t = Thralls[i];
                if (!t || t.State != NpcState.Thrall) { Thralls.RemoveAt(i); if (SelectedThrall == t) SelectThrall(null); continue; }
                // a thrall at a threshold invites her in
                if (Game.Level != null)
                    foreach (var d in Game.Level.All<Door>())
                        if (d.Threshold && !d.Invited && Util.FlatDistance(d.transform.position, t.transform.position) < 2.4f) d.Invite();   // the door cell or the cell beside it
            }
            // the thrall under her hand wears a slow violet ring: who the keys move is plain at a glance
            var sel = SelectedThrall;
            if (sel)
            {
                if (!_controlRing) _controlRing = Fx.GroundMarker("control_ring", new Color(0.62f, 0.3f, 0.95f, 0.8f));
                if (!_controlRing.activeSelf) _controlRing.SetActive(true);
                _controlRing.transform.position = sel.transform.position + Vector3.up * 0.06f;
                _controlRing.transform.localScale = Vector3.one * (1.3f + 0.08f * Mathf.Sin(Time.time * 4f));
            }
            else if (_controlRing && _controlRing.activeSelf) _controlRing.SetActive(false);
        }
        GameObject _controlRing;

        // ---------------------------------------------------------------- save helpers
        List<Progression.Counter> CaptureCooldowns()
        {
            var l = new List<Progression.Counter>();
            foreach (var kv in _cd) if (kv.Value > Time.time) l.Add(new Progression.Counter { Key = kv.Key, Value = kv.Value - Time.time });
            if (ApexActive) l.Add(new Progression.Counter { Key = "#apex", Value = _apexUntil - Time.time });
            return l;
        }

        void RestoreCooldowns(List<Progression.Counter> l)
        {
            _cd.Clear();
            _apexUntil = 0f;
            if (l == null) return;
            foreach (var c in l)
            {
                if (c.Key == "#apex") _apexUntil = Time.time + c.Value;
                else _cd[c.Key] = Time.time + c.Value;
            }
        }

        List<Vector3> CaptureRunes()
        {
            var l = new List<Vector3>();
            foreach (var r in _runes) if (r) l.Add(r.transform.position);
            return l;
        }

        void RestoreRunes(List<Vector3> l)
        {
            foreach (var r in _runes) if (r) Destroy(r.gameObject);
            _runes.Clear();
            if (l == null) return;
            foreach (var p in l) _runes.Add(SnareRune.Create(p));
        }
    }
}
