using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Systems.Building;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Economy
{
    /// <summary>
    /// Pays every living corner its passive income.
    ///
    /// Gated on the corner being active, which quietly gives two behaviours the design wants for
    /// free. An eliminated player stops earning, because their base went inactive. And a player who
    /// merely disconnected keeps earning, because theirs did not — they come back to a corner that
    /// has been working for them, which is the whole point of leaving it standing.
    ///
    /// The match phase gate matters as much: without it, four corners would spend the entire lobby
    /// accumulating gold and the match would open with everyone able to buy everything at once.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct IncomeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchState>();
            state.RequireForUpdate<PlayerEconomy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<MatchState>().Phase != MatchPhase.Active) return;

            var job = new AccrueIncomeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                BuildingLookup = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true)
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct AccrueIncomeJob : IJobEntity
    {
        public float DeltaTime;

        /// <summary>
        /// The central building, which lives on this very entity. Read through a lookup rather than
        /// taken as a query parameter so that a base which somehow lacks one still earns its
        /// authored income instead of silently earning nothing at all.
        /// </summary>
        [ReadOnly] public ComponentLookup<BuildingData> BuildingLookup;

        private void Execute(Entity entity, ref PlayerEconomy economy, RefRO<PlayerBase> owningBase)
        {
            if (!owningBase.ValueRO.IsActive) return;

            int level = BuildingLookup.TryGetComponent(entity, out var central)
                ? central.Level
                : 0;

            // Recomputed every tick rather than written once at purchase. The upgrade handler
            // touches only BuildingData.Level, so this is the single place the rate is decided and
            // there is no second copy to drift. It is also the replicated value, so the HUD shows
            // what is actually being paid.
            economy.IncomePerSecond = BuildingUpgrade.EffectiveIncome(economy.BaseIncomePerSecond, level);

            if (economy.IncomePerSecond <= 0) return;

            economy.Accrued += economy.IncomePerSecond * DeltaTime;

            // Only whole coins are banked; the remainder rides along to the next frame. Truncating
            // per frame instead would floor a rate of 5/sec to zero at every frame rate anyone
            // actually runs at, and the player would earn nothing while the numbers looked right.
            int earned = (int)economy.Accrued;
            if (earned <= 0) return;

            economy.Gold += earned;
            economy.Accrued -= earned;
        }
    }
}
