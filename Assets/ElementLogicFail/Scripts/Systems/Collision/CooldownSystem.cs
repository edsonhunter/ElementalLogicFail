using ElementLogicFail.Scripts.Components.Element;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Systems.Collision
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (RefRW<ElementData> elementData in SystemAPI.Query<RefRW<ElementData>>())
            {
                if (elementData.ValueRO.Cooldown > 0f)
                {
                    elementData.ValueRW.Cooldown = math.max(0f, elementData.ValueRO.Cooldown - deltaTime);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}