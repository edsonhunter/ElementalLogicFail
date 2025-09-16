using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Collision;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ElementLogicFail.Scripts.Systems.Spawner
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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
            Debug.Log("Spawn Execution System");
            var entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer =
                entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (buffer, entity) in
                     SystemAPI.Query<DynamicBuffer<ElementSpawnRequest>>().WithEntityAccess())
            {
                Debug.Log("Reading buffer");
                var spawner = SystemAPI.GetComponent<Components.Spawner.Spawner>(entity);
                Debug.Log($"Added buffer lenght {buffer.Length}");
                foreach (var request in buffer)
                {
                    var newEntity = entityCommandBuffer.Instantiate(spawner.ElementPrefab);
                    entityCommandBuffer.SetComponent(newEntity, LocalTransform.FromPosition(request.Position));
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