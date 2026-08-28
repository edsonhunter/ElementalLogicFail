using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Economy;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class BountySystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<BountySystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private int GoldOf(Entity corner) => EntityManager.GetComponentData<PlayerEconomy>(corner).Gold;

        private int PendingKills()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<KillEvent>());
            return query.CalculateEntityCount();
        }

        [Test]
        public void Bounty_PaysTheKillersCorner()
        {
            var killer = EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team1);
            var victim = EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team2);

            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);

            Tick();

            Assert.Greater(GoldOf(killer), 0);
            Assert.AreEqual(0, GoldOf(victim), "Dying pays nothing.");
        }

        [Test]
        public void Bounty_ScalesWithTheNumberOfKills()
        {
            var killer = EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team1);

            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);
            Tick();
            int afterOne = GoldOf(killer);

            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);
            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team3);
            Tick();

            Assert.AreEqual(afterOne * 3, GoldOf(killer), "Three kills, three bounties.");
        }

        /// <summary>
        /// Every event is consumed on the frame it is read. Leaving them would pay the same kill
        /// again on every subsequent frame — an unbounded income from one dead minion.
        /// </summary>
        [Test]
        public void Bounty_ConsumesTheKillEvent()
        {
            EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team1);
            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);

            Tick();

            Assert.AreEqual(0, PendingKills());
        }

        [Test]
        public void Bounty_PaysNothingToACornerThatHasFallen()
        {
            var killer = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, isActive: false);

            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);

            Tick();

            Assert.AreEqual(0, GoldOf(killer));
            Assert.AreEqual(0, PendingKills(), "Still consumed — there is just nobody to pay.");
        }

        /// <summary>
        /// A kill by a team with no corner on the field must not spill into someone else's purse.
        /// </summary>
        [Test]
        public void Bounty_LeavesOtherCornersAlone()
        {
            var bystander = EntityTest.CreateTestPlayerBaseWithEconomy(EntityManager, TeamNumber.Team3);

            EntityTest.CreateKillEvent(EntityManager, TeamNumber.Team1, TeamNumber.Team2);

            Tick();

            Assert.AreEqual(0, GoldOf(bystander));
        }
    }
}
