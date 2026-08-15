using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Spawner
{
    /// <summary>
    /// Cleans up after a corner that has stopped playing: silences its spawners and clears its
    /// units off the field.
    ///
    /// There are two ways a corner retires — its owner disconnects, or its base is destroyed — and
    /// both used to carry their own copy of this cleanup. That duplication was already drifting,
    /// and Tier 0.3 changes what a disconnect means, which would have driven the two copies
    /// further apart. Retiring a corner is now one fact (<see cref="PlayerBase.IsActive"/> going
    /// false while <see cref="ActiveCorner"/> is present) with one reaction, and callers only have
    /// to state the fact.
    ///
    /// Idempotent by construction: ActiveCorner is removed as part of the teardown, so a corner is
    /// only ever cleaned up once no matter how many frames it stays inactive.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // Load-bearing, unusually for this project: both systems record ActiveCorner changes into the
    // same end-of-frame ECB, and playback follows recording order. Should a corner ever be retired
    // and reclaimed on one frame, recording the removal first leaves the re-added marker intact —
    // the other order would silently strip it from a live corner and disable its future teardown.
    [UpdateBefore(typeof(BaseAllocationSystem))]
    public partial struct CornerTeardownSystem : ISystem
    {
        private EntityQuery _retiredQuery;
        private EntityQuery _spawnerQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            _retiredQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerBase, ActiveCorner>()
                .Build(ref state);
            state.RequireForUpdate(_retiredQuery);

            _spawnerQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpawnerData>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var retiredTeams = new NativeList<TeamNumber>(Teams.Count, Allocator.Temp);

            foreach (var (playerBase, entity) in
                     SystemAPI.Query<RefRO<PlayerBase>>().WithAll<ActiveCorner>().WithEntityAccess())
            {
                if (playerBase.ValueRO.IsActive) continue;

                retiredTeams.Add(playerBase.ValueRO.TeamNumber);
                ecb.RemoveComponent<ActiveCorner>(entity);

                UnityEngine.Debug.Log(
                    $"[CornerTeardownSystem] Corner {playerBase.ValueRO.TeamNumber} retired — " +
                    "silencing spawners and clearing its units.");
            }

            if (retiredTeams.IsEmpty)
            {
                retiredTeams.Dispose();
                return;
            }

            SilenceSpawners(ref state, retiredTeams);
            ClearMinions(ref state, retiredTeams, ecb);

            retiredTeams.Dispose();
        }

        /// <summary>
        /// SpawnerSystem gates on PlayerBase.IsActive, so this is the derived mirror being kept
        /// honest rather than the thing that actually stops production. Leaving it stale would
        /// mislead anyone reading it.
        /// </summary>
        private void SilenceSpawners(ref SystemState state, NativeList<TeamNumber> retiredTeams)
        {
            state.CompleteDependency();

            var baseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: true);
            var spawnerLookup = SystemAPI.GetComponentLookup<SpawnerData>(isReadOnly: false);

            using var spawners = _spawnerQuery.ToEntityArray(Allocator.Temp);

            foreach (var spawnerEntity in spawners)
            {
                if (!spawnerLookup.TryGetComponent(spawnerEntity, out var spawnerData)) continue;
                if (!baseLookup.TryGetComponent(spawnerData.PlayerBaseEntity, out var owningBase)) continue;
                if (!Contains(retiredTeams, owningBase.TeamNumber)) continue;

                spawnerData.IsActive = false;
                spawnerData.NetworkId = 0;
                spawnerData.Timer = 0f;
                spawnerLookup[spawnerEntity] = spawnerData;
            }
        }

        private void ClearMinions(
            ref SystemState state,
            NativeList<TeamNumber> retiredTeams,
            EntityCommandBuffer ecb)
        {
            foreach (var (minion, minionEntity) in
                     SystemAPI.Query<RefRO<MinionData>>().WithEntityAccess())
            {
                if (Contains(retiredTeams, minion.ValueRO.TeamNumber))
                {
                    ecb.DestroyEntity(minionEntity);
                }
            }
        }

        private static bool Contains(NativeList<TeamNumber> teams, TeamNumber team)
        {
            for (int i = 0; i < teams.Length; i++)
            {
                if (teams[i] == team) return true;
            }

            return false;
        }
    }
}
