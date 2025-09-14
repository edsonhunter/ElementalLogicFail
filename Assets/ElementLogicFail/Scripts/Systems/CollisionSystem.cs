using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
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
            _spawnBufferQuery = state.GetEntityQuery(ComponentType.ReadWrite<ElementData>());
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SimulationSingleton simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer();
            
            EntityManager entityManager = state.EntityManager;
            Entity spawnBufferEntity = _spawnBufferQuery.GetSingletonEntity();
            DynamicBuffer<ElementSpawnRequest> spawnBuffer = entityManager.GetBuffer<ElementSpawnRequest>(spawnBufferEntity);

            ComponentLookup<ElementData> lookUpData = SystemAPI.GetComponentLookup<ElementData>(true);
            ComponentLookup<LocalTransform> xformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

            CollisionEvents events = simulation.AsSimulation().CollisionEvents;
            foreach (CollisionEvent collisionEvent in events)
            {
                Entity a = collisionEvent.EntityA;
                Entity b = collisionEvent.EntityB;
                
                bool aIsElement = lookUpData.HasComponent(a);
                bool bIsElement = lookUpData.HasComponent(b);

                if (!aIsElement && !bIsElement)
                {
                    continue;
                }
                
                ElementType typeA = lookUpData[a].Type;
                ElementType typeB = lookUpData[b].Type;
                float3 position = 0.5f * (xformLookup[a].Position + xformLookup[b].Position);

                if (typeA == typeB)
                {
                    spawnBuffer.Add(new ElementSpawnRequest
                    {
                        Type = typeA,
                        Position = position,
                    });
                }
                else
                {
                    entityCommandBuffer.DestroyEntity(a);
                    entityCommandBuffer.DestroyEntity(b);
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