using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;

namespace ElementLogicFail.Scripts.Scenes
{
    public class MainMenuSceneController : BaseScene<MainMenuData>
    {
        protected override void Loaded()
        {
            Debug.Log("Loader Load Complete");
        }
    }
    
    public class MainMenuData : ISceneData{ }
}