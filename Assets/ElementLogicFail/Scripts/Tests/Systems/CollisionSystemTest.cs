using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using ElementLogicFail.Scripts.Systems.Collision;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    public class CollisionSystemTest : ECSTestFixture
    {
        [TestCase(1, 1, 0, 0, true, TestName = "Collide between same elements with no cooldown")]
        [TestCase(1, 2, 0, 0, false, TestName = "No Collide between different elements")]
        public void CollisionSystem_AddSpawnRequest(int elementA, int elementB, int cooldownA, int cooldownB,
            bool collision)
        {
            PhysicsSystemGroup simulationSystem = World.GetOrCreateSystemManaged<PhysicsSystemGroup>();
            var entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            EntityManager entityManager = entitySimulationCommandBufferSystem.EntityManager;
            
            var spawnerEntity = entityManager.CreateEntity(
                typeof(SpawnerRegistry),
                typeof(ElementSpawnRequest)
            );
            entityManager.SetComponentData(spawnerEntity, new SpawnerRegistry
            {
                Type = (ElementType)elementA,
                SpawnerEntity = spawnerEntity
            });

            var entityA = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(ElementData));

            var capsule = CapsuleCollider.Create(
                new CapsuleGeometry { Vertex0 = float3.zero, Vertex1 = new float3(0, 1, 0), Radius = 0.5f });

            entityManager.SetComponentData(entityA, new LocalTransform { Position = new float3(0, 0, 0), Scale = 1 });
            entityManager.SetComponentData(entityA, new PhysicsCollider { Value = capsule });
            entityManager.SetComponentData(entityA, EntityTest.CreateElementData((ElementType)elementA, 2, cooldownA));

            var entityB = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(ElementData));

            entityManager.SetComponentData(entityB,
                new LocalTransform { Position = new float3(0.1f, 0, 0), Scale = 1 });
            entityManager.SetComponentData(entityB, new PhysicsCollider { Value = capsule });
            entityManager.SetComponentData(entityB, EntityTest.CreateElementData((ElementType)elementB, 2, cooldownB));

            SystemHandle collisionSystem = World.CreateSystem<CollisionSystem>();
            simulationSystem.Update();
            collisionSystem.Update(World.Unmanaged);
            entitySimulationCommandBufferSystem.Update();

            entityManager.CompleteAllTrackedJobs();

            BufferLookup<ElementSpawnRequest> buffer =
                entitySimulationCommandBufferSystem.GetBufferLookup<ElementSpawnRequest>(true);
            Assert.AreEqual(collision, buffer[spawnerEntity].Length >= 1);
        }
    }
}