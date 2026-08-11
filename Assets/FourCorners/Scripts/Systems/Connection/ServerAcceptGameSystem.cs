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

                // Observe drops for this connection. Without ConnectionState, NetCode gives us no
                // disconnect signal at all, and the team slot above would leak permanently.
                ecb.AddComponent(sourceConnection, new ConnectionState());

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

                lobbyChanged = true;

                UnityEngine.Debug.Log(
                    $"[ServerAcceptGameSystem] Granted Team {grantedTeam} (race {request.ValueRO.RequestedRace}) " +
                    $"to NetworkId={networkId.Value} (isHost={isHost}). Total players: {playerBuffer.Length}.");
            }

            if (!lobbyChanged) return;

            ecb.SetComponent(matchStateEntity, matchState);
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
