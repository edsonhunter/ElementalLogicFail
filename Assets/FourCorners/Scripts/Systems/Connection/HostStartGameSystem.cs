using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Listens for StartGameRequest RPCs on the server.
    ///
    /// Validation rules (both must pass):
    ///   1. The sender's connection entity must carry HostTag.
    ///   2. The ConnectedPlayerElement buffer must have &gt;= 2 entries.
    ///
    /// On success: transitions MatchState.Phase from Lobby → Active,
    /// which unblocks MinionSpawningSystem globally.
    /// </summary>
    // Not Burst-compatible: the validation failures log interpolated strings, which is worth
    // more here than Burst on a system that runs once per match.
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HostStartGameSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchStateTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StartGameRequest, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));

            var matchQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStateTag, MatchState, ConnectedPlayerElement>();
            state.RequireForUpdate(state.GetEntityQuery(matchQuery));
        }

        public void OnUpdate(ref SystemState state)
        {
            var matchState      = SystemAPI.GetSingletonRW<MatchState>();
            var playerBuffer    = SystemAPI.GetSingletonBuffer<ConnectedPlayerElement>(isReadOnly: true);
            var matchStateEntity = SystemAPI.GetSingletonEntity<MatchStateTag>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Only valid to start from Lobby state
            if (matchState.ValueRO.Phase != MatchPhase.Lobby)
            {
                // Clean up any stale RPCs and bail early
                foreach (var (_, reqEntity) in
                         SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                             .WithAll<StartGameRequest>()
                             .WithEntityAccess())
                {
                    ecb.DestroyEntity(reqEntity);
                }
                return;
            }

            foreach (var (receive, reqEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<StartGameRequest>()
                         .WithEntityAccess())
            {
                var senderConn = receive.ValueRO.SourceConnection;
                ecb.DestroyEntity(reqEntity);

                // --- Validation 1: Sender must be the host ---
                if (!SystemAPI.HasComponent<HostTag>(senderConn))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[HostStartGameSystem] StartGameRequest rejected: sender {senderConn} is NOT the host.");
                    continue;
                }

                // --- Validation 2: Minimum player count ---
                if (playerBuffer.Length < 2)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[HostStartGameSystem] StartGameRequest rejected: only {playerBuffer.Length} player(s) " +
                        "connected. Minimum 2 required.");
                    continue;
                }

                // --- Transition to Active ---
                // Copy-then-mutate: constructing a fresh MatchState here would silently reset
                // every other field on it (HostNetworkId).
                var updated = matchState.ValueRO;
                updated.Phase = MatchPhase.Active;
                ecb.SetComponent(matchStateEntity, updated);

                // --- Broadcast MatchStartedRpc to ALL connected clients ---
                // This triggers ClientMatchStartedSystem on every client,
                // which fires OnMatchStarted and loads the GameplayScene universally.
                for (int i = 0; i < playerBuffer.Length; i++)
                {
                    var broadcast = ecb.CreateEntity();
                    ecb.AddComponent<MatchStartedRpc>(broadcast);
                    ecb.AddComponent(broadcast, new SendRpcCommandRequest
                    {
                        TargetConnection = playerBuffer[i].ConnectionEntity
                    });
                }

                UnityEngine.Debug.Log(
                    $"[HostStartGameSystem] Match started! {playerBuffer.Length} players. " +
                    "MatchState → Active. MatchStartedRpc broadcast sent.");

                // Only the first valid request matters per frame
                break;
            }
        }
    }
}
