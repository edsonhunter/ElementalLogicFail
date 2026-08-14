using FourCorners.Scripts.Components.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Resolves attacks between engaged entities.
    ///
    /// Two phases, and the split is load-bearing. Several attackers can share one target, so
    /// writing Health straight from a parallel job would be a race on the same component. Instead
    /// the parallel pass only *reports* damage into a queue, and a second single-threaded pass
    /// applies it. That keeps the expensive per-attacker work parallel without a
    /// CompleteDependency stall or a NativeDisableParallelForRestriction lie.
    ///
    /// No [UpdateAfter] on EngagementSystem: it releases stale engagements through the
    /// EndSimulation ECB, so its removals never land in the same frame regardless of order.
    /// The guards below are what actually stop an attack on a corpse — ordering could not.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AttackSystem : ISystem
    {
        private ComponentLookup<Health> _healthLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Engagement>();

            _healthLookup = state.GetComponentLookup<Health>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _transformLookup.Update(ref state);

            // WorldUpdateAllocator: freed automatically at the end of the frame, so there is no
            // Dispose to forget and no persistent allocation for a per-frame scratch buffer.
            var damageEvents = new NativeQueue<DamageEvent>(state.WorldUpdateAllocator);

            var reportJob = new ReportDamageJob
            {
                HealthLookup = _healthLookup,
                TransformLookup = _transformLookup,
                DamageWriter = damageEvents.AsParallelWriter()
            };
            state.Dependency = reportJob.ScheduleParallel(state.Dependency);

            var applyJob = new ApplyDamageJob
            {
                DamageEvents = damageEvents,
                HealthLookup = _healthLookup
            };
            state.Dependency = applyJob.Schedule(state.Dependency);
        }
    }

    /// <summary>One landed attack, waiting to be applied.</summary>
    public struct DamageEvent
    {
        public Entity Target;
        public int Amount;
    }

    /// <summary>
    /// Decides who lands a hit this frame. Reads health, never writes it.
    /// </summary>
    [BurstCompile]
    public partial struct ReportDamageJob : IJobEntity
    {
        // Same lookup the apply pass writes through, but flagged read-only here so the safety
        // system knows this parallel pass only reads it. The two jobs are chained, never
        // concurrent.
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

        public NativeQueue<DamageEvent>.ParallelWriter DamageWriter;

        private void Execute(
            RefRO<Engagement> engagement,
            RefRO<AttackStats> stats,
            RefRW<AttackCooldown> cooldown,
            RefRO<LocalTransform> transform)
        {
            if (cooldown.ValueRO.Remaining > 0f) return;

            var target = engagement.ValueRO.Target;

            // Re-validated rather than trusted: Engagement is removed through an end-of-frame ECB,
            // so a target that died earlier this frame is still referenced here.
            if (!HealthLookup.HasComponent(target)) return;
            if (HealthLookup[target].Current <= 0) return;
            if (!TransformLookup.TryGetComponent(target, out var targetTransform)) return;

            float range = stats.ValueRO.Range;
            if (math.distancesq(transform.ValueRO.Position, targetTransform.Position) > range * range) return;

            DamageWriter.Enqueue(new DamageEvent
            {
                Target = target,
                Amount = stats.ValueRO.Damage
            });

            cooldown.ValueRW.Remaining = stats.ValueRO.Interval;
        }
    }

    /// <summary>
    /// Applies every reported hit. Single-threaded on purpose — this is the one place Health is
    /// written, so concurrent attackers on the same target simply queue up behind each other.
    /// </summary>
    [BurstCompile]
    public struct ApplyDamageJob : IJob
    {
        public NativeQueue<DamageEvent> DamageEvents;
        public ComponentLookup<Health> HealthLookup;

        public void Execute()
        {
            while (DamageEvents.TryDequeue(out var damage))
            {
                if (!HealthLookup.HasComponent(damage.Target)) continue;

                var health = HealthLookup[damage.Target];

                // Clamped at zero so overkill cannot wrap into a negative that later reads as
                // "alive" if anything ever compares against a specific value instead of <= 0.
                health.Current = math.max(0, health.Current - damage.Amount);
                HealthLookup[damage.Target] = health;
            }
        }
    }
}
