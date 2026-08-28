using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
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
        private ComponentLookup<MinionData> _minionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Engagement>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _healthLookup = state.GetComponentLookup<Health>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _minionLookup = state.GetComponentLookup<MinionData>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _minionLookup.Update(ref state);

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
                HealthLookup = _healthLookup,
                MinionLookup = _minionLookup,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged)
            };
            state.Dependency = applyJob.Schedule(state.Dependency);
        }
    }

    /// <summary>One landed attack, waiting to be applied.</summary>
    public struct DamageEvent
    {
        /// <summary>
        /// Who swung. Carried so the apply pass can drop the blow if this attacker was already
        /// killed earlier in the same drain — see <see cref="ApplyDamageJob"/>.
        /// </summary>
        public Entity Attacker;

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
            Entity entity,
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
                Attacker = entity,
                Target = target,
                Amount = stats.ValueRO.Damage
            });

            cooldown.ValueRW.Remaining = stats.ValueRO.Interval;
        }
    }

    /// <summary>
    /// Applies every reported hit. Single-threaded on purpose — this is the one place Health is
    /// written, so concurrent attackers on the same target simply queue up behind each other.
    ///
    /// Being single-threaded is also what lets a fight have a winner. Attacks are all *decided*
    /// before any are *applied*, so two evenly matched minions both swing on the frame either of
    /// them dies. Resolving them one at a time, and dropping the blow of an attacker that is
    /// already dead by the time its turn comes up, is what leaves someone standing to walk on.
    ///
    /// It is also the only code in the project that knows a blow was *lethal* and who threw it, so
    /// it is where <see cref="KillEvent"/> is reported. DeathSystem sees the corpse but not the
    /// killer; the report pass sees the killer but not whether the blow landed last. Bounty
    /// arithmetic itself stays out of here — this states the fact, BountySystem decides what it is
    /// worth.
    /// </summary>
    [BurstCompile]
    public struct ApplyDamageJob : IJob
    {
        public NativeQueue<DamageEvent> DamageEvents;
        public ComponentLookup<Health> HealthLookup;

        /// <summary>Teams, for attributing a kill. A victim with no MinionData earns no bounty.</summary>
        [ReadOnly] public ComponentLookup<MinionData> MinionLookup;

        public EntityCommandBuffer Ecb;

        public void Execute()
        {
            while (DamageEvents.TryDequeue(out var damage))
            {
                if (!HealthLookup.HasComponent(damage.Target)) continue;

                // Killed earlier in this same drain — the corpse does not get its last word.
                // Attackers with no Health at all (a future tower, say) are always allowed.
                if (HealthLookup.HasComponent(damage.Attacker) &&
                    HealthLookup[damage.Attacker].Current <= 0)
                {
                    continue;
                }

                var health = HealthLookup[damage.Target];

                // Captured before the subtraction. Several attackers can queue damage onto a target
                // that the first of them kills, so "reached zero" is not the same question as "is
                // at zero" — without this the second swing would report a second kill on a corpse.
                bool wasAlive = health.Current > 0;

                // Clamped at zero so overkill cannot wrap into a negative that later reads as
                // "alive" if anything ever compares against a specific value instead of <= 0.
                health.Current = math.max(0, health.Current - damage.Amount);
                HealthLookup[damage.Target] = health;

                if (!wasAlive || health.Current > 0) continue;

                ReportKill(damage);
            }
        }

        /// <summary>
        /// Records who killed whom, by team.
        ///
        /// Both sides must resolve or nothing is reported. TeamNumber's zero value is a legitimate
        /// team (Team1), so an unresolved lookup cannot be represented as "no team" — guessing here
        /// would quietly pay the wrong corner rather than fail visibly.
        ///
        /// A base is not a minion, so a corner falling produces no kill event. That is deliberate:
        /// BaseDestructionSystem owns that event, and it means far more than a bounty.
        /// </summary>
        private void ReportKill(in DamageEvent damage)
        {
            if (!MinionLookup.TryGetComponent(damage.Target, out var victim)) return;
            if (!MinionLookup.TryGetComponent(damage.Attacker, out var killer)) return;
            if (killer.TeamNumber == victim.TeamNumber) return;

            var kill = Ecb.CreateEntity();
            Ecb.AddComponent(kill, new KillEvent
            {
                KillerTeam = killer.TeamNumber,
                VictimTeam = victim.TeamNumber
            });
        }
    }
}
