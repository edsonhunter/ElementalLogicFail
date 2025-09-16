using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Collision;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace ElementLogicFail.Scripts.Systems.Spawner
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(SpawnerSystem))]
    [UpdateAfter(typeof(CollisionSystem))]
    public partial struct SpawnExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer =
                entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (buffer, entity) in
                     SystemAPI.Query<DynamicBuffer<ElementSpawnRequest>, RefRO<Components.Spawner.Spawner>>())
            {
                foreach (var request in buffer)
                {
                    if (request.Type == entity.ValueRO.Type)
                    {
                        var newEntity = entityCommandBuffer.Instantiate(entity.ValueRO.ElementPrefab);
                        entityCommandBuffer.SetComponent(newEntity, LocalTransform.FromPosition(request.Position));   
                    }
                }

                buffer.Clear();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}