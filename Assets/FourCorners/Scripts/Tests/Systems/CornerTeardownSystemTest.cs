using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Spawner;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// Covers the cleanup that used to be duplicated in ServerDisconnectSystem and
    /// BaseDestructionSystem. Both now just deactivate the corner; this is the one place that
    /// reacts, so this is the one place it needs testing.
    /// </summary>
    [TestFixture]
    public class CornerTeardownSystemTest : ECSTestFixture
    {
        private Entity CreateCorner(TeamNumber team, bool isActive)
        {
            var entity = EntityManager.CreateEntity(typeof(PlayerBase), typeof(ActiveCorner));
            EntityManager.SetComponentData(entity, new PlayerBase
            {
                TeamNumber = team,
                IsActive = isActive,
                NetworkId = isActive ? 1 : 0
            });
            return entity;
        }

        private Entity CreateSpawner(Entity owningBase)
        {
            var entity = EntityTest.CreateTestSpawner(EntityManager, owningBase, spawnInterval: 5f, timer: 3f);
            return entity;
        }

        private void Tick()
        {
            World.GetOrCreateSystem<CornerTeardownSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void CornerTeardown_ClearsTheRetiredTeamsMinions()
        {
            var corner = CreateCorner(TeamNumber.Team1, isActive: false);
            var ownMinion = EntityTest.CreateTestMinion(EntityManager, TeamNumber.Team1);
            var enemyMinion = EntityTest.CreateTestMinion(EntityManager, TeamNumber.Team2);

            Tick();

            Assert.IsFalse(EntityManager.Exists(ownMinion));
            Assert.IsTrue(EntityManager.Exists(enemyMinion), "Only the retired corner's units go.");
            Assert.IsFalse(EntityManager.HasComponent<ActiveCorner>(corner));
        }

        [Test]
        public void CornerTeardown_SilencesTheRetiredCornersSpawners()
        {
            var corner = CreateCorner(TeamNumber.Team1, isActive: false);
            var spawner = CreateSpawner(corner);

            Tick();

            var data = EntityManager.GetComponentData<SpawnerData>(spawner);
            Assert.IsFalse(data.IsActive);
            Assert.AreEqual(0, data.NetworkId);
            Assert.AreEqual(0f, data.Timer);
        }

        [Test]
        public void CornerTeardown_LeavesLiveCornersAlone()
        {
            var corner = CreateCorner(TeamNumber.Team1, isActive: true);
            var minion = EntityTest.CreateTestMinion(EntityManager, TeamNumber.Team1);

            Tick();

            Assert.IsTrue(EntityManager.Exists(minion));
            Assert.IsTrue(EntityManager.HasComponent<ActiveCorner>(corner));
        }

        /// <summary>
        /// ActiveCorner is removed as part of the teardown, so a corner that stays inactive for
        /// the rest of the match is not swept again every frame.
        /// </summary>
        [Test]
        public void CornerTeardown_RunsOnlyOncePerRetirement()
        {
            CreateCorner(TeamNumber.Team1, isActive: false);

            Tick();
            var laterMinion = EntityTest.CreateTestMinion(EntityManager, TeamNumber.Team1);
            Tick();

            Assert.IsTrue(EntityManager.Exists(laterMinion));
        }
    }
}
