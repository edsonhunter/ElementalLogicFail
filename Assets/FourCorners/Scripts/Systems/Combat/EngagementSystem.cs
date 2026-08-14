using FourCorners.Scripts.Components.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Maintains fights that CollisionSystem started: releases the ones that are over, and holds
    /// the combatants still while they last.
    ///
    /// Acquisition lives in CollisionSystem because it needs physics contact events and therefore
    /// has to run inside PhysicsSystemGroup. Everything after that is ordinary simulation, so it
    /// lives here — one system to start a fight, one to end it.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EngagementSystem : ISystem
    {
        /// <summary>
        /// Fights break at a multiple of attack range, not at attack range itself.
        ///
        /// Combatants are dynamic physics bodies pressed against each other; without that gap
        /// the smallest contact impulse would push them a hair past the threshold, release them,
        /// let PathFollowSystem walk them back into contact, and re-engage — a duel that stutters
        /// forward instead of resolving.
        /// </summary>
        private const float BreakRangeMultiplier = 2f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<Engagement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            var releaseJob = new ReleaseEngagementJob
            {
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                BreakRangeMultiplier = BreakRangeMultiplier,
                Ecb = ecb
            };
            state.Dependency = releaseJob.ScheduleParallel(state.Dependency);

            var holdJob = new HoldEngagedStillJob();
            state.Dependency = holdJob.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// Drops <see cref="Engagement"/> once the target is gone, dead, or out of reach — which is
    /// what lets the survivor resume its lane.
    /// </summary>
    [BurstCompile]
    public partial struct ReleaseEngagementJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

        public float BreakRangeMultiplier;
        public EntityCommandBuffer.ParallelWriter Ecb;

        private void Execute(
            Entity entity,
            [EntityIndexInQuery] int sortKey,
            RefRO<Engagement> engagement,
            RefRO<AttackStats> stats,
            RefRO<LocalTransform> transform)
        {
            var target = engagement.ValueRO.Target;

            if (ShouldRelease(target, transform.ValueRO.Position, stats.ValueRO.Range))
            {
                Ecb.RemoveComponent<Engagement>(sortKey, entity);
            }
        }

        private bool ShouldRelease(Entity target, float3 position, float range)
        {
            // Anything we cannot resolve is treated as gone. Failing open would strand the
            // attacker mid-lane forever, which is a far worse failure than one dropped fight.
            if (!HealthLookup.HasComponent(target)) return true;
            if (HealthLookup[target].Current <= 0) return true;
            if (!TransformLookup.TryGetComponent(target, out var targetTransform)) return true;

            float breakRange = range * BreakRangeMultiplier;
            return math.distancesq(position, targetTransform.Position) > breakRange * breakRange;
        }
    }

    /// <summary>
    /// Pins engaged combatants in place.
    ///
    /// PathFollowSystem stops writing their position the moment they engage, which hands them
    /// back to physics — and two overlapping dynamic bodies immediately shove each other apart.
    /// Zeroing velocity every frame is what makes them actually stand and fight.
    ///
    /// Entities without a PhysicsVelocity — a base, a tower — simply do not match this query.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Engagement))]
    public partial struct HoldEngagedStillJob : IJobEntity
    {
        private void Execute(ref PhysicsVelocity velocity)
        {
            velocity = default;
        }
    }
}
