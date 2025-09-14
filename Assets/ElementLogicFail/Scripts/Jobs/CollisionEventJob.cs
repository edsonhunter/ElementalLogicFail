using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
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
        
        public BufferLookup<ElementSpawnRequest> SpawnBufferLookup;
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;
            bool aIsElement = ElementLookup.HasComponent(a);
            bool bIsElement = ElementLookup.HasComponent(b);

            if (!aIsElement && !bIsElement)
            {
                return;
            }
                
            ElementType typeA = ElementLookup[a].Type;
            ElementType typeB = ElementLookup[b].Type;
            float3 position = 0.5f * (LocalTransformLookup[a].Position + LocalTransformLookup[b].Position);

            if (typeA == typeB)
            {
                if (SpawnBufferLookup.HasBuffer(a))
                {
                    DynamicBuffer<ElementSpawnRequest> buffer = SpawnBufferLookup[a];
                    buffer.Add(new ElementSpawnRequest
                    {
                        Type = typeA,
                        Position = position,
                    });
                }
                else if (SpawnBufferLookup.HasBuffer(b))
                {
                    var buffer = SpawnBufferLookup[b];
                    buffer.Add(new ElementSpawnRequest
                    {
                        Type = typeB,
                        Position = position,
                    });
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