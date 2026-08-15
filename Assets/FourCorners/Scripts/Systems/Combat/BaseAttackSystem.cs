using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Lets minions hit an enemy corner they are walking past.
    ///
    /// Deliberately NOT built on <see cref="Engagement"/>, unlike minion-versus-minion combat.
    /// Engagement stops a minion dead, and the design is that a minion attacks the base in its
    /// sight *and keeps walking* — it tours every enemy corner and returns home. Stopping at the
    /// first base it met would end the lane loop there.
    ///
    /// A minion already locked in a fight is skipped: one attack per cooldown, and the enemy in
    /// its face takes priority over the building behind them.
    ///
    /// Target selection is a linear scan over corners rather than a spatial query — there are
    /// exactly <see cref="Teams.Count"/> of them, and a broadphase would cost more than it saves.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BaseAttackSystem : ISystem
    {
        private ComponentLookup<Health> _healthLookup;
        private EntityQuery _baseQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>();

            _baseQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerBase, LocalTransform, Health>()
                .Build(ref state);

            state.RequireForUpdate(_baseQuery);
            state.RequireForUpdate<MinionData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);

            var baseEntities = _baseQuery.ToEntityArray(state.WorldUpdateAllocator);
            var baseData = _baseQuery.ToComponentDataArray<PlayerBase>(state.WorldUpdateAllocator);
            var baseTransforms = _baseQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);

            var damageEvents = new NativeQueue<DamageEvent>(state.WorldUpdateAllocator);

            var reportJob = new ReportBaseDamageJob
            {
                BaseEntities = baseEntities,
                Bases = baseData,
                BaseTransforms = baseTransforms,
                DamageWriter = damageEvents.AsParallelWriter()
            };
            state.Dependency = reportJob.ScheduleParallel(state.Dependency);

            // Same apply pass as minion combat: many minions share one base, so the write has to
            // funnel through a single thread.
            var applyJob = new ApplyDamageJob
            {
                DamageEvents = damageEvents,
                HealthLookup = _healthLookup
            };
            state.Dependency = applyJob.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    [WithNone(typeof(Engagement))]
    public partial struct ReportBaseDamageJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Entity> BaseEntities;
        [ReadOnly] public NativeArray<PlayerBase> Bases;
        [ReadOnly] public NativeArray<LocalTransform> BaseTransforms;

        public NativeQueue<DamageEvent>.ParallelWriter DamageWriter;

        private void Execute(
            Entity entity,
            RefRO<MinionData> minion,
            RefRO<AttackStats> stats,
            RefRW<AttackCooldown> cooldown,
            RefRO<LocalTransform> transform)
        {
            if (cooldown.ValueRO.Remaining > 0f) return;

            float range = stats.ValueRO.Range;
            float rangeSq = range * range;

            for (int i = 0; i < BaseEntities.Length; i++)
            {
                var target = Bases[i];

                // An unclaimed or already-destroyed corner is not a target. Without the IsActive
                // check, minions would grind down empty corners nobody is playing.
                if (!target.IsActive) continue;
                if (target.TeamNumber == minion.ValueRO.TeamNumber) continue;

                if (math.distancesq(transform.ValueRO.Position, BaseTransforms[i].Position) > rangeSq) continue;

                DamageWriter.Enqueue(new DamageEvent
                {
                    Attacker = entity,
                    Target = BaseEntities[i],
                    Amount = stats.ValueRO.Damage
                });

                cooldown.ValueRW.Remaining = stats.ValueRO.Interval;

                // One swing per cooldown, even where two corners overlap in range.
                return;
            }
        }
    }
}
