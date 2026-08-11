using System.Threading.Tasks;
using FourCorners.Scripts.Controller;
using FourCorners.Scripts.Manager.Interface.Camera;
using FourCorners.Scripts.Scenes.Interface;
using FourCorners.Scripts.Services.Interface;
using UnityEngine;

namespace FourCorners.Scripts.Scenes
{
    public class GameplaySceneController : BaseScene<GameplayData>
    {
        [SerializeField] private CameraController cameraController;
        private (Vector3 min, Vector3 max) _bounds;

        protected override Task Loading()
        {
            return WaitAndInitCameraAsync();
        }

        protected override void Loaded()
        {
            var cameraManager = GetManager<ICameraManager>();
            cameraController.Init(cameraManager, _bounds.min, _bounds.max);
        }

        private async Task WaitAndInitCameraAsync()
        {
            var service = GetService<ISystemBridgeService>();

            // Declared outside the loop: an out variable introduced in a `while` condition is
            // scoped to the while statement, unlike one in an `if` condition.
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            // Poll on the success flag, never on the bounds themselves: a map centred on the
            // origin is indistinguishable from "SubScene still streaming" and hangs forever.
            while (!service.TryGetMapBounds(out min, out max))
            {
                await Task.Yield();
            }

            _bounds = (min, max);
            cameraController.Setup();

            // Notify systems (e.g. ClientStreamReadySystem) that the scene is fully baked
            service.NotifyClientSceneReady();
        }
    }
    
    public class GameplayData : ISceneData { }
}
