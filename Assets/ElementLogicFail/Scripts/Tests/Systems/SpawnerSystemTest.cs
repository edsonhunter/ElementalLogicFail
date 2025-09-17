using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using ElementLogicFail.Scripts.Systems.Spawner;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    public class SpawnerSystemTest : ECSTestSetup
    {
        [Test]
        public void Spawner_AddSpawnRequest()
        {
            var entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            var entityManager = entitySimulationCommandBufferSystem.EntityManager;
            
            Entity entity =
                entityManager.CreateEntity(typeof(Spawner), typeof(LocalTransform), typeof(ElementSpawnRequest));
            entityManager.SetComponentData(entity, new Spawner
            {
                Type = ElementType.Fire,
                ElementPrefab = Entity.Null,
                SpawnRate = 1f,
                Timer = 1f
            });
            
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            
            SystemHandle system = World.CreateSystem<SpawnerSystem>();
            system.Update(World.Unmanaged);
            entitySimulationCommandBufferSystem.Update();
            
            BufferLookup<ElementSpawnRequest> buffer = entitySimulationCommandBufferSystem.GetBufferLookup<ElementSpawnRequest>(true);
            Assert.AreEqual(1, buffer[entity].Length);
        }
    }
}