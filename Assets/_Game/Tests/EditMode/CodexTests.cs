using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vespertine.Data;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    /// <summary>The Codex: first sightings are recorded once and saved, and every entry has its text.</summary>
    public class CodexTests
    {
        [Test]
        public void MarkSeenRecordsOnlyTheFirstSighting()
        {
            var c = new CampaignState();
            Assert.IsTrue(c.MarkSeen("hunter"));
            Assert.IsFalse(c.MarkSeen("hunter"));
            Assert.IsFalse(c.MarkSeen(null));
            Assert.IsFalse(c.MarkSeen(""));
            CollectionAssert.AreEqual(new[] { "hunter" }, c.Bestiary);
        }

        [Test]
        public void BestiarySurvivesASave()
        {
            var c = new CampaignState();
            c.MarkSeen("priest"); c.MarkSeen("hound");
            var back = JsonUtility.FromJson<CampaignState>(JsonUtility.ToJson(c));
            CollectionAssert.AreEqual(c.Bestiary, back.Bestiary);
        }

        [Test]
        public void OldSavesWithoutABestiaryLoadEmpty()
        {
            var back = JsonUtility.FromJson<CampaignState>("{\"MissionIndex\":3}");
            Assert.IsNotNull(back.Bestiary);
            Assert.IsEmpty(back.Bestiary);
        }

        [Test]
        public void EveryListedArchetypeHasAFullPage()
        {
            var listed = Codex.Listed.ToList();
            Assert.Greater(listed.Count, 20);
            Assert.IsFalse(listed.Any(a => a.Id == "notable"), "the generic notable stand-in has no page");
            foreach (var a in listed)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(a.Description), a.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(Codex.Lore(a.Id)), $"no codex lore for {a.Id}");
                CollectionAssert.Contains(Codex.Order, a.Faction, a.Id);
                Assert.IsNotEmpty(Codex.SightText(a), a.Id);
                Assert.IsNotEmpty(Codex.WeaponName(a.Weapon), a.Id);
            }
        }

        [Test]
        public void TraitsFollowTheArchetypeFlags()
        {
            var hound = Archetypes.Get("hound");
            var t = Codex.Traits(hound);
            Assert.IsTrue(t.Any(x => x.Contains("Smells")));
            Assert.IsTrue(t.Any(x => x.Contains("animal")));
            StringAssert.Contains("Nose", Codex.SightText(hound));

            Assert.IsTrue(Codex.Traits(Archetypes.Get("priest")).Any(x => x.Contains("Holy aura")));
            Assert.IsTrue(Codex.Traits(Archetypes.Get("bulwark")).Any(x => x.Contains("Armoured")));
            Assert.IsEmpty(Codex.Traits(Archetypes.Get("civilian")));
        }

        [Test]
        public void KindredHaveNoBloodLine()
        {
            Assert.IsNull(Codex.BloodText(Archetypes.Get("fledgling")));
            StringAssert.Contains("Fever-sight", Codex.BloodText(Archetypes.Get("beggar")));
        }
    }
}
