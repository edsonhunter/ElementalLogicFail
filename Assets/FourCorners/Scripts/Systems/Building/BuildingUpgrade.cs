using FourCorners.Scripts.Components.Building;

namespace FourCorners.Scripts.Systems.Building
{
    /// <summary>
    /// What an upgrade costs, how high it goes, and what a level is worth.
    ///
    /// One place rather than two handlers each with their own numbers, so "barracks are too cheap"
    /// is a single edit. These are the tuning constants Tier 3.2 promotes into a balance blob
    /// alongside unit stats — the shape here is deliberately the shape that lifts cleanly into one:
    /// pure functions of a level, no state, nothing reading the world.
    ///
    /// Public rather than internal purely so tests can assert against the real numbers. A test that
    /// restates the cost formula passes forever after the formula changes, which is worse than the
    /// encapsulation is worth.
    /// </summary>
    public static class BuildingUpgrade
    {
        /// <summary>Levels bought on top of the authored baseline. Level 0 is un-upgraded.</summary>
        public const int MaxLevel = 5;

        private const int BarracksBaseCost = 150;
        private const int CentralBaseCost = 250;

        /// <summary>Extra minions per wave, per barracks level.</summary>
        public const int SpawnAmountPerLevel = 1;

        /// <summary>Seconds shaved off the wave interval, per barracks level.</summary>
        public const float SpawnIntervalPerLevel = 1.5f;

        /// <summary>
        /// Floor on the wave interval. Without it a high enough level drives the interval to zero
        /// or negative, and SpawnerJob's <c>Timer &gt;= SpawnInterval</c> would fire every frame —
        /// an unbounded minion fountain from a legal purchase.
        /// </summary>
        public const float MinSpawnInterval = 1f;

        /// <summary>Extra gold per second, per central-building level.</summary>
        public const int IncomePerLevel = 4;

        /// <summary>
        /// Price of moving from <paramref name="currentLevel"/> to the next one.
        ///
        /// Linear rather than exponential: with a cap this low, exponential pricing makes the last
        /// level unreachable in a match that lasts ten minutes.
        /// </summary>
        public static int CostFor(BuildingType type, int currentLevel)
        {
            int baseCost = type switch
            {
                BuildingType.Central => CentralBaseCost,
                BuildingType.Barracks => BarracksBaseCost,
                _ => 0
            };

            return baseCost * (currentLevel + 1);
        }

        /// <summary>Wave size at a given barracks level.</summary>
        public static int EffectiveSpawnAmount(int authoredAmount, int level)
            => authoredAmount + level * SpawnAmountPerLevel;

        /// <summary>Seconds between waves at a given barracks level, never below the floor.</summary>
        public static float EffectiveSpawnInterval(float authoredInterval, int level)
        {
            float interval = authoredInterval - level * SpawnIntervalPerLevel;
            return interval < MinSpawnInterval ? MinSpawnInterval : interval;
        }

        /// <summary>Gold per second at a given central-building level.</summary>
        public static int EffectiveIncome(int baseIncome, int level)
            => baseIncome + level * IncomePerLevel;
    }
}
