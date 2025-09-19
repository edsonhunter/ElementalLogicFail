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
    [UpdateAfter(typeof(CollisionSystem))]
    [UpdateAfter(typeof(SpawnerSystem))]
    public partial struct PoolSpawningSystem : ISystem
    {
        private NativeParallelHashMap<Entity, Entity> _prefabToPool;
        private NativeList<ElementSpawnRequest> _tempRequests;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WanderArea>();

            _prefabToPool = new NativeParallelHashMap<Entity, Entity>(16, Allocator.Persistent);
            _tempRequests = new NativeList<ElementSpawnRequest>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            
            _prefabToPool.Clear();
            
            var area = SystemAPI.GetSingleton<WanderArea>();
            var entitySimulationCommandBufferSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer = entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            var poolQuery = SystemAPI.QueryBuilder().WithAll<ElementPool>().Build();
            foreach (var poolEntity in poolQuery.ToEntityArray(Allocator.Temp))
            {
                var pool = state.EntityManager.GetComponentData<ElementPool>(poolEntity);
                if (pool.Prefab != Entity.Null)
                {
                    _prefabToPool.TryAdd(pool.Prefab, poolEntity);
                }
            }

            var spawnRequestQuery = SystemAPI.QueryBuilder().WithAll<ElementSpawnRequest>().Build();
            foreach (var spawnerEntity in spawnRequestQuery.ToEntityArray(Allocator.Temp))
            {
                var requestBuffer = state.EntityManager.GetBuffer<ElementSpawnRequest>(spawnerEntity);
                if (requestBuffer.Length == 0) continue;
                
                _tempRequests.Clear();
                for (int i = 0; i < requestBuffer.Length; i++)
                {
                    _tempRequests.Add(requestBuffer[i]);
                }

                var spawner = state.EntityManager.GetComponentData<Components.Spawner.Spawner>(spawnerEntity);
                for (int i = 0; i < _tempRequests.Length; i++)
                {
                    var request = _tempRequests[i];
                    if (request.Type != spawner.Type) continue;

                    if (_prefabToPool.TryGetValue(spawner.ElementPrefab, out var poolEntity))
                    {
                        var pooledBuffer = state.EntityManager.GetBuffer<PooledEntity>(poolEntity);
                        if (pooledBuffer.Length > 0)
                        {
                            Entity instance = pooledBuffer[^1].Value;
                            pooledBuffer.RemoveAt(pooledBuffer.Length - 1);
                            entityCommandBuffer.SetComponent(instance, LocalTransform.FromPosition(request.Position));
                            var rand = new Random((uint)UnityEngine.Random.Range(1, int.MaxValue));
                            entityCommandBuffer.SetComponent(instance, new ElementData
                            {
                                Type = request.Type,
                                Speed = 2f,
                                Target = new float3(
                                    rand.NextFloat(area.MinArea.x, area.MaxArea.x),
                                    0,
                                    rand.NextFloat(area.MaxArea.z, area.MaxArea.z)),
                                RandomSeed = rand.NextUInt(),
                                Cooldown = 2f
                            });

                            entityCommandBuffer.RemoveComponent<Disabled>(instance);
                        }
                        else
                        {
                            
                        }
                    }
                }

                entityCommandBuffer.SetBuffer<ElementSpawnRequest>(spawnerEntity).Clear();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_prefabToPool.IsCreated)
            {
                _prefabToPool.Dispose();
            }

            if (_tempRequests.IsCreated)
            {
                _tempRequests.Dispose();
            }
        }
    }
}