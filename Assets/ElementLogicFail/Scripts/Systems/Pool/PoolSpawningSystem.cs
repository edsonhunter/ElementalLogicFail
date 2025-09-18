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
            var entitySimulationCommandBufferSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer = entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var prefabToPool = new NativeParallelHashMap<Entity, Entity>(16, Allocator.Temp);
            var poolQuery = SystemAPI.QueryBuilder().WithAll<ElementPool>().Build();
            using (var poolEntities = poolQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity poolEntity in poolEntities)
                {
                    var pool = state.EntityManager.GetComponentData<ElementPool>(poolEntity);
                    if (pool.Prefab != Entity.Null)
                    {
                        prefabToPool.TryAdd(pool.Prefab,  poolEntity);
                    }
                }
                
                var spawnRequestQuery = SystemAPI.QueryBuilder().WithAll<ElementSpawnRequest>().Build();
                using (var spawnerEntities = spawnRequestQuery.ToEntityArray(Allocator.Temp))
                {
                    NativeList<ElementSpawnRequest> tempRequest = new NativeList<ElementSpawnRequest>(Allocator.Temp);
                    for (int spwnIndex = 0; spwnIndex < spawnerEntities.Length; spwnIndex++)
                    {
                        var spawnerEntity = spawnerEntities[spwnIndex];
                        var requestBuffer = state.EntityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
                        
                        if (requestBuffer.Length == 0)
                        {
                            continue;
                        }
                        
                        tempRequest.Clear();
                        for (int requestIndex = 0; requestIndex < requestBuffer.Length; requestIndex++)
                        {
                            tempRequest.Add(requestBuffer[requestIndex]);
                        }
                        
                        var spawner = state.EntityManager.GetComponentData<Components.Spawner.Spawner>(spawnerEntity);
                        for (int requestIndex = 0; requestIndex < tempRequest.Length; requestIndex++)
                        {
                            var request = tempRequest[requestIndex];
                            if (request.Type != spawner.Type)
                            {
                                continue;
                            }
                            
                            Entity instance = Entity.Null;

                            if (prefabToPool.TryGetValue(spawner.ElementPrefab, out var poolEntity))
                            {
                                var pooledBuffer = state.EntityManager.GetBuffer<PooledEntity>(poolEntity);
                                if (pooledBuffer.Length > 0)
                                {
                                    instance = pooledBuffer[^1].Value;
                                    pooledBuffer.RemoveAt(pooledBuffer.Length - 1);
                                    entityCommandBuffer.SetComponentEnabled<PoolTag>(instance, true);
                                }
                            }

                            if (instance == Entity.Null)
                            {
                                var rand = new Random((uint)UnityEngine.Random.Range(1, int.MaxValue));
                                instance = entityCommandBuffer.Instantiate(spawner.ElementPrefab);
                                entityCommandBuffer.SetComponent(instance, LocalTransform.FromPosition(request.Position));
                                entityCommandBuffer.SetComponent(instance, new ElementData
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
                                
                                entityCommandBuffer.AddComponent(instance, new PoolTag());
                                entityCommandBuffer.SetComponentEnabled<PoolTag>(instance, false);
                            }
                        }
                        entityCommandBuffer.SetBuffer<ElementSpawnRequest>(spawnerEntity).Clear();
                    }
                    tempRequest.Dispose();
                }
            }

            prefabToPool.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}