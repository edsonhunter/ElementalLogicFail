using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Pool;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial struct ReturnToPoolSystem : ISystem
    {
        private NativeParallelHashMap<int, Entity> _typeToPool;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ElementPool>();
            _typeToPool = new NativeParallelHashMap<int, Entity>(16, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            
            _typeToPool.Clear();
            
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var poolQuery = SystemAPI.QueryBuilder().WithAll<ElementPool>().Build();

            using (var poolEntities = poolQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in poolEntities)
                {
                    var pool = state.EntityManager.GetComponentData<ElementPool>(entity);
                    if (!_typeToPool.ContainsKey(pool.ElementType))
                    {
                        _typeToPool.Add(pool.ElementType, entity);
                    }
                }

                var returnQuery = SystemAPI.QueryBuilder().WithAll<ReturnToPool>().Build();
                using (var returnEntities = returnQuery.ToEntityArray(Allocator.Temp))
                {
                    foreach (var returnEntity in returnEntities)
                    {
                        var data = state.EntityManager.GetComponentData<ElementData>(returnEntity);
                        if (_typeToPool.TryGetValue((int)data.Type, out var poolEntity))
                        {
                            entityCommandBuffer.AddComponent<Disabled>(returnEntities);
                            entityCommandBuffer.AppendToBuffer(poolEntity, new PooledEntity
                            {
                                Value = returnEntity
                            });
                        }
                        else
                        {
                            entityCommandBuffer.DestroyEntity(returnEntity);
                        }
                        entityCommandBuffer.RemoveComponent<ReturnToPool>(returnEntity);
                    }
                }
            }

            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_typeToPool.IsCreated)
                _typeToPool.Dispose();
        }
    }
}