using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Building;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// Barracks upgrades, and in particular the slot resolution the dispatcher deliberately refuses
    /// to do. TargetSlot is unvalidated client input right up until this system checks it, so the
    /// cases where it names nothing real are the ones that matter most.
    /// </summary>
    [TestFixture]
    public class UpgradeBarracksSystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<UpgradeBarracksSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private int LevelOf(Entity e) => EntityManager.GetComponentData<BuildingData>(e).Level;
        private int GoldOf(Entity e) => EntityManager.GetComponentData<PlayerEconomy>(e).Gold;

        private BaseCommandRejection SoleRejection()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommandRejectedRpc>());
            using var rejections = query.ToComponentDataArray<BaseCommandRejectedRpc>(Allocator.Temp);
            Assert.AreEqual(1, rejections.Length, "Expected exactly one rejection.");
            return rejections[0].Reason;
        }

        private int RejectionCount()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommandRejectedRpc>());
            return query.CalculateEntityCount();
        }

        private void Command(Entity corner, byte slot, TeamNumber team = TeamNumber.Team1)
        {
            var connection = EntityManager.CreateEntity();
            EntityTest.CreateBaseCommand(
                EntityManager, corner, team, BaseCommandType.UpgradeBarracks, slot, connection);
        }

        [Test]
        public void UpgradeBarracks_RaisesTheAddressedSlotOnly()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 100000);

            var lane0 = EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 0);
            var lane1 = EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 1);

            Command(corner, slot: 1);
            Tick();

            Assert.AreEqual(1, LevelOf(lane1));
            Assert.AreEqual(0, LevelOf(lane0), "Only the addressed barracks moves.");
        }

        /// <summary>
        /// Every corner has a slot 0. Resolving on the slot alone would let one player's command
        /// land on another player's barracks — the exact attack the command channel exists to
        /// make inexpressible.
        /// </summary>
        [Test]
        public void UpgradeBarracks_NeverReachesAnotherCornersBarracks()
        {
            var mine = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 100000);
            var theirs = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team2, gold: 100000);

            var myBarracks = EntityTest.CreateTestSpawner(EntityManager, mine, 5f, 0f, slot: 0);
            var theirBarracks = EntityTest.CreateTestSpawner(EntityManager, theirs, 5f, 0f, slot: 0);

            Command(mine, slot: 0);
            Tick();

            Assert.AreEqual(1, LevelOf(myBarracks));
            Assert.AreEqual(0, LevelOf(theirBarracks));
            Assert.AreEqual(100000, GoldOf(theirs), "Nobody else pays for my upgrade either.");
        }

        [Test]
        public void UpgradeBarracks_RefusesASlotThatNamesNothing()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 100000);
            EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 0);

            Command(corner, slot: 200);
            Tick();

            Assert.AreEqual(100000, GoldOf(corner), "A command that hit nothing costs nothing.");
            Assert.AreEqual(BaseCommandRejection.NoSuchBuilding, SoleRejection());
        }

        [Test]
        public void UpgradeBarracks_RefusesWhatThePlayerCannotAfford()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 5);
            var barracks = EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 0);

            Command(corner, slot: 0);
            Tick();

            Assert.AreEqual(0, LevelOf(barracks));
            Assert.AreEqual(5, GoldOf(corner));
            Assert.AreEqual(BaseCommandRejection.InsufficientFunds, SoleRejection());
        }

        [Test]
        public void UpgradeBarracks_ChargesAnIncreasingPriceAsItClimbs()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 100000);
            var barracks = EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 0);

            Command(corner, slot: 0);
            Tick();
            int afterFirst = GoldOf(corner);
            int firstCost = 100000 - afterFirst;

            // Cleared by hand because this fixture does not run BaseCommandCleanupSystem. In a real
            // frame an intent lives exactly once; leaving the first one here would have the handler
            // apply it a second time and quietly double the level.
            using (var spent = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommand>()))
            {
                EntityManager.DestroyEntity(spent);
            }

            Command(corner, slot: 0);
            Tick();
            int secondCost = afterFirst - GoldOf(corner);

            Assert.AreEqual(2, LevelOf(barracks));
            Assert.Greater(secondCost, firstCost, "Each level costs more than the last.");
            Assert.AreEqual(0, RejectionCount());
        }

        [Test]
        public void UpgradeBarracks_RefusesOnceMaxed()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 100000);
            var barracks = EntityTest.CreateTestSpawner(EntityManager, corner, 5f, 0f, slot: 0);

            EntityManager.SetComponentData(barracks, new BuildingData
            {
                Type = BuildingType.Barracks,
                Slot = 0,
                Level = BuildingUpgrade.MaxLevel
            });

            Command(corner, slot: 0);
            Tick();

            Assert.AreEqual(BuildingUpgrade.MaxLevel, LevelOf(barracks));
            Assert.AreEqual(BaseCommandRejection.LevelCapped, SoleRejection());
        }
    }
}
