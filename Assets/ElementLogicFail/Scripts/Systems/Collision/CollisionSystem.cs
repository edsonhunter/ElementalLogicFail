using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace ElementLogicFail.Scripts.Systems.Collision
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
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
    
    public struct CollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<ElementData> ElementLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Debug.Log($"Collision event: {collisionEvent}");
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;

            Debug.Log($"Has a Component? {ElementLookup.HasComponent(a)} | and b component? {ElementLookup.HasComponent(b)}");
            if (!ElementLookup.HasComponent(a) && !ElementLookup.HasComponent(b))
            {
                return;
            }

            var dataA = ElementLookup[a];
            var dataB = ElementLookup[b];
            float3 position = 0.5f * (LocalTransformLookup[a].Position + LocalTransformLookup[b].Position);
            Debug.Log($"$Check elements: position {position}, dataA type: {dataA.Type}, dataB type: {dataB.Type}");
            if (dataA.Type == dataB.Type)
            {
                Entity requestEntity = EntityCommandBuffer.CreateEntity(0);
                var buffer = EntityCommandBuffer.AddBuffer<ElementSpawnRequest>(0, requestEntity);
                buffer.Add(new ElementSpawnRequest
                    {
                        Type = dataA.Type,
                        Position = position
                    });
            }
            else
            {
                EntityCommandBuffer.DestroyEntity(0, a);
                EntityCommandBuffer.DestroyEntity(0, b);
            }
        }
    }
}