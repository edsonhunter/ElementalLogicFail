using ElementLogicFail.Scripts.Components.Particles;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Particles
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ParticleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EndSimulationEntityCommandBufferSystem.Singleton entitySimulationCommandBufferSystem =
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer entityCommandBuffer = entitySimulationCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in SystemAPI
                         .Query<DynamicBuffer<ParticleSpawnRequest>>().WithEntityAccess())
            {
                foreach (var spawnRequest in request)
                {
                    for (int requestIdx = 0; requestIdx < request.Length; requestIdx++)
                    {
                        var instance = entityCommandBuffer.Instantiate(spawnRequest.Prefab);
                        entityCommandBuffer.SetComponent(instance, LocalTransform.FromPosition(spawnRequest.Position));
                        entityCommandBuffer.AddComponent(instance, new ParticleEffectData
                        {
                            Lifetime = 1f,
                            Timer = 0f
                        });
                    }
                }
                request.Clear();
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}