using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Server-side handler for GoInGameRequest. Flow:
    ///   1. Read the MatchStateTag entity's DynamicBuffer&lt;TeamStatusElement&gt;.
    ///   2. Grant the client's desired team, else the first free slot.
    ///   3. No slot free → TeamRejectedRpc, drop the request.
    ///   4. On success: occupy the slot, add to the roster, elect a host if there isn't one,
    ///      move WaitingForPlayers → Lobby, and broadcast the new lobby state.
    ///   5. If the match is already Active, send that one client a MatchStartedRpc so a late
    ///      joiner walks straight into gameplay instead of waiting on a Start that cannot come.
    ///
    /// Joining is deliberately not gated on the phase: a corner freed by a disconnect must be
    /// grantable mid-match. "Match full" is the only rejection, and it is decided by ResolveTeam.
    ///
    /// NetworkStreamInGame is deliberately NOT added here — ghost streaming is deferred until
    /// the client reports its SubScenes are baked (ReadyForGhostsRequest).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerAcceptGameSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchStateTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<GoInGameRequest, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));

            var matchQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStateTag, TeamStatusElement, MatchState>();
            state.RequireForUpdate(state.GetEntityQuery(matchQuery));
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

            foreach (var (request, receive, rpcEntity) in
                     SystemAPI.Query<RefRO<GoInGameRequest>, RefRO<ReceiveRpcCommandRequest>>()
                         .WithEntityAccess())
            {
                var sourceConnection = receive.ValueRO.SourceConnection;
                ecb.DestroyEntity(rpcEntity);

                if (!state.EntityManager.Exists(sourceConnection))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ServerAcceptGameSystem] Connection {sourceConnection} no longer exists. Dropping request.");
                    continue;
                }

                int grantedTeam = ResolveTeam(teamBuffer, request.ValueRO.RequestedTeamIndex);

                if (grantedTeam == -1)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ServerAcceptGameSystem] All {Teams.Count} teams occupied. Rejecting connection {sourceConnection}.");
                    var rejectionRpc = ecb.CreateEntity();
                    ecb.AddComponent<TeamRejectedRpc>(rejectionRpc);
                    ecb.AddComponent(rejectionRpc, new SendRpcCommandRequest { TargetConnection = sourceConnection });
                    continue;
                }

                // Race is always honoured — races are not exclusive, only corners are.
                teamBuffer[grantedTeam] = new TeamStatusElement
                {
                    IsOccupied = true,
                    OccupyingPlayer = sourceConnection,
                    Race = request.ValueRO.RequestedRace
                };

                var networkId = SystemAPI.GetComponent<NetworkId>(sourceConnection);

                // Elect a host only when the seat is genuinely vacant, rather than assuming
                // "first player ever" — that assumption breaks once players can leave.
                bool isHost = matchState.HostNetworkId == 0;
                if (isHost)
                {
                    ecb.AddComponent<HostTag>(sourceConnection);
                    matchState.HostNetworkId = networkId.Value;
                    UnityEngine.Debug.Log($"[ServerAcceptGameSystem] HostTag assigned to NetworkId={networkId.Value}.");
                }

                if (matchState.Phase == MatchPhase.WaitingForPlayers)
                    matchState.Phase = MatchPhase.Lobby;

                playerBuffer.Add(new ConnectedPlayerElement
                {
                    NetworkId = networkId.Value,
                    ConnectionEntity = sourceConnection
                });

                // A player accepted after the match started missed HostStartGameSystem's one-shot
                // broadcast — that loop is a snapshot of the roster at the instant Start was
                // pressed, and it can never run again because it gates on Phase == Lobby. Send
                // them their own copy so the standard client pipeline (MatchStartedTag →
                // OnMatchStarted → GameplayScene → ReadyForGhostsRequest → PendingBaseAllocation)
                // runs unchanged. Without this they sit in the lobby holding a corner forever.
                if (matchState.Phase == MatchPhase.Active)
                {
                    var matchStarted = ecb.CreateEntity();
                    ecb.AddComponent<MatchStartedRpc>(matchStarted);
                    ecb.AddComponent(matchStarted, new SendRpcCommandRequest { TargetConnection = sourceConnection });

                    UnityEngine.Debug.Log(
                        $"[ServerAcceptGameSystem] NetworkId={networkId.Value} joined a running match — " +
                        "sending MatchStartedRpc directly.");
                }

                lobbyChanged = true;

                UnityEngine.Debug.Log(
                    $"[ServerAcceptGameSystem] Granted Team {grantedTeam} (race {request.ValueRO.RequestedRace}) " +
                    $"to NetworkId={networkId.Value} (isHost={isHost}). Total players: {playerBuffer.Length}.");
            }

            if (!lobbyChanged) return;

            // Written immediately, not through the ECB. ServerDisconnectSystem runs earlier in
            // the same frame and also does a read-modify-write of MatchState; if both deferred to
            // end-of-frame playback, a join landing on the same frame as a drop would replay a
            // pre-disconnect copy and silently resurrect the departed player's HostNetworkId.
            SystemAPI.SetComponent(matchStateEntity, matchState);
            LobbyBroadcast.SendToAll(ref ecb, playerBuffer, matchState.HostNetworkId);
        }

        /// <summary>Grants the requested slot if free, else the first free slot, else -1.</summary>
        private static int ResolveTeam(in DynamicBuffer<TeamStatusElement> teamBuffer, int desired)
        {
            if (desired >= 0 && desired < teamBuffer.Length && !teamBuffer[desired].IsOccupied)
                return desired;

            for (int i = 0; i < teamBuffer.Length; i++)
            {
                if (!teamBuffer[i].IsOccupied) return i;
            }

            return -1;
        }
    }
}
