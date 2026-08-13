using System.Threading.Tasks;
using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Scenes.Interface;
using FourCorners.Scripts.Services.Interface;
using FourCorners.Scripts.View;
using UnityEngine;
using UnityEngine.UI;

namespace FourCorners.Scripts.Scenes
{
    public class MainMenuSceneController : BaseScene<MainMenuData>
    {
        [field: SerializeField] private Button playGameButton;
        [field: SerializeField] private Button configButton;
        [field: SerializeField] private ConnectionScreenUI connectionScreenPanel;

        protected override Task Loading()
        {
            var bridge = GetService<ISystemBridgeService>();
            bridge.RegisterBridge();

            // Arriving here means no match is in progress for this process, so nothing should
            // still be holding the baked gameplay subscene. SubScene's own teardown has already
            // run by now, but it only ever unloads one scene entity per GUID — so if a copy was
            // ever loaded twice, this is what clears the remainder. A no-op on first boot.
            bridge.UnloadEntityScenes();

            return Task.CompletedTask;
        }

        protected override void Loaded()
        {
            if (connectionScreenPanel != null)
                connectionScreenPanel.gameObject.SetActive(false);

            playGameButton.onClick.AddListener(PlayGame);
            configButton.onClick.AddListener(OpenConfig);
        }

        private void PlayGame()
        {
            if (connectionScreenPanel != null)
            {
                var multiplayer = GetService<IMultiplayerService>();

                connectionScreenPanel.gameObject.SetActive(true);
                connectionScreenPanel.InitSelection(GetService<ISystemBridgeService>().SetLocalPlayerSelection);
                connectionScreenPanel.Init(
                    multiplayer.AuthenticateAsync(),
                    multiplayer.HostRelayGameAsync,
                    multiplayer.JoinRelayGameAsync,
                    multiplayer.HostDirectGameAsync,
                    multiplayer.JoinDirectGameAsync,
                    StartGame);
            }
            else
            {
                // Fallback for missing UI
                GetManager<ISceneManager>().LoadScene(new GameplayData());
            }
        }

        private void OpenConfig()
        {
            GetManager<ISceneManager>().LoadOverlayScene(new ConfigData());
        }

        private void StartGame()
        {
            GetManager<ISceneManager>().LoadScene(new LobbyData());
        }
    }
    
    public class MainMenuData : ISceneData{ }
}
