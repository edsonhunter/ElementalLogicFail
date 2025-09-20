using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Spawner
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial struct SpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<Components.Spawner.Spawner>();
            state.RequireForUpdate<ElementSpawnRequest>();
            state.RequireForUpdate<SpawnerRateChangeRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer =
                entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var requestBuffer = SystemAPI.GetSingletonBuffer<SpawnerRateChangeRequest>();
            var rateChanges = new NativeHashMap<int, float>(requestBuffer.Length, Allocator.Temp);
            foreach (var request in requestBuffer)
            {
                rateChanges[(int)request.Type] = request.NewRate;
            }
            
            foreach (var (spawner, transform, entity) in
                     SystemAPI.Query<RefRW<Components.Spawner.Spawner>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Components.Spawner.Spawner spawnerRW = spawner.ValueRW;
                
                if (rateChanges.TryGetValue((int)spawner.ValueRO.Type, out var newRate))
                {
                    spawnerRW.SpawnRate = newRate;
                }
                
                spawnerRW.Timer += deltaTime;
                float timePerSpawn = 1f / spawnerRW.SpawnRate;
                if (spawnerRW.Timer >= timePerSpawn)
                {
                    spawnerRW.Timer = 0f;
                    entityCommandBuffer.AppendToBuffer(entity, new ElementSpawnRequest
                    {
                        Type = spawnerRW.Type,
                        Position = transform.ValueRO.Position,
                    });
                }
                spawner.ValueRW = spawnerRW;
            }
            requestBuffer.Clear();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}