using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Releases everything a departing player owned.
    ///
    /// Without this, a drop leaked permanently: the team slot stayed occupied so the corner
    /// could never be reused (not even by the same player rejoining), the roster never shrank
    /// so the lobby count only ever grew, and the host seat was never vacated so no one could
    /// start another match.
    ///
    /// Runs before ServerAcceptGameSystem so a slot freed this frame is immediately grantable.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ServerAcceptGameSystem))]
    public partial struct ServerDisconnectSystem : ISystem
    {
        private EntityQuery _disconnectedQuery;
        private EntityQuery _baseQuery;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchStateTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            // NetCode keeps the connection entity alive for one frame in the Disconnected state
            // precisely so gameplay code can clean up after it.
            _disconnectedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ConnectionState>()
                .Build(ref state);
            state.RequireForUpdate(_disconnectedQuery);

            _baseQuery = state.GetEntityQuery(ComponentType.ReadWrite<PlayerBase>());

        }

        public void OnUpdate(ref SystemState state)
        {
            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: false);
            var playerBuffer = SystemAPI.GetSingletonBuffer<ConnectedPlayerElement>(isReadOnly: false);
            var matchStateEntity = SystemAPI.GetSingletonEntity<MatchStateTag>();
            var matchState = SystemAPI.GetComponent<MatchState>(matchStateEntity);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            bool lobbyChanged = false;

            foreach (var (connectionState, connectionEntity) in
                     SystemAPI.Query<RefRO<ConnectionState>>().WithEntityAccess())
            {
                if (connectionState.ValueRO.CurrentState != ConnectionState.State.Disconnected)
                    continue;

                int networkId = connectionState.ValueRO.NetworkId;

                ReleaseTeamSlot(ref state, teamBuffer, connectionEntity);
                RemoveFromRoster(playerBuffer, connectionEntity);

                // ConnectionState is ICleanupComponentData: the entity outlives the connection
                // until we remove it. Skipping this leaks an entity per disconnect and makes
                // this system reprocess the same drop every frame.
                ecb.RemoveComponent<ConnectionState>(connectionEntity);

                // Vacate the host seat so the next remaining player can be promoted.
                if (matchState.HostNetworkId == networkId)
                    matchState.HostNetworkId = 0;

                lobbyChanged = true;

                UnityEngine.Debug.Log(
                    $"[ServerDisconnectSystem] NetworkId={networkId} disconnected " +
                    $"(reason: {connectionState.ValueRO.DisconnectReason}). " +
                    $"Slot released. {playerBuffer.Length} player(s) remain.");
            }

            if (!lobbyChanged) return;

            // Promote a replacement host from whoever is left.
            if (matchState.HostNetworkId == 0 && !playerBuffer.IsEmpty)
            {
                matchState.HostNetworkId = playerBuffer[0].NetworkId;
                ecb.AddComponent<HostTag>(playerBuffer[0].ConnectionEntity);
                UnityEngine.Debug.Log(
                    $"[ServerDisconnectSystem] Host re-elected: NetworkId={matchState.HostNetworkId}.");
            }

            // Empty lobby: reset so the next player to arrive starts a clean match.
            if (playerBuffer.IsEmpty)
            {
                matchState.Phase = MatchPhase.WaitingForPlayers;
                UnityEngine.Debug.Log("[ServerDisconnectSystem] Lobby empty — match reset to WaitingForPlayers.");
            }

            // Written immediately, not through the ECB. ServerAcceptGameSystem runs later in the
            // same frame and does its own read-modify-write of MatchState; deferring both to
            // end-of-frame playback let a same-frame join replay a pre-disconnect copy over the
            // host-vacate and phase reset below.
            SystemAPI.SetComponent(matchStateEntity, matchState);
            LobbyBroadcast.SendToAll(ref ecb, playerBuffer, matchState.HostNetworkId);
        }

        /// <summary>
        /// Frees the team slot and deactivates the corner it owned.
        ///
        /// Deactivating is the whole job here. Silencing the spawners and clearing the field is
        /// CornerTeardownSystem's, which reacts to any corner going inactive — so a corner
        /// abandoned by a disconnect and one flattened in combat are cleaned up by the same code
        /// rather than by two copies that drift apart.
        ///
        /// BaseAllocationSystem's "not already active" guard then lets the corner be granted
        /// again, including to the same player rejoining.
        /// </summary>
        /// <remarks>
        /// Takes `ref SystemState` because it uses SystemAPI: the source generator needs it to
        /// update component handles and complete dependencies (SGSG0002).
        /// </remarks>
        private void ReleaseTeamSlot(
            ref SystemState state,
            DynamicBuffer<TeamStatusElement> teamBuffer,
            Entity connectionEntity)
        {
            int releasedTeam = -1;

            for (int i = 0; i < teamBuffer.Length; i++)
            {
                if (!teamBuffer[i].IsOccupied || teamBuffer[i].OccupyingPlayer != connectionEntity)
                    continue;

                teamBuffer[i] = new TeamStatusElement { IsOccupied = false, OccupyingPlayer = Entity.Null };
                releasedTeam = i;
                break;
            }

            if (releasedTeam == -1) return;

            state.CompleteDependency();
            var team = (TeamNumber)releasedTeam;
            var baseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: false);

            using var bases = _baseQuery.ToEntityArray(Allocator.Temp);

            foreach (var candidate in bases)
            {
                if (!baseLookup.TryGetComponent(candidate, out var baseData)) continue;
                if (baseData.TeamNumber != team) continue;

                baseData.IsActive = false;
                baseData.NetworkId = 0;
                baseLookup[candidate] = baseData;
                break;
            }
        }

        /// <summary>Removes the player's roster entry. Order is not meaningful, so swap-back.</summary>
        private static void RemoveFromRoster(
            DynamicBuffer<ConnectedPlayerElement> playerBuffer,
            Entity connectionEntity)
        {
            for (int i = playerBuffer.Length - 1; i >= 0; i--)
            {
                if (playerBuffer[i].ConnectionEntity == connectionEntity)
                    playerBuffer.RemoveAtSwapBack(i);
            }
        }
    }
}
