using NUnit.Framework;
using Vespertine.AI;
using Vespertine.Data;
using Vespertine.Progression;

namespace Vespertine.Tests
{
    /// <summary>E5: what an inquisitor learns from what she leaves behind.</summary>
    public class ExamineTests
    {
        [Test]
        public void ADazedVictimTeachesThemHerSips()
        {
            Assert.AreEqual(Habits.Sips, Npc.ExamineHabit(null, true));
            Assert.AreEqual(Habits.Sips, Npc.ExamineHabit("drain", true), "the living are read as sips, whatever comes later");
        }

        [Test]
        public void BloodArtsAndKillsFeedTheirOwnHabits()
        {
            Assert.AreEqual(Habits.Blood, Npc.ExamineHabit("hemorrhage", false));
            Assert.AreEqual(Habits.Lethal, Npc.ExamineHabit("drain", false));
            Assert.AreEqual(Habits.Lethal, Npc.ExamineHabit("combat", false));
        }

        [Test]
        public void AStagedAccidentDoesNotFoolThem()
        {
            Assert.AreEqual(Habits.Lethal, Npc.ExamineHabit("accident", false));
            Assert.AreEqual(Habits.Lethal, Npc.ExamineHabit("drowned", false));
        }

        [Test]
        public void WorkThatIsNotHersTellsThemNothing()
        {
            Assert.IsNull(Npc.ExamineHabit("fledgling", false));
            Assert.IsNull(Npc.ExamineHabit("unknown", false));
            Assert.IsNull(Npc.ExamineHabit(null, false));
        }

        [Test]
        public void OnlyWardedOfficersReadBodies()
        {
            foreach (var id in new[] { "inquisitor", "vane", "tracker" })
            {
                var a = Archetypes.Get(id);
                Assert.IsTrue(a.Has(ArchFlags.Ward) && a.Has(ArchFlags.Officer), id);
            }
            foreach (var id in new[] { "watchman", "sergeant", "hunter", "priest" })
            {
                var a = Archetypes.Get(id);
                Assert.IsFalse(a.Has(ArchFlags.Ward) && a.Has(ArchFlags.Officer), id);
            }
        }
    }
}
