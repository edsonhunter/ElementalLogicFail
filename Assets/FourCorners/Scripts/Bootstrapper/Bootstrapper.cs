using FourCorners.Scripts.Manager;
using FourCorners.Scripts.Manager.Camera;
using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Manager.Interface.Camera;
using FourCorners.Scripts.Scenes;
using FourCorners.Scripts.Services;
using FourCorners.Scripts.Services.Interface;
using UnityEngine;

namespace FourCorners.Scripts.Bootstrapper
{
    public class Bootstrapper : MonoBehaviour
    {
        private ApplicationManager _applicationManager;
        
        private void Start()
        {
            SetupApplication();
            SetupServices();
            SetupManagers();
            StartGame();
        }

        private void SetupApplication()
        {
            _applicationManager = new GameObject("ApplicationManager").AddComponent<ApplicationManager>();
        }

        private void SetupServices()
        {
            _applicationManager.RegisterServices<ISystemBridgeService>(new SystemBridgeService());
            _applicationManager.RegisterServices<IAddressablesService>(new AddressablesService());
            _applicationManager.RegisterServices<IMultiplayerService>(new MultiplayerService());
        }

        private void SetupManagers()
        {
            //Register all managers
            
            _applicationManager.RegisterManager<ISceneManager>(new SceneManager(_applicationManager));
            _applicationManager.RegisterManager<ICameraManager>(new CameraManager());
        }

        private void StartGame()
        {
            _applicationManager.GetManager<ISceneManager>().LoadScene(new LoaderData());
        }
    }
}
