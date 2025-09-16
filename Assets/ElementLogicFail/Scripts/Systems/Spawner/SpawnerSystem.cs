using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace ElementLogicFail.Scripts.Systems.Spawner
{
    [BurstCompile]
    public partial struct SpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<Components.Spawner.Spawner>();
            state.RequireForUpdate<ElementSpawnRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Debug.Log("SpawnerSystem Update");
            var deltaTime = SystemAPI.Time.DeltaTime;
            var entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer =
                entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (spawner, transform, entity) in
                     SystemAPI.Query<RefRW<Components.Spawner.Spawner>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Components.Spawner.Spawner spawnerRW = spawner.ValueRW;
                spawnerRW.Timer += deltaTime;
                if (spawnerRW.Timer >= spawnerRW.SpawnRate)
                {
                    spawnerRW.Timer = 0f;
                    DynamicBuffer<ElementSpawnRequest> buffer = entityCommandBuffer.AddBuffer<ElementSpawnRequest>(entity);
                    buffer.Add(new ElementSpawnRequest
                    {
                        Type = spawnerRW.Type,
                        Position = transform.ValueRO.Position,
                    });
                    spawner.ValueRW = spawnerRW;
                    Debug.Log("Added buffer");
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}