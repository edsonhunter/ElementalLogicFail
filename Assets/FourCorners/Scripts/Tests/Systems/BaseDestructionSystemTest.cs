using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Combat;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class BaseDestructionSystemTest : ECSTestFixture
    {
        private Entity _matchState;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _matchState = EntityTest.CreateMatchStateWithSlots(EntityManager, MatchPhase.Active);
        }

        private Entity CreateBase(TeamNumber team, int health)
        {
            var entity = EntityManager.CreateEntity(typeof(PlayerBase), typeof(Health));
            EntityManager.SetComponentData(entity, new PlayerBase
            {
                TeamNumber = team,
                IsActive = true,
                NetworkId = 1
            });
            EntityManager.SetComponentData(entity, new Health { Current = health, Max = 100 });
            return entity;
        }

        private Entity CreateMinion(TeamNumber team)
        {
            var entity = EntityManager.CreateEntity(typeof(MinionData));
            EntityManager.SetComponentData(entity, EntityTest.CreateMinionData(team, speed: 1f));
            return entity;
        }

        private void Tick()
        {
            World.GetOrCreateSystem<BaseDestructionSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void BaseDestructionSystem_DeactivatesTheCornerAtZeroHealth()
        {
            var playerBase = CreateBase(TeamNumber.Team1, health: 0);

            Tick();

            Assert.IsFalse(EntityManager.GetComponentData<PlayerBase>(playerBase).IsActive);
        }

        /// <summary>
        /// The base must survive as a deactivated ghost — it still carries the team's replicated
        /// identity and the eliminated player is still connected and watching.
        /// </summary>
        [Test]
        public void BaseDestructionSystem_DoesNotDestroyTheBaseEntity()
        {
            var playerBase = CreateBase(TeamNumber.Team1, health: 0);

            Tick();

            Assert.IsTrue(EntityManager.Exists(playerBase));
        }

        [Test]
        public void BaseDestructionSystem_MarksTheTeamSlotEliminated()
        {
            CreateBase(TeamNumber.Team2, health: 0);

            Tick();

            var slot = EntityManager.GetBuffer<TeamStatusElement>(_matchState)[(int)TeamNumber.Team2];
            Assert.IsTrue(slot.IsEliminated);
        }

        [Test]
        public void BaseDestructionSystem_ClearsTheDeadTeamsMinions()
        {
            CreateBase(TeamNumber.Team1, health: 0);
            var ownMinion = CreateMinion(TeamNumber.Team1);
            var enemyMinion = CreateMinion(TeamNumber.Team2);

            Tick();

            Assert.IsFalse(EntityManager.Exists(ownMinion));
            Assert.IsTrue(EntityManager.Exists(enemyMinion), "Only the dead corner's units should be removed.");
        }

        [Test]
        public void BaseDestructionSystem_LeavesHealthyBasesAlone()
        {
            var playerBase = CreateBase(TeamNumber.Team1, health: 1);
            var minion = CreateMinion(TeamNumber.Team1);

            Tick();

            Assert.IsTrue(EntityManager.GetComponentData<PlayerBase>(playerBase).IsActive);
            Assert.IsTrue(EntityManager.Exists(minion));
        }

        /// <summary>An already-dead corner must not be processed again on later frames.</summary>
        [Test]
        public void BaseDestructionSystem_IsIdempotent()
        {
            CreateBase(TeamNumber.Team1, health: 0);

            Tick();
            var survivor = CreateMinion(TeamNumber.Team1);
            Tick();

            Assert.IsTrue(EntityManager.Exists(survivor));
        }
    }
}
