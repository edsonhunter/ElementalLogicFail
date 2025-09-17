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
            var entity =
                EntityManager.CreateEntity(typeof(Spawner), typeof(LocalTransform), typeof(ElementSpawnRequest));
            EntityManager.SetComponentData(entity, new Spawner
            {
                Type = ElementType.Fire,
                ElementPrefab = Entity.Null,
                SpawnRate = 1f,
                Timer = 1f
            });
            
            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            var system = World.CreateSystem<SpawnerSystem>();
            system.Update(World.Unmanaged);

            EndSimulationEntityCommandBufferSystem entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            entitySimulationCommandBufferSystem.Update();
            
            DynamicBuffer<ElementSpawnRequest> buffer = EntityManager.GetBuffer<ElementSpawnRequest>(entity);
            Assert.AreEqual(1, buffer.Length);
        }
    }
}