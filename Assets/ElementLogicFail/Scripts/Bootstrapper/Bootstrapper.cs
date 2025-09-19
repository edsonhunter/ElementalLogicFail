using ElementLogicFail.Scripts.Manager;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes;
using ElementLogicFail.Scripts.Utils.Threadpool;
using UnityEngine;

namespace ElementLogicFail.Scripts.Bootstrapper
{
    public class Bootstrapper : MonoBehaviour
    {
        private ApplicationManager _applicationManager;
        
        private void Start()
        {
            //Create applicationPrefab
            //Initialize applicationPrefab
            SetupThreadPool();
            SetupApplication();
            SetupServices();
            SetupManagers();
            StartGame();
        }

        private void SetupApplication()
        {
            _applicationManager = new GameObject().AddComponent<ApplicationManager>();
        }

        private void SetupThreadPool()
        {
            var threadObject = new GameObject().AddComponent<ThreadPoolController>();
            threadObject.transform.SetParent(_applicationManager.transform);
        }

        private void SetupServices()
        {
            //register all services
        }

        private void SetupManagers()
        {
            //Register all managers
            _applicationManager.RegisterManager<ISceneManager>(new SceneManager(_applicationManager));
        }

        private void StartGame()
        {
            _applicationManager.GetManager<ISceneManager>().LoadScene(new LoaderData());
        }
    }
}
