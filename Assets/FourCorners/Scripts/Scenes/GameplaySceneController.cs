using System.Threading.Tasks;
using FourCorners.Scripts.Controller;
using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Manager.Interface.Camera;
using FourCorners.Scripts.Scenes.Interface;
using FourCorners.Scripts.Services.Interface;
using FourCorners.Scripts.View;
using UnityEngine;

namespace FourCorners.Scripts.Scenes
{
    /// <summary>
    /// Scene controller for the match itself.
    ///
    /// Two ways out, both landing on the MainMenu:
    ///   - the player presses Leave, which disconnects them (see ExitLobby for the same shape);
    ///   - the connection drops under us — the host left, or we were dropped — which arrives as
    ///     ISystemBridgeService.OnDisconnected. Without that path a client whose host quit sits
    ///     in a frozen scene with no server behind it.
    /// </summary>
    public class GameplaySceneController : BaseScene<GameplayData>
    {
        [SerializeField] private CameraController cameraController;
        [SerializeField] private GameplayScreenUI gameplayScreenUI;

        private ISystemBridgeService _systemBridgeService;
        private (Vector3 min, Vector3 max) _bounds;

        /// <summary>Leaving and being dropped can race; only the first one gets to navigate.</summary>
        private bool _leaving;

        protected override Task Loading()
        {
            return WaitAndInitCameraAsync();
        }

        protected override void Loaded()
        {
            var cameraManager = GetManager<ICameraManager>();
            cameraController.Init(cameraManager, _bounds.min, _bounds.max);

            _systemBridgeService = GetService<ISystemBridgeService>();
            _systemBridgeService.OnDisconnected += ConnectionLost;
            _systemBridgeService.OnMatchEnded += MatchEnded;

            gameplayScreenUI.Init(onLeave: LeaveMatch);
        }

        protected override void Unload()
        {
            // Mandatory, not tidiness: LobbySceneController's Unload comment records the
            // MissingReferenceException a leaked bridge subscription caused there.
            if (_systemBridgeService == null) return;

            _systemBridgeService.OnDisconnected -= ConnectionLost;
            _systemBridgeService.OnMatchEnded -= MatchEnded;
            _systemBridgeService = null;

            // Deliberately NOT unloading the entity scenes here. This runs synchronously, before
            // the scene swap even starts, so the connection can still be NetworkStreamInGame —
            // and pulling a subscene containing prespawned ghosts out from under a live ghost
            // collection produces "ghost ... does not have a valid prefab on the client
            // (PrespawnSceneList)". MainMenuSceneController sweeps up instead, by which point
            // the connection is gone and SubScene's own teardown has already run.
        }

        private async Task WaitAndInitCameraAsync()
        {
            var service = GetService<ISystemBridgeService>();

            // Declared outside the loop: an out variable introduced in a `while` condition is
            // scoped to the while statement, unlike one in an `if` condition.
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            // Poll on the success flag, never on the bounds themselves: a map centred on the
            // origin is indistinguishable from "SubScene still streaming" and hangs forever.
            while (!service.TryGetMapBounds(out min, out max))
            {
                await Task.Yield();

                // Leaving during the load unloads the entity scenes, so the bounds will never
                // arrive — without this the loop spins forever on a destroyed controller and
                // then throws from cameraController.Setup().
                if (this == null || _leaving) return;
            }

            _bounds = (min, max);
            cameraController.Setup();

            // Notify systems (e.g. ClientStreamReadySystem) that the scene is fully baked
            service.NotifyClientSceneReady();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Navigation
        // ──────────────────────────────────────────────────────────────────────────

        private void LeaveMatch()
        {
            if (_leaving) return;
            _leaving = true;

            UnityEngine.Debug.Log("[GameplaySceneController] Leaving the match.");

            // Mirrors LobbySceneController.ExitLobby. On the host this also drops every remote
            // connection — the server world lives in this process, so the match cannot outlive it.
            GetService<IMultiplayerService>().Disconnect();
            GetManager<ISceneManager>().LoadScene(new MainMenuData());
        }

        /// <summary>
        /// The host left, or we were dropped. Deferred by a frame because OnDisconnected is
        /// raised from inside BridgeNotificationSystem's update, and leaving the scene runs
        /// Unload() — which unloads the entity scenes. Doing that structural work in the middle
        /// of SimulationSystemGroup's own iteration is asking for trouble.
        /// </summary>
        /// <summary>
        /// The server declared a winner. Deliberately does not navigate: the player stays in the
        /// scene watching, and leaves when they want to. Eliminated players get spectating for
        /// free this way — their client is already receiving every ghost.
        /// </summary>
        private void MatchEnded(bool localPlayerWon)
        {
            UnityEngine.Debug.Log($"[GameplaySceneController] Match ended. Won={localPlayerWon}.");
            gameplayScreenUI.ShowMatchResult(localPlayerWon);
        }

        private void ConnectionLost()
        {
            if (_leaving) return;
            _leaving = true;

            UnityEngine.Debug.Log("[GameplaySceneController] Connection lost. Returning to the main menu.");
            _ = ReturnToMainMenuNextFrameAsync();
        }

        private async Task ReturnToMainMenuNextFrameAsync()
        {
            await Task.Yield();
            if (this == null) return;

            GetManager<ISceneManager>().LoadScene(new MainMenuData());
        }
    }

    public class GameplayData : ISceneData { }
}
