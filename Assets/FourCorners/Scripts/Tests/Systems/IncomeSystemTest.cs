using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Economy;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class IncomeSystemTest : ECSTestFixture
    {
        private Entity _matchState;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _matchState = EntityTest.CreateMatchStateWithSlots(EntityManager, MatchPhase.Active);
        }

        private void Tick(float deltaTime)
        {
            AdvanceTime(deltaTime);
            World.GetOrCreateSystem<IncomeSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private int GoldOf(Entity corner) => EntityManager.GetComponentData<PlayerEconomy>(corner).Gold;

        [Test]
        public void Income_PaysALiveCornerOverTime()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 0, incomePerSecond: 5);

            Tick(1f);

            Assert.AreEqual(5, GoldOf(corner));
        }

        /// <summary>
        /// The failure this guards is silent and total: truncating income to an int every frame
        /// floors 5/sec to zero at any real frame rate, so the player earns nothing while every
        /// number in the inspector looks correct.
        /// </summary>
        [Test]
        public void Income_AccumulatesAcrossFramesTooShortToEarnACoin()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 0, incomePerSecond: 5);

            // Ten frames at a sixtieth of a second: each one earns 0.083 gold on its own.
            for (int i = 0; i < 10; i++) Tick(1f / 60f);

            Assert.AreEqual(0, GoldOf(corner), "Not yet a whole coin.");

            for (int i = 0; i < 5; i++) Tick(1f / 60f);

            Assert.AreEqual(1, GoldOf(corner), "The remainder must carry, not be discarded.");
        }

        [Test]
        public void Income_SkipsAnInactiveCorner()
        {
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 0, incomePerSecond: 5, isActive: false);

            Tick(2f);

            Assert.AreEqual(0, GoldOf(corner), "An eliminated corner earns nothing.");
        }

        /// <summary>
        /// Four corners quietly banking income through the whole lobby would open the match with
        /// everybody able to afford everything.
        /// </summary>
        [Test]
        public void Income_DoesNotPayBeforeTheMatchStarts()
        {
            EntityManager.SetComponentData(_matchState, new MatchState { Phase = MatchPhase.Lobby });
            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 0, incomePerSecond: 5);

            Tick(5f);

            Assert.AreEqual(0, GoldOf(corner));
        }

        /// <summary>
        /// A corner held for a player who dropped out is still active, so it keeps earning. They
        /// come back to a base that has been working for them — the reason it was left standing.
        /// </summary>
        [Test]
        public void Income_KeepsPayingACornerWhoseOwnerIsAway()
        {
            var slots = EntityManager.GetBuffer<TeamStatusElement>(_matchState);
            slots[(int)TeamNumber.Team1] = new TeamStatusElement
            {
                IsOccupied = true,
                OccupyingPlayer = Entity.Null
            };

            var corner = EntityTest.CreateTestPlayerBaseWithEconomy(
                EntityManager, TeamNumber.Team1, gold: 0, incomePerSecond: 5);

            Tick(2f);

            Assert.AreEqual(10, GoldOf(corner));
        }
    }
}
