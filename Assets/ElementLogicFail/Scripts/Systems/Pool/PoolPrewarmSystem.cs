using ElementLogicFail.Scripts.Components.Pool;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

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
                if (state.EntityManager.HasComponent<Prewarmed>(entity))
                    continue;

                if (!state.EntityManager.HasBuffer<PooledEntity>(entity))
                {
                    entityCommandBuffer.AddBuffer<PooledEntity>(entity);
                }

                int initialSize = pool.ValueRO.InitialSize;
                Entity prefab = pool.ValueRO.Prefab;

                for (int i = 0; i < initialSize; i++)
                {
                    var newInstance = entityCommandBuffer.Instantiate(prefab);
                    entityCommandBuffer.AddComponent(newInstance, new PoolTag());
                    entityCommandBuffer.SetComponentEnabled<PoolTag>(newInstance, false);
                    entityCommandBuffer.AppendToBuffer(entity, new PooledEntity
                    {
                        Value = newInstance
                    });
                }

                entityCommandBuffer.AddComponent<Prewarmed>(entity);
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