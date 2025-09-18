using ElementLogicFail.Scripts.Components.Bounds;
using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Collision;
using ElementLogicFail.Scripts.Systems.Spawner;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(SpawnerSystem))]
    [UpdateAfter(typeof(CollisionSystem))]
    public partial struct PoolSpawningSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WanderArea>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var area = SystemAPI.GetSingleton<WanderArea>();
            var entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer =
                entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var poolQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ElementPool>(),
                ComponentType.ReadWrite<PooledEntity>());
            using var poolEntities = poolQuery.ToEntityArray(Allocator.Temp);

            foreach (var (buffer, spawner) in
                     SystemAPI.Query<DynamicBuffer<ElementSpawnRequest>, RefRO<Components.Spawner.Spawner>>())
            {
                foreach (var request in buffer)
                {
                    if (request.Type == spawner.ValueRO.Type)
                    {
                        var rand = new Random((uint)UnityEngine.Random.Range(1, int.MaxValue));
                        var newEntity = entityCommandBuffer.Instantiate(spawner.ValueRO.ElementPrefab);
                        entityCommandBuffer.SetComponent(newEntity, LocalTransform.FromPosition(request.Position));
                        entityCommandBuffer.SetComponent(newEntity, new ElementData
                        {
                            Type = request.Type,
                            Speed = 2f,
                            Target = new float3(
                                rand.NextFloat(area.MinArea.x, area.MaxArea.x),
                                0,
                                rand.NextFloat(area.MinArea.z, area.MaxArea.z)),
                            RandomSeed = rand.NextUInt(),
                            Cooldown = 2f
                        });
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