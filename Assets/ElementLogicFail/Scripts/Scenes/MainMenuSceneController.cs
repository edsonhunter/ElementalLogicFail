using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;

namespace ElementLogicFail.Scripts.Scenes
{
    public class MainMenuSceneController : BaseScene<MainMenuData>
    {
        protected override void Loaded()
        {
            Debug.Log("Main Menu Load Complete");
        }
    }
    
    public class MainMenuData : ISceneData{ }
}