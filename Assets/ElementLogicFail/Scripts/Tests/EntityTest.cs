using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Tests
{
    public static class EntityTest
    {
        public static Entity CreateElement(EntityManager manager, ElementType type, float speed, int cooldown)
        {
            var entity = manager.CreateEntity();
            manager.SetComponentData(entity, new ElementData()
            {
                Type= type,
                Speed = speed,
                Cooldown = cooldown,
                Target = float3.zero,
                RandomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
            });
            manager.SetComponentData(entity, LocalTransform.FromPosition(new float3(0, 0, 0)));
            return entity;
        }

        public static ElementData CreateElementData(ElementType type, float speed, float cooldown)
        {
            return new ElementData()
            {
                Type = type,
                Speed = speed,
                Cooldown = cooldown,
                Target = float3.zero,
                RandomSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
            };
        }
        
        public static Entity CreateTestElement(EntityManager entityManager, ElementType type, float cooldown, float3 position)
        {
            var entity = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PhysicsCollider),
                typeof(PhysicsVelocity),
                typeof(ElementData));
            
            entityManager.AddComponent<PhysicsMass>(entity);
            
            var capsule = CapsuleCollider.Create(new CapsuleGeometry { Vertex0 = float3.zero, Vertex1 = new float3(0, 1, 0), Radius = 0.5f });
            entityManager.SetComponentData(entity, new LocalTransform { Position = position, Scale = 1 });
            entityManager.SetComponentData(entity, new PhysicsCollider { Value = capsule });
            entityManager.SetComponentData(entity, CreateElementData(type, 2, cooldown));
            return entity;
        }
    }
}