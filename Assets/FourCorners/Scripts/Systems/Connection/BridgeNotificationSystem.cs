using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Services.Interface;
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
    /// It never mutates simulation state.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BridgeNotificationSystem : SystemBase
    {
        private ISystemBridgeService _service;
        private uint _lastLobbyVersion;
        private bool _matchStartedFired;
        private bool _joinRejectedFired;
        private bool _worldResolved;

        // No OnCreate world check. ClientServerBootstrap.CreateClientWorld creates its systems
        // (running OnCreate) BEFORE it does ClientWorlds.Add(world), and ClientWorld reads
        // ClientWorlds[0] — so ClientServerBootstrap.ClientWorld is still null at that point.
        // Comparing against it there disabled this system permanently in every world, which
        // meant PresentationClientTag was never created and no lobby update ever reached the UI.

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

            if (!_matchStartedFired && SystemAPI.HasSingleton<MatchStartedTag>())
            {
                _matchStartedFired = true;
                _service.OnMatchStarted?.Invoke();
            }

            if (!_joinRejectedFired && SystemAPI.HasSingleton<JoinRejectedTag>())
            {
                _joinRejectedFired = true;
                _service.OnJoinRejected?.Invoke();
            }
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
