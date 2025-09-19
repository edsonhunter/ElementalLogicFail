using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;
using UnityEngine.UI;

namespace ElementLogicFail.Scripts.Scenes
{
    public class ConfigScene : BaseScene<ConfigData>
    {
        [field: SerializeField] private Button backButton;

        protected override void Loaded()
        {
            backButton.onClick.AddListener(Back);
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