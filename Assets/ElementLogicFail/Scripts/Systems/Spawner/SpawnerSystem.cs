using ElementLogicFail.Scripts.Components.Spawner;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Spawner
{
    [BurstCompile]
    public partial struct SpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnControl>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var multiplier = SystemAPI.GetSingleton<SpawnControl>().SpawnRateMultiplier;
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (spawner, xform) in
                     SystemAPI.Query<RefRW<Components.Spawner.Spawner>, RefRO<LocalTransform>>())
            {
                Components.Spawner.Spawner spawnerRW = spawner.ValueRW;
                spawnerRW.SpawnRate += deltaTime;
                if (spawnerRW.SpawnRate >= math.max(0.01f, spawnerRW.SpawnRate * multiplier))
                {
                    
                    spawnerRW.SpawnRate = 0f;
                    var newInstance = entityCommandBuffer.Instantiate(spawnerRW.ElementPrefab);
                    entityCommandBuffer.SetComponent(newInstance, new LocalTransform
                    {
                        Position = xform.ValueRO.Position,
                        Rotation = xform.ValueRO.Rotation,
                        Scale = 1f
                    });
                }
                spawner.ValueRW = spawnerRW;
            }
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}