using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Collision.Jobs
{
    public struct CollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<ElementData> ElementLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;

            if (!ElementLookup.HasComponent(a) && !ElementLookup.HasComponent(b))
            {
                return;
            }

            var dataA = ElementLookup[a];
            var dataB = ElementLookup[b];
            float3 position = 0.5f * (LocalTransformLookup[a].Position + LocalTransformLookup[b].Position);

            if (dataA.Type == dataB.Type)
            {
                Entity requestEntity = EntityCommandBuffer.CreateEntity(0);
                EntityCommandBuffer.AddBuffer<ElementSpawnRequest>(0, requestEntity)
                    .Add(new ElementSpawnRequest
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