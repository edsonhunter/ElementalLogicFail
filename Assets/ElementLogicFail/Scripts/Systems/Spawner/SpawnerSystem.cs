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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (spawner, transform, entity) in
                     SystemAPI.Query<RefRW<Components.Spawner.Spawner>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Components.Spawner.Spawner spawnerRW = spawner.ValueRW;
                spawnerRW.Timer += deltaTime;
                if (spawnerRW.Timer >= spawnerRW.SpawnRate)
                {
                    spawnerRW.Timer = 0f;
                    DynamicBuffer<ElementSpawnRequest> buffer = state.EntityManager.GetBuffer<ElementSpawnRequest>(entity);
                    buffer.Add(new ElementSpawnRequest
                    {
                        Type = spawnerRW.Type,
                        Position = transform.ValueRO.Position,
                    });
                    
                    DynamicBuffer<ElementSpawnRequest> request = state.EntityManager.GetBuffer<ElementSpawnRequest>(entity);
                    foreach (ElementSpawnRequest spawnerRequest in request)
                    {
                        if (spawnerRequest.Type == spawnerRW.Type)
                        {
                            var newInstance = entityCommandBuffer.Instantiate(spawnerRW.ElementPrefab);
                            entityCommandBuffer.SetComponent(newInstance,
                                LocalTransform.FromPosition(spawnerRequest.Position));
                        }
                    }
                    request.Clear();
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