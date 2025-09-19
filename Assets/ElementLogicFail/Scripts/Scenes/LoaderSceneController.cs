using System.Threading.Tasks;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;

namespace ElementLogicFail.Scripts.Scenes
{
    public class LoaderSceneController : BaseScene<LoaderData>
    {
        protected override async Task Loading()
        {
            Debug.Log("Loader Loading Started");
            await Task.Run(async () =>
            {
                await Task.Delay(2);
            });
            Debug.Log("Loader Loading Complete");
        }

        protected override void Loaded()
        {
            Debug.Log("Loader Load Complete");
            GetManager<ISceneManager>().LoadScene(new MainMenuData());
        }

        protected override void Unload()
        {
            Debug.Log("Loader Unloaded");
        }
    }
    
    public class LoaderData : ISceneData { }
}