using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Spawner;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class SpawnerSystemTest : ECSTestFixture
    {
        private Entity CreateReadySpawner(float spawnInterval, float timer)
        {
            EntityTest.CreateActiveMatchState(EntityManager);
            var playerBase = EntityTest.CreateTestPlayerBase(EntityManager, TeamNumber.Team1);
            return EntityTest.CreateTestSpawner(EntityManager, playerBase, spawnInterval, timer);
        }

        private void Tick()
        {
            World.GetOrCreateSystem<SpawnerSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void Spawner_WhenTimerIsReady_AddsSpawnRequest()
        {
            var spawnerEntity = CreateReadySpawner(spawnInterval: 1f, timer: 1f);

            Tick();

            var buffer = EntityManager.GetBuffer<MinionSpawnRequest>(spawnerEntity);
            Assert.AreEqual(1, buffer.Length);
        }

        [Test]
        public void Spawner_WhenTimerNotReady_DoesNotAddSpawnRequest()
        {
            var spawnerEntity = CreateReadySpawner(spawnInterval: 1f, timer: 0.5f);

            Tick();

            var buffer = EntityManager.GetBuffer<MinionSpawnRequest>(spawnerEntity);
            Assert.AreEqual(0, buffer.Length);
        }

        [Test]
        public void Spawner_WhenOwningBaseIsInactive_DoesNotAddSpawnRequest()
        {
            EntityTest.CreateActiveMatchState(EntityManager);
            var playerBase = EntityTest.CreateTestPlayerBase(EntityManager, TeamNumber.Team1, isActive: false);
            var spawnerEntity = EntityTest.CreateTestSpawner(EntityManager, playerBase, 1f, 1f);

            Tick();

            // PlayerBase is the authority: an unclaimed corner must stay silent even though
            // SpawnerData.IsActive is set, because that field is only a mirror.
            var buffer = EntityManager.GetBuffer<MinionSpawnRequest>(spawnerEntity);
            Assert.AreEqual(0, buffer.Length);
        }

        [Test]
        public void Spawner_WhenMatchNotActive_DoesNotAddSpawnRequest()
        {
            var matchState = EntityManager.CreateEntity(typeof(MatchStateTag), typeof(Components.Connection.MatchState));
            EntityManager.SetComponentData(matchState, new Components.Connection.MatchState
            {
                Phase = Components.Connection.MatchPhase.Lobby
            });

            var playerBase = EntityTest.CreateTestPlayerBase(EntityManager, TeamNumber.Team1);
            var spawnerEntity = EntityTest.CreateTestSpawner(EntityManager, playerBase, 1f, 1f);

            Tick();

            // The lobby gate: queuing waves before the match starts let them accumulate in an
            // uncapped buffer and discharge all at once on start.
            var buffer = EntityManager.GetBuffer<MinionSpawnRequest>(spawnerEntity);
            Assert.AreEqual(0, buffer.Length);
        }
    }
}
