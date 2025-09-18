using System;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes;
using ElementLogicFail.Scripts.Scenes.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ElementLogicFail.Scripts.Manager
{
    public class SceneManager : ISceneManager
    {
        private BaseScene _activeScene;
        private IApplication _application;

        public SceneManager(IApplication application)
        {
            _application = application;
        }

        private void SetupSceneToLoad()
        {
            if (_activeScene != null)
            {
                _activeScene.SetActiveScene(false);
                _activeScene.FireUnload();
            }
        }

        public void LoadScene(ISceneData data)
        {
            SetupSceneToLoad();
            if (data == null)
            {
                return;
            }

            UnitySceneManager.LoadSceneAsync(data.GetType().Name.Replace("Data", "Scene"), LoadSceneMode.Single)
                    .completed +=
                async operation =>
                {
                    _activeScene = GetActiveSceneController();
                    _activeScene.Init(_application, data);
                    _activeScene.SetActiveScene(true);
                    await _activeScene.FireLoading();
                    _activeScene.FireLoaded();
                };
        }

        public void LoadOverlayScene(ISceneData data)
        {
            UnitySceneManager.LoadSceneAsync(data.GetType().Name.Replace("Data", "Scene"), LoadSceneMode.Additive)
                    .completed +=
                async operation =>
                {
                    SetLastLoadedSceneActive();
                    var overlay = GetActiveSceneController();
                    overlay.Init(_application, data);
                    _activeScene.SetActiveScene(false);
                    await overlay.FireLoading();
                    overlay.FireLoaded();
                };
        }

        public void UnloadOverlay(IBaseScene overlay)
        {
            overlay.FireUnload();
            UnitySceneManager.UnloadSceneAsync(UnitySceneManager.GetActiveScene()).completed += operation =>
            {
                _activeScene.SetActiveScene(true);
            };
        }

        public BaseScene GetActiveSceneController()
        {
            Scene activeScene = UnitySceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (var rootObject in rootObjects)
            {
                if (rootObject.GetComponent<BaseScene>() == null)
                {
                    continue;
                }

                return rootObject.GetComponent<BaseScene>();
            }

            throw new InvalidOperationException(
                $"Cannot have a scene without a game object with component {nameof(BaseScene)}.");
        }

        private void SetLastLoadedSceneActive()
        {
            Scene lastLoadedScene = default;
            var lastSceneIndex = UnitySceneManager.sceneCount - 1;

            while (lastSceneIndex >= 0)
            {
                lastLoadedScene = UnitySceneManager.GetSceneAt(lastSceneIndex);
                if (lastLoadedScene.IsValid() && lastLoadedScene.isLoaded)
                {
                    break;
                }

                lastSceneIndex--;
            }

            UnitySceneManager.SetActiveScene(lastLoadedScene);
        }
    }
}