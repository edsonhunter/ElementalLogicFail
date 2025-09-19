using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace ElementLogicFail.Scripts.Scenes
{
    public class MainMenuSceneController : BaseScene<MainMenuData>
    {
        [field: SerializeField] private Button playGameButton;
        [field: SerializeField] private Button configButton;
        protected override void Loaded()
        {
            Debug.Log("Main Menu Load Complete");
            playGameButton.onClick.AddListener(PlayGame);
            configButton.onClick.AddListener(OpenConfig);
        }

        private void PlayGame()
        {
            GetManager<ISceneManager>().LoadScene(new GameplayData());
        }

        private void OpenConfig()
        {
            GetManager<ISceneManager>().LoadOverlayScene(new ConfigData());
        }
    }
    
    public class MainMenuData : ISceneData{ }
}