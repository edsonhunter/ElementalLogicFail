using ElementLogicFail.Scripts.Components.Pool;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Systems.Particles
{
    public partial struct ReturnParticleToPoolSystem : ISystem
    {
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (pool, entity) in SystemAPI.Query<ParentPool>().WithAll<ReturnToParticlePool>().WithEntityAccess())
            {
                entityCommandBuffer.AddComponent<Disabled>(entity);
                entityCommandBuffer.AppendToBuffer(pool.PoolEntity, new PooledEntity { Value = entity });
                entityCommandBuffer.RemoveComponent<ReturnToParticlePool>(entity);
            }
            
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }
    }
}