using ElementLogicFail.Scripts.Components.Bounds;
using ElementLogicFail.Scripts.Components.Element;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Wander
{
    [BurstCompile]
    public partial struct WanderSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WanderArea>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var area = SystemAPI.GetSingleton<WanderArea>();

            foreach (var (element, transform) in SystemAPI.Query<RefRW<ElementData>, RefRW<LocalTransform>>())
            {
                var elementRW = element.ValueRW;
                var transformRW = transform.ValueRW;

                if (math.distance(transformRW.Position, elementRW.Target) < 0.2f)
                {
                    var rand = new Unity.Mathematics.Random(elementRW.RandomSeed);
                    elementRW.Target = new float3(
                        rand.NextFloat(area.MinArea.x, area.MaxArea.x),
                        0,
                        rand.NextFloat(area.MinArea.z, area.MaxArea.z));
                    elementRW.RandomSeed = rand.NextUInt();
                }
                
                float3 direction = math.normalizesafe(elementRW.Target - transformRW.Position);
                transformRW.Position += direction * elementRW.Speed * deltaTime;
                element.ValueRW = elementRW;
                transform.ValueRW = transformRW;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}