using System;
using System.Collections.Generic;
using NUnit.Framework;
using Vespertine.Save;

namespace Vespertine.Tests
{
    /// <summary>Rotating save slots and the HUD's save age (QW8).</summary>
    public class SaveSlotTests
    {
        static Func<string, DateTime?> Files(Dictionary<string, DateTime> d) => s => d.TryGetValue(s, out var t) ? t : (DateTime?)null;

        [Test]
        public void TheFirstEmptySlotIsUsedThenTheOldest()
        {
            var t0 = new DateTime(2026, 10, 2, 20, 0, 0);
            var d = new Dictionary<string, DateTime>();
            Assert.AreEqual("quick0", SaveSystem.Oldest("quick", 3, Files(d)));
            d["quick0"] = t0;
            Assert.AreEqual("quick1", SaveSystem.Oldest("quick", 3, Files(d)));
            d["quick1"] = t0.AddMinutes(1); d["quick2"] = t0.AddMinutes(2);
            Assert.AreEqual("quick0", SaveSystem.Oldest("quick", 3, Files(d)), "all full: the oldest goes");
            d["quick0"] = t0.AddMinutes(3);
            Assert.AreEqual("quick1", SaveSystem.Oldest("quick", 3, Files(d)), "a save in a lost position keeps the two before it");
        }

        [Test]
        public void QuickSlotsReadAsQuickSaves()
        {
            Assert.AreEqual("Quick save", SaveSystem.SlotName("quick2"));
            Assert.AreEqual("Quick save", SaveSystem.SlotName("quick"), "an older save's single slot");
            Assert.AreEqual("Autosave", SaveSystem.SlotName("auto1"));
        }

        [Test]
        public void SaveAgeReadsInPlainWords()
        {
            Assert.AreEqual("Not saved", SaveSystem.Age(-1f));
            Assert.AreEqual("Saved just now", SaveSystem.Age(4f));
            Assert.AreEqual("Saved 42 s ago", SaveSystem.Age(42.9f));
            Assert.AreEqual("Saved 3 min ago", SaveSystem.Age(200f));
            Assert.AreEqual("Saved 1 h 5 min ago", SaveSystem.Age(3900f));
        }
    }
}
