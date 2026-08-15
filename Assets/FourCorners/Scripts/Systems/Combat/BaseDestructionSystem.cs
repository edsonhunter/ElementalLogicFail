using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Turns a corner whose health has run out into an eliminated corner.
    ///
    /// This is the counterpart to DeathSystem for the one thing that must NOT be destroyed. The
    /// base entity survives: it is a ghost carrying the team's identity, and the eliminated player
    /// is still connected and still watching. Only its *participation* ends — the building goes
    /// dark, its spawners stop, and its minions leave the field.
    ///
    /// The slot is left occupied. Freeing it would hand a dead corner to the next player to
    /// connect, which is both unfair and confusing; ServerDisconnectSystem is the only thing that
    /// frees a slot.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BaseDestructionSystem : ISystem
    {
        private EntityQuery _spawnerQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<MatchStateTag>();
            state.RequireForUpdate<PlayerBase>();

            _spawnerQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpawnerData>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: false);

            bool anyDestroyed = false;
            var destroyedTeams = new NativeList<TeamNumber>(Teams.Count, Allocator.Temp);

            foreach (var (playerBase, health) in
                     SystemAPI.Query<RefRW<PlayerBase>, RefRO<Health>>())
            {
                if (!playerBase.ValueRO.IsActive) continue;
                if (health.ValueRO.Current > 0) continue;

                var team = playerBase.ValueRO.TeamNumber;
                int networkId = playerBase.ValueRO.NetworkId;

                // NetworkId is kept, unlike a disconnect: the corner is dead but still belongs to
                // someone, and MatchOutcomeSystem needs to know who is out.
                playerBase.ValueRW.IsActive = false;

                int index = (int)team;
                if (index >= 0 && index < teamBuffer.Length)
                {
                    var slot = teamBuffer[index];
                    slot.IsEliminated = true;
                    teamBuffer[index] = slot;
                }

                destroyedTeams.Add(team);
                anyDestroyed = true;

                UnityEngine.Debug.Log(
                    $"[BaseDestructionSystem] Team {team} (NetworkId={networkId}) eliminated — base destroyed.");
            }

            if (!anyDestroyed)
            {
                destroyedTeams.Dispose();
                return;
            }

            DeactivateSpawners(ref state, destroyedTeams);
            DestroyMinions(ref state, destroyedTeams, ecb);

            destroyedTeams.Dispose();
        }

        /// <summary>
        /// Silences the dead corner's spawners. SpawnerSystem gates on PlayerBase.IsActive, so
        /// this is strictly the derived mirror being kept honest — but leaving it stale would
        /// mislead anyone reading it.
        /// </summary>
        private void DeactivateSpawners(ref SystemState state, NativeList<TeamNumber> destroyedTeams)
        {
            state.CompleteDependency();

            var baseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: true);
            var spawnerLookup = SystemAPI.GetComponentLookup<SpawnerData>(isReadOnly: false);

            using var spawners = _spawnerQuery.ToEntityArray(Allocator.Temp);

            foreach (var spawnerEntity in spawners)
            {
                if (!spawnerLookup.TryGetComponent(spawnerEntity, out var spawnerData)) continue;
                if (!baseLookup.TryGetComponent(spawnerData.PlayerBaseEntity, out var owningBase)) continue;
                if (!Contains(destroyedTeams, owningBase.TeamNumber)) continue;

                spawnerData.IsActive = false;
                spawnerData.NetworkId = 0;
                spawnerData.Timer = 0f;
                spawnerLookup[spawnerEntity] = spawnerData;
            }
        }

        /// <summary>Clears the field of a dead corner's units so it stops fighting from the grave.</summary>
        private void DestroyMinions(
            ref SystemState state,
            NativeList<TeamNumber> destroyedTeams,
            EntityCommandBuffer ecb)
        {
            foreach (var (minion, minionEntity) in
                     SystemAPI.Query<RefRO<MinionData>>().WithEntityAccess())
            {
                if (Contains(destroyedTeams, minion.ValueRO.TeamNumber))
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
