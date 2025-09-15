using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
            
            ComponentLookup<ElementData> elementLookUpData = SystemAPI.GetComponentLookup<ElementData>(true);
            ComponentLookup<LocalTransform> xformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            ComponentLookup<Components.Spawner.Spawner> spawnerLookup = SystemAPI.GetComponentLookup<Components.Spawner.Spawner>(true);
            BufferLookup<ElementSpawnRequest> bufferLookup = SystemAPI.GetBufferLookup<ElementSpawnRequest>();
            
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            EntityCommandBuffer.ParallelWriter parallel = entityCommandBuffer.AsParallelWriter();
            
            var job = new CollisionEventJob
            {
                ElementLookup = elementLookUpData,
                LocalTransformLookup = xformLookup,
                SpawnerLookup = spawnerLookup,
                BufferLookup = bufferLookup,
                EntityCommandBuffer = parallel
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