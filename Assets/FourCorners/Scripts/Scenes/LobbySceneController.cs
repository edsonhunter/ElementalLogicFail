using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Scenes.Interface;
using FourCorners.Scripts.Services.Interface;
using FourCorners.Scripts.View;
using UnityEngine;

namespace FourCorners.Scripts.Scenes
{
    /// <summary>
    /// Scene controller for the Lobby phase.
    ///
    /// Lifecycle:
    ///   1. Loaded() wires up LobbyScreenUI with bridge service callbacks.
    ///   2. LobbyScreenUI subscribes to ISystemBridgeService.OnLobbyStateUpdate (ECS → UI).
    ///   3. Host presses Start → bridge.SendStartGameRequest() → server validates.
    ///   4. Server broadcasts MatchStartedRpc to ALL clients.
    ///   5. ClientMatchStartedSystem raises MatchStartedTag; BridgeNotificationSystem turns that
    ///      into bridge.OnMatchStarted in the presentation client world only.
    ///   6. OnMatchStarted calls NavigateToGameplay() which transitions to GameplayScene.
    /// </summary>
    public class LobbySceneController : BaseScene<LobbyData>
    {
        [field: SerializeField] private LobbyScreenUI lobbyScreenUI;

        private ISystemBridgeService _systemBridgeService;

        protected override void Loaded()
        {
            _systemBridgeService = GetService<ISystemBridgeService>();

            // Subscribe to the match-started broadcast so ALL clients transition together.
            _systemBridgeService.OnMatchStarted += NavigateToGameplay;
            _systemBridgeService.OnLobbyStateUpdate += LobbyUpdate;
            _systemBridgeService.OnJoinRejected += JoinRejected;

            lobbyScreenUI.Init(
                _systemBridgeService.SendStartGameRequest,
                onExit: ExitLobby);

            // The first LobbyStateUpdateRpc lands while we are still on the connection screen,
            // so subscribing alone would leave this scene showing Init()'s placeholder count
            // until some *other* player joins. Pull whatever the server has already told us.
            if (_systemBridgeService.TryGetLobbyState(out var current))
                LobbyUpdate(current);
        }

        private void LobbyUpdate(LobbyStateUpdateEvent obj)
        {
            lobbyScreenUI.UpdateLobbyState(obj.PlayerCount, obj.IsHost);
        }

        private void JoinRejected()
        {
            UnityEngine.Debug.LogWarning("[LobbySceneController] Join rejected — every corner is occupied.");
            lobbyScreenUI.ShowJoinRejected();
        }

        protected override void Unload()
        {
            // Both subscriptions must be released. Leaving OnLobbyStateUpdate attached kept a
            // destroyed controller alive and threw MissingReferenceException from
            // LobbyUpdate() on lobby re-entry.
            if (_systemBridgeService == null) return;

            _systemBridgeService.OnMatchStarted -= NavigateToGameplay;
            _systemBridgeService.OnLobbyStateUpdate -= LobbyUpdate;
            _systemBridgeService.OnJoinRejected -= JoinRejected;
            _systemBridgeService = null;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Navigation
        // ──────────────────────────────────────────────────────────────────────────

        private void NavigateToGameplay()
        {
            UnityEngine.Debug.Log("[LobbySceneController] OnMatchStarted received. Transitioning to Gameplay.");
            GetManager<ISceneManager>().LoadScene(new GameplayData());
        }

        private void ExitLobby()
        {
            // The multiplayer service handles the actual disconnection from the relay/direct session.
            GetService<IMultiplayerService>().Disconnect();
            GetManager<ISceneManager>().LoadScene(new MainMenuData());
        }
    }

    public class LobbyData : ISceneData { }
}
