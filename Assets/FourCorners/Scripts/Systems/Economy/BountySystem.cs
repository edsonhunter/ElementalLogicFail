using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Economy
{
    /// <summary>
    /// Pays a corner for the kills its units landed.
    ///
    /// The other half of the economy the design asks for: passive income rewards surviving, bounty
    /// rewards winning fights. Without it a player who never contests the lane earns exactly as
    /// much as one who wins every engagement, and the whole minion loop stops mattering
    /// economically.
    ///
    /// The single consumer of <see cref="KillEvent"/>, so it destroys the events itself rather than
    /// needing the cleanup-system dance <c>BaseCommand</c> requires — that one exists only because
    /// several handlers may look at the same intent.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BountySystem : ISystem
    {
        /// <summary>
        /// Paid per enemy minion killed. Flat while every unit is an identical placeholder; it
        /// belongs in the balance blob alongside unit stats and building costs at Tier 3.2, at
        /// which point it becomes a property of what died rather than a constant.
        /// </summary>
        private const int BountyPerKill = 10;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<KillEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Four entries, rebuilt each frame this system runs at all. Cheaper and simpler than
            // keeping a persistent map in sync with corners being claimed and destroyed.
            var earnedByTeam = new NativeArray<int>(Teams.Count, Allocator.Temp);

            foreach (var (kill, killEntity) in
                     SystemAPI.Query<RefRO<KillEvent>>().WithEntityAccess())
            {
                ecb.DestroyEntity(killEntity);

                int team = (int)kill.ValueRO.KillerTeam;
                if (team < 0 || team >= earnedByTeam.Length) continue;

                earnedByTeam[team] += BountyPerKill;
            }

            // Accumulated first, then applied once per corner. A busy frame can hold dozens of
            // kills, and this way a corner's purse is touched once regardless.
            foreach (var (economy, owningBase) in
                     SystemAPI.Query<RefRW<PlayerEconomy>, RefRO<PlayerBase>>())
            {
                // A corner that fell this frame collects nothing. Its minions may still have been
                // killing on the way down, but there is no longer anyone to pay.
                if (!owningBase.ValueRO.IsActive) continue;

                int team = (int)owningBase.ValueRO.TeamNumber;
                if (team < 0 || team >= earnedByTeam.Length) continue;
                if (earnedByTeam[team] == 0) continue;

                economy.ValueRW.Gold += earnedByTeam[team];
            }

            earnedByTeam.Dispose();
        }
    }
}
