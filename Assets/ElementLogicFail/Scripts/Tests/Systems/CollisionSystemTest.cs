using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Particles;
using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using ElementLogicFail.Scripts.Systems.Collision;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    public class CollisionSystemTest : ECSTestFixture
    {
         [Test]
        public void SameTypeCollision_WithNoCooldown_CreatesSpawnRequest()
        {
            var spawnerEntity = EntityManager.CreateEntity(typeof(SpawnerRegistry), typeof(ElementSpawnRequest));
            EntityManager.SetComponentData(spawnerEntity, new SpawnerRegistry { Type = ElementType.Fire, SpawnerEntity = spawnerEntity });
            
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0, 0, 0));
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
            
            var buffer = EntityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
            Assert.AreEqual(1, buffer.Length);
        }
        
        [Test]
        public void SameTypeCollision_WithCooldown_DoesNotCreateSpawnRequest()
        {
            var spawnerEntity = EntityManager.CreateEntity(typeof(SpawnerRegistry), typeof(ElementSpawnRequest));
            EntityManager.SetComponentData(spawnerEntity, new SpawnerRegistry { Type = ElementType.Fire, SpawnerEntity = spawnerEntity });

           EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 5f, new float3(0, 0, 0));
           EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();

            var buffer = EntityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
            Assert.AreEqual(0, buffer.Length);
        }

        [Test]
        public void DifferentTypeCollision_AddsReturnToPoolComponent()
        {
            var entityA = EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0, 0, 0));
            var entityB = EntityTest.CreateTestElement(EntityManager, ElementType.Water, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();

            Assert.IsTrue(EntityManager.HasComponent<ReturnToPool>(entityA));
            Assert.IsTrue(EntityManager.HasComponent<ReturnToPool>(entityB));
        }
        
        [Test]
        public void AnyCollision_CreatesParticleSpawnRequest()
        {
            var particleManager = EntityManager.CreateEntity(typeof(ParticlePrefabs), typeof(ParticleSpawnRequest));
            EntityManager.SetComponentData(particleManager, new ParticlePrefabs
            {
                CreationEffect = Entity.Null,
                ExplosionEffect = Entity.Null
            });

            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0, 0, 0));
            EntityTest.CreateTestElement(EntityManager, ElementType.Water, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
            
            var buffer = EntityManager.GetBuffer<ParticleSpawnRequest>(particleManager);
            Assert.AreEqual(1, buffer.Length);
        }
    }
}