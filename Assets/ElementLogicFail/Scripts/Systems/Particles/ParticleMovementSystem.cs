using ElementLogicFail.Scripts.Components.Particles;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Particles
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ParticleMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var endSimulationEntityCommandBufferSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>(); 
            var entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (particle, transform, entity) in SystemAPI
                         .Query<RefRW<ParticleEffectData>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                particle.ValueRW.Timer += deltaTime;
                if (particle.ValueRO.Timer >= particle.ValueRO.Lifetime)
                {
                    entityCommandBuffer.DestroyEntity(entity);
                }
                else
                {
                    transform.ValueRW.Position.y += 2f * deltaTime;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}