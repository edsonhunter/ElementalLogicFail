using System.Threading.Tasks;
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
    ///
    /// Late join: a player accepted while the match is already Active is sent MatchStartedRpc by
    /// ServerAcceptGameSystem on the spot, so OnMatchStarted has already fired by the time this
    /// scene loads. Loaded() therefore pulls IsMatchStarted and navigates on, exactly as it pulls
    /// TryGetLobbyState rather than trusting the edge-triggered event.
    /// </summary>
    public class LobbySceneController : BaseScene<LobbyData>
    {
        [field: SerializeField] private LobbyScreenUI lobbyScreenUI;

        private ISystemBridgeService _systemBridgeService;

        /// <summary>
        /// There are two routes to Gameplay — the OnMatchStarted event and the IsMatchStarted
        /// pull below — and on a late join both are live at once. Whichever wins, the other must
        /// not fire a second scene load on top of it.
        /// </summary>
        private bool _navigatingToGameplay;

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

            // Same reason, and mandatory for joining a match that is already running: the server
            // answers such a join with MatchStartedRpc straight away, so OnMatchStarted fires
            // while this scene is still loading and NavigateToGameplay would never be called.
            if (_systemBridgeService.IsMatchStarted)
            {
                lobbyScreenUI.ShowJoiningMatch();
                _ = NavigateToGameplayNextFrameAsync();
            }
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
            if (_navigatingToGameplay) return;
            _navigatingToGameplay = true;

            UnityEngine.Debug.Log("[LobbySceneController] Match started. Transitioning to Gameplay.");
            GetManager<ISceneManager>().LoadScene(new GameplayData());
        }

        /// <summary>
        /// Deferred by one frame because Loaded() is still on the stack. LoadScene synchronously
        /// calls SetupSceneToLoad, which fires Unload() on the scene it is replacing — that would
        /// tear this controller down from inside its own Loaded().
        /// </summary>
        private async Task NavigateToGameplayNextFrameAsync()
        {
            await Task.Yield();

            // The scene may have been swapped out from under us in the meantime (e.g. Exit).
            if (this == null || _systemBridgeService == null) return;

            NavigateToGameplay();
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
