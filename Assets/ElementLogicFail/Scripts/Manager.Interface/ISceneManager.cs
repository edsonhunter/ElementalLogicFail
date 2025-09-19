using ElementLogicFail.Scripts.Scenes.Interface;
using ElementLogicFail.Scripts.Services.Interface;

namespace ElementLogicFail.Scripts.Manager.Interface
{
    public interface ISceneManager : IManager
    {
        void LoadScene(ISceneData data);
        void LoadOverlayScene(ISceneData data);
        void UnloadOverlay(IBaseScene overlay);
    }
}