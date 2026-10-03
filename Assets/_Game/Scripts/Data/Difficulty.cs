namespace Vespertine.Data
{
    public enum Difficulty { Merciful = 0, Hunter = 1, Apex = 2 }

    public class DifficultyDef
    {
        public string Name, Description;
        public float Detection, Search, ShoutRadius, BloodGain, AbilityCost, DamageTaken;
        public bool AllCones;
        /// <summary>Scales the cone-edge grace's angle and depth (D138): Merciful 1.5.</summary>
        public float Grace = 1f;
        /// <summary>How close (m) her feet or her 1.5 s look-ahead must come to a guard's detecting region before his cone
        /// shows by itself (SR.6, SR.12): Merciful 6, otherwise 4.</summary>
        public float ConeNear = 4f;
        /// <summary>Apex (SR.12): with nothing held, unaware guards show their near sector alone; only aware guards, pinned
        /// and hovered ones show a full cone.</summary>
        public bool ConesAwareOnly;
        /// <summary>Exposure rims show within this distance of the line (D132): 10 m, Apex 6 m.</summary>
        public float RimWithin = 10f;
        public int AutosaveMode; // 0 = objectives + 3 min, 1 = objectives, 2 = mission start only
    }

    public static class Difficulties
    {
        static readonly DifficultyDef[] _defs =
        {
            new DifficultyDef { Name = "Merciful", Description = "Slower detection, generous blood. For those who want the story and the hunt.", Detection = 0.7f, Search = 0.7f, ShoutRadius = 10f, BloodGain = 1.25f, AbilityCost = 0.85f, DamageTaken = 0.7f, AllCones = true, Grace = 1.5f, ConeNear = 6f, AutosaveMode = 0 },
            new DifficultyDef { Name = "Hunter", Description = "The intended experience. Mistakes are recoverable, but costly.", Detection = 1f, Search = 1f, ShoutRadius = 15f, BloodGain = 1f, AbilityCost = 1f, DamageTaken = 1f, AllCones = true, AutosaveMode = 1 },
            new DifficultyDef { Name = "Apex", Description = "Sharp eyes, long searches, scarce blood. Unaware guards show only their near sight; Alt shows cones within 20 m.", Detection = 1.3f, Search = 1.5f, ShoutRadius = 22f, BloodGain = 0.85f, AbilityCost = 1.15f, DamageTaken = 1.25f, AllCones = false, ConesAwareOnly = true, RimWithin = 6f, AutosaveMode = 2 },
        };

        public static DifficultyDef Get(Difficulty d) => _defs[(int)d];
        public static DifficultyDef Current => Get(Core.Game.Campaign != null ? Core.Game.Campaign.Difficulty : Difficulty.Hunter);
    }
}
