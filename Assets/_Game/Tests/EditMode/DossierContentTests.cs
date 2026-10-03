using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Level;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    /// <summary>
    /// The Dossier's answers on the ground: from the first mission it can act on, every mission carries a dormant body
    /// group for each countermeasure that brings people (roof sentries, paired partners, an inquisitor, censer posts),
    /// and none of them is placed where it would see Ilse the moment the mission starts.
    /// </summary>
    public class DossierContentTests
    {
        static readonly string[] BodyGroups = { "cm_rooftop", "cm_paired", "cm_inquest", "cm_censer" };
        const float StartClearance = 10f; // cells between the player start and any countermeasure NPC or waypoint

        static IEnumerable<TestCaseData> AnsweredMissions()
        {
            // the last night (the undercroft) has no Vigil in it: its countermeasures are systemic only
            for (int i = CampaignState.DossierBodiesFrom; i < Missions.All.Count - 1; i++)
                yield return new TestCaseData(Missions.All[i].Id).SetName("Dossier_bodies_" + Missions.All[i].Id);
        }

        [TestCaseSource(nameof(AnsweredMissions))]
        public void EveryAnswerHasBodiesAndNoneSeesTheStart(string id)
        {
            var t = Resources.Load<TextAsset>("Missions/" + id);
            Assert.IsNotNull(t, id);
            var d = MapParser.Parse(t.text);
            var start = d.Entities.Find(e => e.Kind == "player");
            Assert.IsNotNull(start, id);
            var s = new Vector2(start.X, start.Y);

            foreach (var g in BodyGroups)
            {
                var npcs = d.Entities.FindAll(e => e.Kind == "npc" && e.Group == g);
                Assert.IsNotEmpty(npcs, $"{id}: no bodies in group {g}");
                foreach (var n in npcs)
                {
                    Assert.GreaterOrEqual(Vector2.Distance(s, new Vector2(n.X, n.Y)), StartClearance, $"{id}: {n.Id} ({g}) spawns beside the player start");
                    var r = n.Opt("route");
                    if (r == null || !d.Routes.TryGetValue(r, out var route)) continue;
                    // a paired partner walks a beat someone already walks: it adds no ground the mission didn't cover
                    if (d.Entities.Exists(e => e.Kind == "npc" && e.Group == null && e.Opt("route") == r)) continue;
                    foreach (var p in route.Points)
                        Assert.GreaterOrEqual(Vector2.Distance(s, new Vector2(p.X, p.Y)), StartClearance,
                            $"{id}: {n.Id} ({g}) patrols route {r} past the player start at ({p.X},{p.Y})");
                }
            }
        }
    }
}
