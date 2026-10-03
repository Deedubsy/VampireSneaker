using NUnit.Framework;
using Vespertine.Data;
using Vespertine.Progression;
using Vespertine.Save;

namespace Vespertine.Tests
{
    /// <summary>Round-trips through the real save directory using a profile index the game never shows.</summary>
    public class SaveSystemTests
    {
        const int TestProfile = 7;

        [SetUp] public void Clean() => SaveSystem.DeleteProfile(TestProfile);
        [TearDown] public void Tidy() => SaveSystem.DeleteProfile(TestProfile);

        [Test]
        public void CampaignRoundTrip()
        {
            var c = CampaignState.NewGame(Difficulty.Apex);
            c.AddVitae(300);
            c.Marks = 3;
            c.MissionIndex = 4;
            c.Nodes.Add("predator.stalker");
            c.SetFlag("tobias_met");
            c.AddHabit(Habits.Snuff, 3);
            c.Terror = 5; c.Rumour = 2; c.Tobias = "thrall";
            Assert.IsTrue(SaveSystem.SaveCampaign(c, TestProfile));
            Assert.IsTrue(SaveSystem.ProfileExists(TestProfile));

            var l = SaveSystem.LoadCampaign(TestProfile);
            Assert.IsNotNull(l);
            Assert.AreEqual(Difficulty.Apex, l.Difficulty);
            Assert.AreEqual(300, l.Vitae);
            Assert.AreEqual(3, l.Marks);
            Assert.AreEqual(4, l.MissionIndex);
            Assert.IsTrue(l.Has("predator.stalker"));
            Assert.IsTrue(l.Flag("tobias_met"));
            Assert.AreEqual(3f, l.Habit(Habits.Snuff), 1e-4f);
            Assert.AreEqual("thrall", l.Tobias);
        }

        [Test]
        public void SecondSaveKeepsABackup()
        {
            var c = CampaignState.NewGame(Difficulty.Hunter);
            c.Vitae = 10;
            SaveSystem.SaveCampaign(c, TestProfile);
            c.Vitae = 20;
            SaveSystem.SaveCampaign(c, TestProfile);
            Assert.AreEqual(20, SaveSystem.LoadCampaign(TestProfile).Vitae);
        }

        [Test]
        public void MissingProfileLoadsNull()
        {
            Assert.IsFalse(SaveSystem.ProfileExists(TestProfile));
            Assert.IsNull(SaveSystem.LoadCampaign(TestProfile));
        }

        [Test]
        public void MissionSnapshotRoundTripAndListing()
        {
            var s = new MissionSave { MissionId = "m03", Label = "Before the bell", MissionIndex = 2, Alarm = 1, BodiesFound = 2 };
            Assert.IsTrue(SaveSystem.WriteMission(s, "manual_1", TestProfile));
            Assert.IsTrue(SaveSystem.HasMission("manual_1", TestProfile));
            var r = SaveSystem.ReadMission("manual_1", TestProfile);
            Assert.IsNotNull(r);
            Assert.AreEqual("m03", r.MissionId);
            Assert.AreEqual(1, r.Alarm);
            Assert.AreEqual(2, r.BodiesFound);
            Assert.AreEqual("manual_1", r.Slot);
            Assert.AreEqual(1, SaveSystem.ListMissionSaves(TestProfile).Count);
            SaveSystem.DeleteMission("manual_1", TestProfile);
            Assert.IsFalse(SaveSystem.HasMission("manual_1", TestProfile));
        }

        [Test]
        public void FutureVersionSnapshotsAreRejected()
        {
            var s = new MissionSave { MissionId = "m01", Version = MissionSave.CurrentVersion + 1 };
            SaveSystem.WriteMission(s, "manual_2", TestProfile);
            Assert.IsNull(SaveSystem.ReadMission("manual_2", TestProfile));
        }

        [Test]
        public void ALoadRestartsToItsOwnNightsStart()
        {
            var start = CampaignState.NewGame(Difficulty.Hunter);
            var mid = CampaignState.NewGame(Difficulty.Hunter);
            mid.AddVitae(200);
            var s = new MissionSave { MissionId = "m03", CampaignJson = UnityEngine.JsonUtility.ToJson(mid), CampaignAtStartJson = UnityEngine.JsonUtility.ToJson(start) };
            SaveSystem.WriteMission(s, "manual_3", TestProfile);
            var back = SaveSystem.ReadMission("manual_3", TestProfile);
            Assert.AreEqual(0, UnityEngine.JsonUtility.FromJson<CampaignState>(Vespertine.Mission.MissionController.CampaignAtStart(back)).Vitae);
            // a save from before the field existed restarts from its own snapshot
            back.CampaignAtStartJson = null;
            Assert.AreEqual(200, UnityEngine.JsonUtility.FromJson<CampaignState>(Vespertine.Mission.MissionController.CampaignAtStart(back)).Vitae);
        }
    }
}
