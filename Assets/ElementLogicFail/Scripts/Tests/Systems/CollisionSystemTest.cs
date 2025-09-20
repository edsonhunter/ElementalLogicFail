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
        private Entity _particleManager;
        private Entity _spawnerEntity;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            
            _particleManager = EntityManager.CreateEntity(typeof(ParticlePrefabs), typeof(ParticleSpawnRequest));
            EntityManager.SetComponentData(_particleManager, new ParticlePrefabs
            {
                CreationEffect = Entity.Null,
                ExplosionEffect = Entity.Null
            });

            _spawnerEntity = EntityManager.CreateEntity(typeof(SpawnerRegistry), typeof(ElementSpawnRequest));
            EntityManager.SetComponentData(_spawnerEntity, new SpawnerRegistry { Type = ElementType.Fire, SpawnerEntity = _spawnerEntity });
        }

        [Test]
        public void SameTypeCollision_WithNoCooldown_CreatesAllRequests()
        {
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0, 0, 0));
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
            
            var elementBuffer = EntityManager.GetBuffer<ElementSpawnRequest>(_spawnerEntity);
            var particleBuffer = EntityManager.GetBuffer<ParticleSpawnRequest>(_particleManager);
            Assert.AreEqual(1, elementBuffer.Length, "Should create one element spawn request.");
            Assert.AreEqual(1, particleBuffer.Length, "Should create one particle spawn request.");
        }
        
        [Test]
        public void SameTypeCollision_WithCooldown_CreatesNoRequests()
        {
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 5f, new float3(0, 0, 0));
            EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();

            var elementBuffer = EntityManager.GetBuffer<ElementSpawnRequest>(_spawnerEntity);
            var particleBuffer = EntityManager.GetBuffer<ParticleSpawnRequest>(_particleManager);
            Assert.AreEqual(0, elementBuffer.Length, "Should not create an element spawn request.");
            Assert.AreEqual(0, particleBuffer.Length, "Should not create a particle spawn request.");
        }

        [Test]
        public void DifferentTypeCollision_AddsReturnToPoolAndParticleRequest()
        {
            var entityA = EntityTest.CreateTestElement(EntityManager, ElementType.Fire, 0, new float3(0, 0, 0));
            var entityB = EntityTest.CreateTestElement(EntityManager, ElementType.Water, 0, new float3(0.1f, 0, 0));

            World.GetOrCreateSystemManaged<PhysicsSystemGroup>().Update();
            World.GetOrCreateSystem<CollisionSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();

            var particleBuffer = EntityManager.GetBuffer<ParticleSpawnRequest>(_particleManager);
            Assert.IsTrue(EntityManager.HasComponent<ReturnToPool>(entityA), "Entity A should have ReturnToPool component.");
            Assert.IsTrue(EntityManager.HasComponent<ReturnToPool>(entityB), "Entity B should have ReturnToPool component.");
            Assert.AreEqual(1, particleBuffer.Length, "Should create one particle spawn request for explosion.");
        }
    }
}