using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Starts a fight when two enemy minions touch.
    ///
    /// This used to destroy both of them outright, which is why "whoever survives continues the
    /// walk" never happened — nobody ever survived. Contact now only *acquires* a target;
    /// AttackSystem resolves the fight and EngagementSystem ends it.
    ///
    /// It lives in PhysicsSystemGroup because collision events are only valid there. Everything
    /// else about combat is ordinary simulation and lives in Systems/Combat.
    ///
    /// Acquisition by contact rather than by range search is deliberate: the design is a duel
    /// between minions that have walked into each other, so the broadphase has already done the
    /// work. Towers and area spells will need a real spatial query; minions do not.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct EngagementAcquisitionSystem : ISystem
    {
        private const int ExpectedCollisionsPerFrame = 128;

        private ComponentLookup<MinionData> _minionLookup;
        private ComponentLookup<Engagement> _engagementLookup;
        private ComponentLookup<Health> _healthLookup;
        private NativeHashSet<Entity> _processedEntities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SimulationSingleton>();

            _minionLookup = state.GetComponentLookup<MinionData>(true);
            _engagementLookup = state.GetComponentLookup<Engagement>(true);
            _healthLookup = state.GetComponentLookup<Health>(true);
            _processedEntities = new NativeHashSet<Entity>(ExpectedCollisionsPerFrame, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _minionLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _healthLookup.Update(ref state);

            // Last frame's job owns _processedEntities until it finishes. Clearing it from the
            // main thread without completing first is a race — and a hard throw with the Jobs
            // Debugger enabled.
            state.Dependency.Complete();
            _processedEntities.Clear();

            var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            var job = new EngagementAcquisitionJob
            {
                MinionLookup = _minionLookup,
                EngagementLookup = _engagementLookup,
                HealthLookup = _healthLookup,
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
    public struct EngagementAcquisitionJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<MinionData> MinionLookup;
        [ReadOnly] public ComponentLookup<Engagement> EngagementLookup;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;

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

            if (MinionLookup[a].TeamNumber == MinionLookup[b].TeamNumber)
            {
                // Allies. Bumping into a friend who is mid-fight means joining it.
                //
                // Same-team contacts used to be discarded outright, which is why reinforcements
                // walked straight past a brawl: engaged minions are pinned in place, so an
                // arriving ally collides with *them* and never reaches the enemy behind them.
                TryAssist(a, b, collisionEvent.BodyIndexA);
                TryAssist(b, a, collisionEvent.BodyIndexB);
                return;
            }

            // Each side is decided independently, so a minion already fighting someone else keeps
            // its current target instead of being yanked onto whoever brushed past it last.
            TryEngage(a, b, collisionEvent.BodyIndexA);
            TryEngage(b, a, collisionEvent.BodyIndexB);
        }

        /// <summary>
        /// Puts <paramref name="helper"/> onto whatever <paramref name="ally"/> is fighting.
        /// </summary>
        private void TryAssist(Entity helper, Entity ally, int sortKey)
        {
            if (!EngagementLookup.TryGetComponent(ally, out var allyEngagement)) return;

            var target = allyEngagement.Target;

            // The ally's target is read through an end-of-frame ECB like every other engagement,
            // so it may already be dead. Piling onto a corpse would pin the helper in place until
            // EngagementSystem noticed and released it a frame later.
            if (!HealthLookup.TryGetComponent(target, out var targetHealth)) return;
            if (targetHealth.Current <= 0) return;

            TryEngage(helper, target, sortKey);
        }

        private void TryEngage(Entity attacker, Entity target, int sortKey)
        {
            // Already fighting — leave it alone.
            if (EngagementLookup.HasComponent(attacker)) return;

            // ProcessedEntities covers the same frame, which the lookup above cannot: the ECB
            // has not played back yet, so a minion hit by two enemies this frame would otherwise
            // be assigned both and keep whichever command happened to sort last.
            if (!ProcessedEntities.Add(attacker)) return;

            EntityCommandBuffer.AddComponent(sortKey, attacker, new Engagement { Target = target });
        }
    }
}
