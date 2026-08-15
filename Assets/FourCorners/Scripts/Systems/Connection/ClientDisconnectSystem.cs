using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// The client's counterpart to ServerDisconnectSystem: notices that this world lost the
    /// server, wipes the session state that would otherwise poison the next connection, and
    /// raises <see cref="ClientDisconnectedTag"/> for the managed layer.
    ///
    /// Two problems this closes:
    ///
    /// 1. Nothing used to observe a drop on the client at all. A player whose host quit sat in
    ///    a frozen gameplay scene forever.
    ///
    /// 2. Every client-session singleton outlives the connection, because the client world is
    ///    never recreated. On a rejoin, ClientStreamReadySystem — which needs only ClientSceneReady
    ///    plus a fresh connection — would fire immediately and request ghosts while the player was
    ///    still sitting in the lobby, and ClientSceneReadySystem would never re-run because its
    ///    WithNone&lt;ClientSceneReady&gt; gate was already closed.
    ///
    /// LobbyStateSnapshot is reset rather than destroyed: ClientLobbyStateSystem creates it once
    /// in OnCreate and assumes the singleton exists for the world's lifetime.
    ///
    /// Uses <see cref="NetworkStreamDriver.ConnectionEventsForTick"/> rather than ConnectionState,
    /// so no component has to be attached to the connection in advance — which matters here
    /// because a client can be dropped before it ever reaches the lobby.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientDisconnectSystem : ISystem
    {
        private EntityQuery _matchStartedQuery;
        private EntityQuery _matchEndedQuery;
        private EntityQuery _sceneReadyQuery;
        private EntityQuery _sceneLoadedQuery;
        private EntityQuery _joinRejectedQuery;
        private EntityQuery _disconnectedTagQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            // Cleared by query rather than by singleton lookup. This is the last line of defence
            // against a leaked duplicate: TryGetSingletonEntity throws outright when it finds
            // more than one, which turns a recoverable mess into an exception every frame — and
            // the very thing that would clean the mess up is the thing that dies.
            _matchStartedQuery = state.GetEntityQuery(ComponentType.ReadOnly<MatchStartedTag>());
            _matchEndedQuery = state.GetEntityQuery(ComponentType.ReadOnly<MatchEndedTag>());
            _sceneReadyQuery = state.GetEntityQuery(ComponentType.ReadOnly<ClientSceneReady>());
            _sceneLoadedQuery = state.GetEntityQuery(ComponentType.ReadOnly<SceneLoadedTag>());
            _joinRejectedQuery = state.GetEntityQuery(ComponentType.ReadOnly<JoinRejectedTag>());
            _disconnectedTagQuery = state.GetEntityQuery(ComponentType.ReadOnly<ClientDisconnectedTag>());
        }

        public void OnUpdate(ref SystemState state)
        {
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;

            bool disconnected = false;
            foreach (var connectionEvent in networkStreamDriver.ConnectionEventsForTick)
            {
                if (connectionEvent.State != ConnectionState.State.Disconnected) continue;

                disconnected = true;
                UnityEngine.Debug.Log(
                    $"[ClientDisconnectSystem] World '{state.WorldUnmanaged.Name}' lost the server " +
                    $"(reason: {connectionEvent.DisconnectReason}). Clearing session state.");
            }

            if (!disconnected) return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            ecb.DestroyEntity(_matchStartedQuery, EntityQueryCaptureMode.AtPlayback);
            ecb.DestroyEntity(_matchEndedQuery, EntityQueryCaptureMode.AtPlayback);
            ecb.DestroyEntity(_sceneReadyQuery, EntityQueryCaptureMode.AtPlayback);
            ecb.DestroyEntity(_sceneLoadedQuery, EntityQueryCaptureMode.AtPlayback);
            ecb.DestroyEntity(_joinRejectedQuery, EntityQueryCaptureMode.AtPlayback);

            // Reset, not destroy: ClientLobbyStateSystem creates this singleton in OnCreate and
            // assumes it exists for the world's lifetime. Version 0 is the "server has not told
            // us anything yet" sentinel SystemBridgeService.TryGetLobbyState already understands.
            foreach (var snapshot in SystemAPI.Query<RefRW<LobbyStateSnapshot>>())
                snapshot.ValueRW = default;

            // Guarded so a tag nobody consumed cannot stack. Worlds without a
            // BridgeNotificationSystem (thin clients) never consume theirs.
            if (_disconnectedTagQuery.IsEmpty)
                ecb.AddComponent<ClientDisconnectedTag>(ecb.CreateEntity());
        }
    }
}
