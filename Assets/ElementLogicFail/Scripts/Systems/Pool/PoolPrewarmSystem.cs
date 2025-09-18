using ElementLogicFail.Scripts.Components.Pool;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PoolPrewarmSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (pool, entity) in SystemAPI.Query<RefRO<ElementPool>>().WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<PooledEntity>(entity))
                {
                    entityCommandBuffer.AddBuffer<PooledEntity>(entity);
                }

                var buffer = entityCommandBuffer.SetBuffer<PooledEntity>(entity);

                for (int index = 0; index < pool.ValueRO.InitialSize; index++)
                {
                    Entity newInstance = entityCommandBuffer.Instantiate(pool.ValueRO.Prefab);
                    entityCommandBuffer.AddComponent(newInstance, new ElementPooled());
                    entityCommandBuffer.SetComponentEnabled<ElementPooled>(newInstance, false);
                    buffer.Add(new PooledEntity
                    {
                        Value = newInstance
                    });
                }
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