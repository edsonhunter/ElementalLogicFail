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

        protected override void OnCreate()
        {
            // Guard against a second full client world in-process ever claiming the UI.
            if (World != ClientServerBootstrap.ClientWorld)
            {
                Enabled = false;
                return;
            }

            EntityManager.CreateEntity(typeof(PresentationClientTag));
        }

        protected override void OnUpdate()
        {
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
    }
}
