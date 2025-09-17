using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;
using Unity.Mathematics;
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

        public static ElementData CreateElementData(ElementType type, float speed, int cooldown)
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
    }
}