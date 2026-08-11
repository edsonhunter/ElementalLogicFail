using FourCorners.Scripts.Components.Minion;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace FourCorners.Scripts.Systems.Collision
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct CollisionSystem : ISystem
    {
        private const int ExpectedCollisionsPerFrame = 128;

        private ComponentLookup<MinionData> _minionLookup;
        private NativeHashSet<Entity> _processedEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SimulationSingleton>();

            _minionLookup = state.GetComponentLookup<MinionData>(true);
            _processedEntities = new NativeHashSet<Entity>(ExpectedCollisionsPerFrame, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _minionLookup.Update(ref state);

            // Last frame's job owns _processedEntities until it finishes. Clearing it from the
            // main thread without completing first is a race — and a hard throw with the Jobs
            // Debugger enabled.
            state.Dependency.Complete();
            _processedEntities.Clear();

            var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            var job = new CollisionEventJob
            {
                MinionLookup = _minionLookup,
                EntityCommandBuffer = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                ProcessedEntities = _processedEntities
            };

            state.Dependency = job.Schedule(simulation, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_processedEntities.IsCreated)
                _processedEntities.Dispose();
        }
    }
    
    [BurstCompile]
    public struct CollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<MinionData> MinionLookup;
        public NativeHashSet<Entity> ProcessedEntities;
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;

            if (!MinionLookup.HasComponent(a) || !MinionLookup.HasComponent(b))
            {
                return;
            }
            
            var dataA = MinionLookup[a];
            var dataB = MinionLookup[b];

            if (dataA.TeamNumber == dataB.TeamNumber)
            {
                return;
            }

            bool canDisableA = !ProcessedEntities.Contains(a);
            bool canDisableB = !ProcessedEntities.Contains(b);

            if (!canDisableA && !canDisableB)
            {
                return;
            }

            if (canDisableA)
            {
                AppendEntityRequest(a, collisionEvent.BodyIndexA);
            }

            if (canDisableB)
            {
                AppendEntityRequest(b, collisionEvent.BodyIndexB);
            }
        }

        private void AppendEntityRequest(Entity entity, int sortKey)
        {
            ProcessedEntities.Add(entity);
            EntityCommandBuffer.DestroyEntity(sortKey, entity);
        }
    }
}
