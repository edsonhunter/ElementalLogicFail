using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Services.Interface;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// The project's only bridge from ECS state to managed C# events, and the only SystemBase
    /// in the connection pipeline.
    ///
    /// It exists in exactly one world — <see cref="ClientServerBootstrap.ClientWorld"/> — where it
    /// creates <see cref="PresentationClientTag"/>. Every unmanaged system discriminates on that
    /// tag instead of probing for this system, which keeps them Burst-compilable.
    ///
    /// Responsibility is deliberately narrow: observe simulation state, fan out to the UI.
    /// The single exception is consuming ClientDisconnectedTag, which is an event rather than a
    /// state and has exactly one reader — this system.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BridgeNotificationSystem : SystemBase
    {
        private ISystemBridgeService _service;
        private uint _lastLobbyVersion;
        private bool _worldResolved;

        // Edge-triggered on the tag's presence rather than latched "already fired once". A latch
        // only re-arms if something explicitly clears it, so a single missed disconnect used to
        // leave the player permanently unable to enter another match. Since ClientDisconnectSystem
        // destroys these tags, presence going false→true is the honest signal for "again".
        private bool _matchStartedPresent;
        private bool _joinRejectedPresent;

        private bool _matchEndedPresent;

        private EntityQuery _matchStartedQuery;
        private EntityQuery _matchEndedQuery;
        private EntityQuery _joinRejectedQuery;
        private EntityQuery _disconnectedQuery;
        private EntityQuery _commandRejectedQuery;

        protected override void OnCreate()
        {
            // Queried rather than asked for as singletons: HasSingleton throws outright on a
            // duplicate, and this system is the only route from simulation to the UI — if it
            // dies, the player is stranded on whatever screen they happen to be looking at.
            _matchStartedQuery = GetEntityQuery(ComponentType.ReadOnly<MatchStartedTag>());
            _matchEndedQuery = GetEntityQuery(ComponentType.ReadOnly<MatchEndedTag>());
            _joinRejectedQuery = GetEntityQuery(ComponentType.ReadOnly<JoinRejectedTag>());
            _disconnectedQuery = GetEntityQuery(ComponentType.ReadOnly<ClientDisconnectedTag>());
            _commandRejectedQuery = GetEntityQuery(ComponentType.ReadOnly<BaseCommandRejectedTag>());
        }

        // Note OnCreate builds queries only — deliberately no world check there.
        // ClientServerBootstrap.CreateClientWorld creates its systems (running OnCreate) BEFORE it
        // does ClientWorlds.Add(world), and ClientWorld reads ClientWorlds[0] — so
        // ClientServerBootstrap.ClientWorld is still null at that point. Comparing against it
        // there disabled this system permanently in every world, which meant PresentationClientTag
        // was never created and no lobby update ever reached the UI. See ResolvePresentationWorld.

        protected override void OnUpdate()
        {
            if (!ResolvePresentationWorld()) return;

            // Pulled lazily: worlds are created at application start, but the service is only
            // registered once the MainMenu scene loads. See ClientBridgeRegistry.
            _service ??= ClientBridgeRegistry.Service;
            if (_service == null) return;

            if (SystemAPI.TryGetSingleton<LobbyStateSnapshot>(out var lobby) &&
                lobby.Version != _lastLobbyVersion)
            {
                _lastLobbyVersion = lobby.Version;
                _service.OnLobbyStateUpdate?.Invoke(new LobbyStateUpdateEvent
                {
                    IsHost = lobby.IsHost,
                    PlayerCount = lobby.PlayerCount
                });
            }

            bool matchStarted = !_matchStartedQuery.IsEmpty;
            if (matchStarted && !_matchStartedPresent)
                _service.OnMatchStarted?.Invoke();
            _matchStartedPresent = matchStarted;

            bool matchEnded = !_matchEndedQuery.IsEmpty;
            if (matchEnded && !_matchEndedPresent)
            {
                // Read rather than assumed: the tag carries the outcome, and only this world
                // knows whether the winning NetworkId is its own.
                var outcome = SystemAPI.GetSingleton<MatchEndedTag>();
                _service.OnMatchEnded?.Invoke(outcome.LocalPlayerWon);
            }
            _matchEndedPresent = matchEnded;

            bool joinRejected = !_joinRejectedQuery.IsEmpty;
            if (joinRejected && !_joinRejectedPresent)
                _service.OnJoinRejected?.Invoke();
            _joinRejectedPresent = joinRejected;

            // Not edge-triggered, unlike everything above it. Those are states, and asking twice
            // whether the match has started is the same question; a rejection is an answer to one
            // specific press, so every one is delivered and then consumed.
            if (!_commandRejectedQuery.IsEmpty)
            {
                using var rejections =
                    _commandRejectedQuery.ToComponentDataArray<BaseCommandRejectedTag>(Allocator.Temp);

                // Destroyed before the fan-out: a subscriber is free to send another command from
                // inside the callback, and clearing afterwards would take its rejection with it.
                EntityManager.DestroyEntity(_commandRejectedQuery);

                for (int i = 0; i < rejections.Length; i++)
                    _service.OnBaseCommandRejected?.Invoke(rejections[i].Type, rejections[i].Reason);
            }

            // Last, so a disconnect arriving on the same frame as a lobby update still lets that
            // update through before the version is rewound for the next session.
            if (_disconnectedQuery.IsEmpty) return;

            // LobbyStateSnapshot is reset rather than destroyed, so its version restarts at 0 and
            // would otherwise look unchanged to the comparison above.
            _lastLobbyVersion = 0;

            // Consumed here: unlike MatchStartedTag this marks an instant, not a state.
            EntityManager.DestroyEntity(_disconnectedQuery);
            _service.OnDisconnected?.Invoke();
        }

        /// <summary>
        /// Decides once, on the first update where Netcode has finished registering worlds,
        /// whether this world owns the UI — and claims it with <see cref="PresentationClientTag"/>.
        ///
        /// Deferred rather than done in OnCreate because ClientServerBootstrap.ClientWorld is
        /// not populated until after system creation completes.
        /// </summary>
        private bool ResolvePresentationWorld()
        {
            if (_worldResolved) return true;

            var presentationWorld = ClientServerBootstrap.ClientWorld;
            if (presentationWorld == null) return false; // registration not finished yet

            _worldResolved = true;

            // Guard against a second full client world in-process ever claiming the UI.
            if (World != presentationWorld)
            {
                Enabled = false;
                return false;
            }

            if (!SystemAPI.HasSingleton<PresentationClientTag>())
                EntityManager.CreateEntity(typeof(PresentationClientTag));

            return true;
        }
    }
}
