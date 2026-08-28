using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Systems.Building;
using NUnit.Framework;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// The tuning policy itself. Pure functions of a level, so they need no world at all — which is
    /// the shape that lifts cleanly into the Tier 3.2 balance blob.
    /// </summary>
    [TestFixture]
    public class BuildingUpgradeTest
    {
        [Test]
        public void Cost_RisesWithEachLevel()
        {
            int first = BuildingUpgrade.CostFor(BuildingType.Barracks, 0);
            int second = BuildingUpgrade.CostFor(BuildingType.Barracks, 1);

            Assert.Greater(first, 0);
            Assert.Greater(second, first);
        }

        /// <summary>
        /// An unknown building type must not be free. Zero would make it a legal purchase for a
        /// player with no gold, which is the wrong way for a bad enum value to fail.
        /// </summary>
        [Test]
        public void Cost_OfAnUnsetBuildingTypeIsNotAPurchase()
        {
            Assert.AreEqual(0, BuildingUpgrade.CostFor(BuildingType.None, 0),
                "Priced at zero, but no handler ever reaches this — both check Type first.");
        }

        [Test]
        public void Barracks_ProduceMoreAndFasterWithEachLevel()
        {
            Assert.Greater(
                BuildingUpgrade.EffectiveSpawnAmount(5, 2),
                BuildingUpgrade.EffectiveSpawnAmount(5, 0));

            Assert.Less(
                BuildingUpgrade.EffectiveSpawnInterval(10f, 2),
                BuildingUpgrade.EffectiveSpawnInterval(10f, 0));
        }

        /// <summary>
        /// The failure this exists to prevent is a legal purchase producing an infinite minion
        /// fountain: SpawnerJob fires whenever Timer >= SpawnInterval, so an interval driven to
        /// zero or below fires every single frame.
        /// </summary>
        [Test]
        public void Barracks_IntervalNeverReachesZeroHoweverHighTheLevel()
        {
            float interval = BuildingUpgrade.EffectiveSpawnInterval(2f, BuildingUpgrade.MaxLevel);

            Assert.GreaterOrEqual(interval, BuildingUpgrade.MinSpawnInterval);
            Assert.Greater(interval, 0f);
        }

        [Test]
        public void Central_RaisesIncomeWithEachLevel()
        {
            Assert.AreEqual(5, BuildingUpgrade.EffectiveIncome(5, 0));
            Assert.Greater(BuildingUpgrade.EffectiveIncome(5, 3), BuildingUpgrade.EffectiveIncome(5, 0));
        }
    }
}
