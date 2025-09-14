using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
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
            state.RequireForUpdate<ElementPoolTag>();
            state.RequireForUpdate<SpawnControl>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var deltaTime = SystemAPI.Time.DeltaTime;
            var multiplier = SystemAPI.GetSingleton<SpawnControl>().SpawnRateMultiplier;
            var poolEntity = SystemAPI.GetSingletonEntity<ElementPoolTag>();
            var poolSpawnBuf = entityManager.GetBuffer<PoolSpawnRequest>(poolEntity);
            
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (spawner, xform) in
                     SystemAPI.Query<RefRW<Components.Spawner.Spawner>, RefRO<LocalTransform>>())
            {
                Components.Spawner.Spawner spawnerRW = spawner.ValueRW;
                spawnerRW.SpawnRate += deltaTime;
                if (spawnerRW.SpawnRate >= math.max(0.01f, spawnerRW.SpawnRate * multiplier))
                {
                    spawnerRW.SpawnRate = 0f;
                    poolSpawnBuf.Add(new PoolSpawnRequest
                    {
                        Type = spawnerRW.Type,
                        Position = xform.ValueRO.Position,
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