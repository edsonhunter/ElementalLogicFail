using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Scenes.Interface;
using FourCorners.Scripts.Services.Interface;
using FourCorners.Scripts.View;
using UnityEngine;
using UnityEngine.UI;

namespace FourCorners.Scripts.Scenes
{
    public class ConfigScene : BaseScene<ConfigData>
    {
        [field: SerializeField] private Button backButton;
        [field: SerializeField] private MultiplayerTestUI multiplayerTestUI;

        protected override void Loaded()
        {
            backButton.onClick.AddListener(Back);
            var multiplayerService = GetService<IMultiplayerService>();
            multiplayerTestUI.Init(multiplayerService.AuthenticateAsync(), multiplayerService.HostRelayGameAsync, multiplayerService.JoinRelayGameAsync);
        }

        private void Back()
        {
            GetManager<ISceneManager>().UnloadOverlay(this);       
        }
    }

    public class ConfigData : ISceneData
    {
    }
}
