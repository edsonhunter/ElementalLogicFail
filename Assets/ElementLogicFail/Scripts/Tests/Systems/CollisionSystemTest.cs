using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Collision;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    [TestFixture]
    public class CollisionSystemTest :  ECSTestFixture
    {
        [TestCase(1,1,0,0, true, TestName = "Collide between same elements with no cooldown")]
        public void CollisionSystem_AddSpawnRequest(int elementA, int elementB, int cooldownA, int cooldownB, bool collision)
        {
            PhysicsSystemGroup simulationSystem = World.CreateSystemManaged<PhysicsSystemGroup>();
            var entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            EntityManager entityManager = entitySimulationCommandBufferSystem.EntityManager;

            var entityA = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(ElementData));
            
            var capsule = CapsuleCollider.Create(
                new CapsuleGeometry { Vertex0 = float3.zero, Vertex1 = new float3(0,1,0), Radius = 0.5f });
            
            entityManager.SetComponentData(entityA, new LocalTransform { Position = new float3(0,0,0), Scale = 1 });
            entityManager.SetComponentData(entityA, new PhysicsCollider { Value = capsule });
            entityManager.SetComponentData(entityA, EntityTest.CreateElementData((ElementType)elementA, 2, cooldownA));

            var entityB = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(ElementData));
            
            entityManager.SetComponentData(entityB, new LocalTransform { Position = new float3(0.1f,0,0), Scale = 1 });
            entityManager.SetComponentData(entityB, new PhysicsCollider { Value = capsule });
            entityManager.SetComponentData(entityB, EntityTest.CreateElementData((ElementType)elementB, 2, cooldownB));
            
            SystemHandle collisionSystem = World.CreateSystem<CollisionSystem>();
            simulationSystem.Update();
            collisionSystem.Update(World.Unmanaged);
            
            var query = entityManager.CreateEntityQuery(typeof(ElementSpawnRequest));
            Assert.AreEqual(1, query.CalculateEntityCount());
        }
    }
}