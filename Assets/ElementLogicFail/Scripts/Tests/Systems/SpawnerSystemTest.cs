using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Spawner;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    [TestFixture]
    public class SpawnerSystemTest : ECSTestFixture
    {
        [Test]
        public void Spawner_WhenTimerIsReady_AddsSpawnRequest()
        {
            var entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var entityManager = entitySimulationCommandBufferSystem.EntityManager;
            
            var spawnerEntity = EntityTest.CreateTestSpawner(entityManager, 1f, 1f); // Rate of 1/sec, timer is at 1sec
            
            World.GetOrCreateSystem<SpawnerSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
            
            var buffer = entityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
            Assert.AreEqual(1, buffer.Length);
        }
        
        [Test]
        public void Spawner_WhenTimerNotReady_DoesNotAddSpawnRequest()
        {
            var spawnerEntity = EntityTest.CreateTestSpawner(EntityManager, 1f, 0.5f); // Rate of 1/sec, timer is at 0.5sec
            
            World.GetOrCreateSystem<SpawnerSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
            
            var buffer = EntityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
            Assert.AreEqual(0, buffer.Length);
        }
    }
}