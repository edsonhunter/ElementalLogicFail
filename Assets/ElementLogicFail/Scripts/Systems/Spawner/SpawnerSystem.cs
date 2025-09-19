using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}