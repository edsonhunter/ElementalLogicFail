using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Scenes;
using FourCorners.Scripts.Scenes.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace FourCorners.Scripts.Manager
{
    public class SceneManager : ISceneManager
    {
        private BaseScene _activeScene;
        private IApplication _application;
        private bool _isLoading;

        public SceneManager(IApplication application)
        {
            _application = application;
        }

        /// <summary>
        /// Loads a scene, replacing the current one.
        ///
        /// Calls made while a load is already in flight are refused. Without that guard two
        /// LoadSceneMode.Single loads run concurrently and both scenes exist at once — which is
        /// survivable for GameObjects but not for a scene owning a SubScene: each instance's
        /// SubScene loads the entity scene, while SubScene teardown only ever unloads the *first*
        /// scene entity matching the GUID. The surplus copy then never goes away, and every
        /// GetSingleton against baked data (RaceCatalog, WanderArea) throws from then on.
        /// </summary>
        public void LoadScene(ISceneData data)
        {
            if (_isLoading)
            {
                Debug.LogWarning(
                    $"[SceneManager] Ignoring LoadScene({data?.GetType().Name}) — a scene load is already in flight.");
                return;
            }

            SetupSceneToLoad();
            if (data == null)
            {
                return;
            }

            _isLoading = true;

            UnitySceneManager.LoadSceneAsync(data.GetType().Name.Replace("Data", "Scene"), LoadSceneMode.Single)
                    .completed +=
                async operation =>
                {
                    _activeScene = GetActiveSceneController();
                    _activeScene.Init(_application, data);
                    _activeScene.SetActiveScene(true);

                    // Cleared before the lifecycle hooks run, not after: a controller is allowed
                    // to navigate straight onward from Loading/Loaded — that is exactly what the
                    // lobby does when it finds the match already running.
                    _isLoading = false;

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
        
        public void StartFirstScene(ISceneData data)
        {
            LoadScene(data);
        }
        
        private void SetupSceneToLoad()
        {
            if (_activeScene != null)
            {
                _activeScene.SetActiveScene(false);
                _activeScene.FireUnload();
            }
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
        
        private BaseScene GetActiveSceneController()
        {
            Scene activeScene = UnitySceneManager.GetActiveScene();
            GameObject[] overlayRootObjects = activeScene.GetRootGameObjects();

            BaseScene baseScene = null;
            foreach (GameObject rootObject in overlayRootObjects)
            {
                if (rootObject.GetComponent<BaseScene>() == null)
                    continue;
                
                baseScene = rootObject.GetComponent<BaseScene>();
            }

            return baseScene;
        }
    }
}
