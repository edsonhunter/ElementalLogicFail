using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Jobs
{
    public struct CollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<ElementData> ElementLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<Spawner> SpawnerLookup;
        
        public BufferLookup<ElementSpawnRequest> BufferLookup;
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;

            if (!ElementLookup.HasComponent(a) && !ElementLookup.HasComponent(b))
            {
                return;
            }

            ElementType typeA = ElementLookup[a].Type;
            ElementType typeB = ElementLookup[b].Type;
            float3 position = 0.5f * (LocalTransformLookup[a].Position + LocalTransformLookup[b].Position);

            if (typeA == typeB)
            {
                if (SpawnerLookup.HasComponent(a))
                {
                    if (SpawnerLookup[a].Type == typeA && BufferLookup.HasBuffer(a))
                    {
                        BufferLookup[a].Add(new ElementSpawnRequest
                        {
                            Type = typeA,
                            Position = position
                        });
                    }
                }
            }
            else
            {
                EntityCommandBuffer.DestroyEntity(0, a);
                EntityCommandBuffer.DestroyEntity(0, b);
            }
        }
    }
}