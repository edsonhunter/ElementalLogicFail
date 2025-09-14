using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct CollisionSystem : ISystem
    {
        private EntityQuery _spawnBufferQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spawnBufferQuery = state.GetEntityQuery(ComponentType.ReadWrite<ElementSpawnRequest>());
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SimulationSingleton simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            Entity spawnBufferEntity = _spawnBufferQuery.GetSingletonEntity();
            
            ComponentLookup<ElementData> lookUpData = SystemAPI.GetComponentLookup<ElementData>(true);
            ComponentLookup<LocalTransform> xformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var spawnBuffer = SystemAPI.GetBufferLookup<ElementSpawnRequest>();
            
            var job = new CollisionEventJob
            {
                ElementLookup = lookUpData,
                LocalTransformLookup = xformLookup,
                SpawnBufferLookup = spawnBuffer,
                EntityCommandBuffer = entityCommandBuffer.AsParallelWriter()
            };
            
            state.Dependency = job.Schedule(simulation, state.Dependency);
            state.Dependency.Complete();
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}