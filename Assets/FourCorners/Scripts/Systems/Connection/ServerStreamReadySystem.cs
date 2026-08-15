using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Server-side system that receives ReadyForGhostsRequest from a client
    /// whose subscenes have fully baked.
    ///
    /// It looks up the previously allocated team slot for that connection,
    /// appends PendingBaseAllocation so their base is spawned, and adds
    /// NetworkStreamInGame to officially start Ghost streaming for that client.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(ServerAcceptGameSystem))]
    public partial struct ServerStreamReadySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ReadyForGhostsRequest, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));

            // Requires MatchStateTag to exist so we can read TeamStatusElement buffer
            var matchQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStateTag, TeamStatusElement>();
            state.RequireForUpdate(state.GetEntityQuery(matchQuery));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: true);

            foreach (var (receive, rpcEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<ReadyForGhostsRequest>()
                         .WithEntityAccess())
            {
                var sourceConnection = receive.ValueRO.SourceConnection;
                ecb.DestroyEntity(rpcEntity);

                // Recover the slot granted in ServerAcceptGameSystem, along with the race the
                // player chose — recorded at accept time so we need no second client round trip.
                int grantedTeam = -1;
                var grantedRace = default(RaceType);
                bool eliminated = false;
                for (int i = 0; i < teamBuffer.Length; i++)
                {
                    if (teamBuffer[i].IsOccupied && teamBuffer[i].OccupyingPlayer == sourceConnection)
                    {
                        grantedTeam = i;
                        grantedRace = teamBuffer[i].Race;
                        eliminated = teamBuffer[i].IsEliminated;
                        break;
                    }
                }

                if (grantedTeam == -1)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ServerStreamReadySystem] Could not find pre-allocated team for connection {sourceConnection}. Ghost stream rejected.");
                    continue; // Something went very wrong with the lobby tracking
                }

                UnityEngine.Debug.Log(
                    $"[ServerStreamReadySystem] Connection {sourceConnection} read for ghosts. Team {grantedTeam} base allocating. Commencing stream.");

                // Officially bring connection in-game on the server, starting ghost sync
                ecb.AddComponent<NetworkStreamInGame>(sourceConnection);

                // A player reconnecting to a corner that died while they were away has nothing to
                // allocate — their base is rubble and must stay that way. They still come in-game,
                // so they receive every ghost and can watch the rest of the match.
                if (eliminated)
                {
                    UnityEngine.Debug.Log(
                        $"[ServerStreamReadySystem] Connection {sourceConnection} rejoined an eliminated " +
                        $"Team {grantedTeam} — streaming ghosts as a spectator, no base allocated.");
                    continue;
                }

                // Allocate their base, triggering BaseAllocationSystem
                ecb.AddComponent(sourceConnection, new PendingBaseAllocation
                {
                    ApprovedTeam = (TeamNumber)grantedTeam,
                    ApprovedRace = grantedRace
                });
            }
        }
    }
}
