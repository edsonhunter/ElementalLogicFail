using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Pool;
using Unity.Android.Gradle;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ReturnToPoolSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ElementPool>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var typeToPool = new NativeParallelHashMap<int, Entity>(16, Allocator.Temp);
            var poolQuery = SystemAPI.QueryBuilder().WithAll<ElementPool>().Build();

            using (var poolEntities = poolQuery.ToEntityArray(Allocator.TempJob))
            {
                foreach (var entity in poolEntities)
                {
                    var pool = state.EntityManager.GetComponentData<ElementPool>(entity);
                    if (!typeToPool.ContainsKey(pool.ElementType))
                    {
                        typeToPool.Add(pool.ElementType, entity);
                    }
                }
                
                var returnQuery = SystemAPI.QueryBuilder().WithAll<ReturnToPool>().Build();
                using (var returnEntities = returnQuery.ToEntityArray(Allocator.Temp))
                {
                    foreach (var returnEntity in returnEntities)
                    {
                        var data = state.EntityManager.GetComponentData<ElementData>(returnEntity);
                        if (typeToPool.TryGetValue((int)data.Type, out var poolEntity))
                        {
                            entityCommandBuffer.AppendToBuffer(poolEntity, new PooledEntity
                            {
                                Value = returnEntity
                            });
                        }
                        else
                        {
                            entityCommandBuffer.DestroyEntity(returnEntity);
                        }
                    }
                }
            }
            typeToPool.Dispose();
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}