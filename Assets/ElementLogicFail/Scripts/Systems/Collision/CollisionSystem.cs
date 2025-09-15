using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Systems.Collision.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Collision
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct CollisionSystem : ISystem
    {
        private ComponentLookup<ElementData> _elementLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
            
            _elementLookup = SystemAPI.GetComponentLookup<ElementData>(true);
            _localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _elementLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            
            SimulationSingleton simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            
            var job = new CollisionEventJob
            {
                ElementLookup = _elementLookup,
                LocalTransformLookup = _localTransformLookup,
                EntityCommandBuffer = entityCommandBuffer.AsParallelWriter(),
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