using ElementLogicFail.Scripts.Scenes.Interface;

namespace ElementLogicFail.Scripts.Manager.Interface
{
    public interface ISceneManager
    {
        void LoadScene(ISceneData data);
        void LoadOverlayScene(ISceneData data);
        void UnloadOverlay(IBaseScene overlay);
    }
}