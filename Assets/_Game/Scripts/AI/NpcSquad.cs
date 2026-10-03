using UnityEngine;
using UnityEngine.AI;
using Vespertine.Core;
using Vespertine.Level;
using Vespertine.Stealth;

namespace Vespertine.AI
{
    /// <summary>A man in a hunter squad (see <see cref="Squad"/>): walking his place in the wedge, standing in the
    /// ring after a shock, and running when the squad breaks.</summary>
    public partial class Npc
    {
        public Squad Squad;
        public int Slot;
        /// <summary>His squad broke and he is running for the rally point. Nothing turns him round now.</summary>
        public bool Routed;
        float _glanceT, _glanceFor;
        bool _inSlot;

        /// <summary>A follower walks his place behind the leader and hands every lead he gets to him.</summary>
        public bool SquadFollower => Squad != null && Slot > 0 && !Squad.Broken && !Routed && Squad.Capable(Squad.Leader) && Squad.Leader != this;

        /// <summary>A follower's own investigation becomes the squad's: the leader goes, the follower keeps his place
        /// (alert now). Returns true when the lead was handed over.</summary>
        bool ForwardToLeader(Vector3 at, bool search, bool run)
        {
            if (!SquadFollower) return false;
            var l = Squad.Leader;
            // already hunting: the lead only moves his search (no re-entry: an officer leader would send his own men
            // straight back to him)
            if (l.State == NpcState.Searching || l.State == NpcState.Alerted) { if (l.State == NpcState.Searching) l.LastKnown = at; }
            else if (search) l.EnterSearching(at, run);
            else l.EnterInvestigating(at, run, true);
            Wary = true;
            if (State != NpcState.Relaxed) ResumeRoutine();
            return true;
        }

        /// <summary>The squad has broken: run for the rally point and leave. Fearless or not.</summary>
        public void Rout(Vector3 rally, Vector3 from, bool shout)
        {
            if (!IsAlive || Incapacitated || State == NpcState.Thrall || Rescue) return;
            Routed = true;
            Wary = true;
            EvacTo = rally;
            Asleep = false; Sitting = false;
            _bellTarget = null;
            SetState(NpcState.Panicked);
            _target = from;
            _stateDuration = 999f;
            if (shout)
            {
                GameEvents.RaiseBark(Id, Random.value < 0.5f ? "Run! RUN! Leave them!" : "I'm not dying for this street!");
                Game.Noise?.Emit(transform.position, 18f, NoiseKind.Scream, this);
            }
        }

        /// <summary>The leader fell: take over his beat (route and place on it), so the squad finishes the patrol.</summary>
        public void TakeBeatFrom(Npc old)
        {
            if (!old || old._route == null) return;
            _route = old._route;
            _wp = old._wp;
            _wpDir = old._wpDir;
            _wpWait = 0f;
            _routeDone = old._routeDone;
            _returning = true;
            if (old._routeDone) { _post = old._post; _postYaw = old._postYaw; }
        }

        /// <summary>Standing back to back after a shock: each man takes his point on the ring and faces out.
        /// Returns true while the ring holds him.</summary>
        bool SquadRingTick(float dt)
        {
            if (Squad == null || !Squad.Ringing || Routed || !Squad.Capable(this)) return false;
            if (State != NpcState.Relaxed && State != NpcState.Suspicious && State != NpcState.Investigating
                && State != NpcState.Searching && State != NpcState.Distracted) return false;
            int i = 0, n = 0;
            foreach (var m in Squad.Members)
            {
                if (!Squad.Capable(m)) continue;
                if (m == this) i = n;
                n++;
            }
            var pt = SquadMath.RingPoint(Squad.RingCentre, i, n);
            if (Util.FlatDistance(pt, transform.position) > 0.5f && MoveTo(pt, true) && !Stuck(dt)) return true;
            Halt();
            var outward = (transform.position - Squad.RingCentre).Flat();
            if (outward.sqrMagnitude < 0.01f) outward = Forward;
            // sweep a little either side of straight out
            FaceYaw(Util.DirToFacing(outward) + Mathf.Sin(Time.time * 0.8f + Slot * 1.7f) * 35f, 120f);
            return true;
        }

        /// <summary>Walk the wedge behind the leader. Where the street is too narrow for the wedge, fall into file.
        /// Returns true while it is in charge.</summary>
        bool SquadFollowTick(float dt)
        {
            if (!SquadFollower) return false;
            var l = Squad.Leader;
            var lf = l.Forward.Flat();
            if (lf.sqrMagnitude < 0.01f) lf = Vector3.forward;
            lf.Normalize();
            var right = Vector3.Cross(Vector3.up, lf);
            var off = SquadMath.SlotOffset(Slot);
            var lp = l.transform.position;
            var slot = lp + right * off.x + lf * off.y;
            // a wall between the leader and the slot: single file instead
            if (NavMesh.Raycast(lp, slot, out _, Filter)) slot = lp - lf * (1.3f * Slot);
            float d = Util.FlatDistance(slot, transform.position);
            bool leaderMoving = l.Agent && l.Agent.enabled && l.Agent.velocity.sqrMagnitude > 0.04f;
            float speedMul = d > 3f ? 1.3f : d > 1.2f ? 1.12f : 1f;
            if (d > (_inSlot && !leaderMoving ? 1.2f : 0.45f) && MoveTo(slot, d > 7f || l.State == NpcState.Alerted, speedMul) && !Stuck(dt))
            {
                _inSlot = false;
                RearGlance(dt, true);
                return true;
            }
            Halt();
            _inSlot = true;
            if (SquadMath.IsRear(Slot, Squad.Standing)) FaceYaw(Util.DirToFacing(-lf) + Mathf.Sin(Time.time * 0.5f) * 50f, 120f);
            else FaceYaw(Util.DirToFacing(lf) + (off.x < 0f ? -25f : off.x > 0f ? 25f : 0f), 180f);
            return true;
        }

        /// <summary>The rearguard keeps turning his head to the street behind while the squad walks (the agent's own
        /// steering is paused for the glance; <see cref="EndGlanceIfIdle"/> hands it back).</summary>
        void RearGlance(float dt, bool moving)
        {
            if (!moving || !SquadMath.IsRear(Slot, Squad.Standing)) { _glanceFor = 0f; return; }
            if (_glanceFor <= 0f)
            {
                _glanceT += dt;
                if (_glanceT < SquadMath.GlanceEvery) return;
                _glanceT = Random.Range(-1.5f, 1.5f);
                _glanceFor = SquadMath.GlanceTime;
            }
            _glanceFor -= dt;
            if (_glanceFor <= 0f) return;
            _glanceFrame = Time.frameCount;
            if (Agent) Agent.updateRotation = false;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-Squad.Leader.Forward.Flat()), 300f * dt);
        }
        int _glanceFrame = -1;

        /// <summary>Called every frame: a glance not renewed this frame is over.</summary>
        void EndGlanceIfIdle()
        {
            if (_glanceFrame == Time.frameCount || !Agent || Agent.updateRotation) return;
            Agent.updateRotation = true;
            _glanceFor = 0f;
        }
    }
}
