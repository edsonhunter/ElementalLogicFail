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
    /// The first handler to consume a BaseCommand, and therefore the first place the command
    /// channel does something a player can feel.
    ///
    /// Ownership is not tested here on purpose — it was settled by ServerBaseCommandSystem before
    /// this intent existed, and re-checking it in the handler is exactly the duplication the
    /// dispatcher was built to prevent. What is tested is everything ownership does not cover.
    /// </summary>
    [TestFixture]
    public class UpgradeCentralSystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<UpgradeCentralSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private BuildingData BuildingOf(Entity e) => EntityManager.GetComponentData<BuildingData>(e);
        private int GoldOf(Entity e) => EntityManager.GetComponentData<PlayerEconomy>(e).Gold;

        private BaseCommand SoleCommand()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommand>());
            using var commands = query.ToComponentDataArray<BaseCommand>(Allocator.Temp);
            Assert.AreEqual(1, commands.Length);
            return commands[0];
        }

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

        /// <summary>A corner rich enough to buy, and a connection to answer to.</summary>
        private Entity Corner(int gold)
            => EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team1, gold: gold);

        private void Command(Entity corner, byte slot = 0)
        {
            var connection = EntityManager.CreateEntity();
            EntityTest.CreateBaseCommand(
                EntityManager, corner, TeamNumber.Team1, BaseCommandType.UpgradeCentral, slot, connection);
        }

        [Test]
        public void UpgradeCentral_RaisesTheLevelAndChargesForIt()
        {
            int cost = BuildingUpgrade.CostFor(BuildingType.Central, currentLevel: 0);
            var corner = Corner(cost + 25);
            Command(corner);

            Tick();

            Assert.AreEqual(1, BuildingOf(corner).Level);
            Assert.AreEqual(25, GoldOf(corner));
            Assert.AreEqual(0, RejectionCount());
        }

        [Test]
        public void UpgradeCentral_RefusesWhatThePlayerCannotAfford()
        {
            var corner = Corner(1);
            Command(corner);

            Tick();

            Assert.AreEqual(0, BuildingOf(corner).Level);
            Assert.AreEqual(1, GoldOf(corner), "A refused purchase costs nothing.");
            Assert.AreEqual(BaseCommandRejection.InsufficientFunds, SoleRejection());
        }

        [Test]
        public void UpgradeCentral_RefusesOnceTheBuildingIsMaxed()
        {
            var corner = Corner(100000);
            EntityManager.SetComponentData(corner, new BuildingData
            {
                Type = BuildingType.Central,
                Level = BuildingUpgrade.MaxLevel
            });
            Command(corner);

            Tick();

            Assert.AreEqual(BuildingUpgrade.MaxLevel, BuildingOf(corner).Level);
            Assert.AreEqual(100000, GoldOf(corner));
            Assert.AreEqual(BaseCommandRejection.LevelCapped, SoleRejection());
        }

        /// <summary>
        /// The intent is a frame old by the time a handler sees it, so the corner it names may have
        /// been destroyed in between. Same staleness rule as Engagement.Target.
        /// </summary>
        [Test]
        public void UpgradeCentral_SurvivesACornerThatHasGone()
        {
            var corner = Corner(100000);
            Command(corner);
            EntityManager.DestroyEntity(corner);

            Assert.DoesNotThrow(Tick);

            Assert.AreEqual(BaseCommandRejection.NoSuchBuilding, SoleRejection());
        }

        /// <summary>
        /// A rejection is still this system having handled the command. Leaving the flag clear
        /// would have BaseCommandCleanupSystem accuse a working handler of not existing.
        /// </summary>
        [Test]
        public void UpgradeCentral_MarksARejectedCommandHandled()
        {
            var corner = Corner(0);
            Command(corner);

            Tick();

            Assert.IsTrue(SoleCommand().Handled);
        }

        [Test]
        public void UpgradeCentral_IgnoresCommandsMeantForSomethingElse()
        {
            var corner = Corner(100000);
            var connection = EntityManager.CreateEntity();
            EntityTest.CreateBaseCommand(
                EntityManager, corner, TeamNumber.Team1, BaseCommandType.UpgradeBarracks, 0, connection);

            Tick();

            Assert.AreEqual(0, BuildingOf(corner).Level);
            Assert.IsFalse(SoleCommand().Handled, "Another handler owns this one.");
            Assert.AreEqual(0, RejectionCount());
        }
    }
}
