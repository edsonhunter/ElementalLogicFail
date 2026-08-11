using FourCorners.Scripts.Manager.Interface.Camera;
using FourCorners.Scripts.Systems.Camera;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace FourCorners.Scripts.Controller
{
    public class CameraController : MonoBehaviour
    {
        private ICameraManager _cameraManager;

        private Vector3 _targetPosition;
        private float _targetZoom;
        private Camera _camera;

        [SerializeField]
        private Transform cameraTransform;

        [SerializeField]
        private float mapPadding = 50f;

        private float _moveSpeed = 40f;
        private float _moveSmoothing = 10f;
        private float _zoomSpeed = 2f;
        private float _zoomSmoothing = 5f;
        private Vector2 _zoomLimits = new Vector2(3f, 15f);

        private bool _isInitialized;
        private LocalPlayerCameraSystem _cameraSystem;

        public void Setup()
        {
            _targetPosition = transform.position;

            if (cameraTransform != null)
                _camera = cameraTransform.GetComponent<Camera>();

            // Setup initial zoom target based on projection
            if (_camera != null && _camera.orthographic)
            {
                _targetZoom = _camera.orthographicSize;
            }
            else
            {
                _targetZoom = cameraTransform != null ? cameraTransform.localPosition.z : 0f;
            }
        }
        
        public void Init(ICameraManager cameraManager, Vector3 min,
            Vector3 max)
        {
            _cameraManager = cameraManager;
            RegisterCameraBridge();
            _cameraManager.Initialize(min, max);
            _isInitialized = true;
            SetActiveControls(true);
        }

        private void OnEnable()
        {
            SetActiveControls(true);
        }

        private void OnDisable()
        {
            SetActiveControls(false);
        }

        private void Update()
        {
            if (!_isInitialized) return;

            ProcessInput();
            MoveRig();
        }

        private void OnDestroy()
        {
            if (_cameraSystem != null)
            {
                _cameraSystem.OnCameraFocus -= MoveCameraToPlayerBase;
            }
        }

        private void RegisterCameraBridge()
        {
            // Target the presentation client world explicitly. Scanning World.All and taking the
            // first match picks a nondeterministic world when thin clients share the process,
            // which silently focuses the camera on someone else's base.
            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true })
            {
                Debug.LogWarning("[CameraController] No client world — camera focus not wired.");
                return;
            }

            _cameraSystem = world.GetExistingSystemManaged<LocalPlayerCameraSystem>();
            if (_cameraSystem == null) return;

            _cameraSystem.OnCameraFocus -= MoveCameraToPlayerBase;
            _cameraSystem.OnCameraFocus += MoveCameraToPlayerBase;
        }
        
        public void MoveCameraToPlayerBase(float3 pos)
        {
            SnapPosition(pos);
        }

        private void ProcessInput()
        {
            _cameraManager.BoundsCalculator.CalculateDynamicBounds(_camera, cameraTransform, mapPadding);

            Vector2 moveInput = _cameraManager.InputHandler.GetMoveInput();
            float zoomInput = _cameraManager.InputHandler.GetZoomInput();

            HandleTranslation(moveInput);
            HandleZoom(zoomInput);
        }

        private void HandleTranslation(Vector2 moveInput)
        {
            Vector3 moveDir = (Vector3.forward * moveInput.y) + (Vector3.right * moveInput.x);

            float heightFactor;
            if (_camera != null && _camera.orthographic)
            {
                heightFactor = Mathf.InverseLerp(_zoomLimits.x, _zoomLimits.y, _targetZoom);
            }
            else
            {
                heightFactor = Mathf.InverseLerp(_zoomLimits.x, _zoomLimits.y, Mathf.Abs(_targetZoom));
            }

            float currentSpeed = _moveSpeed * (0.5f + heightFactor);

            Vector3 targetMove = _targetPosition + (moveDir * currentSpeed * Time.deltaTime);

            Vector2 limitX = _cameraManager.BoundsCalculator.MapLimitX;
            Vector2 limitZ = _cameraManager.BoundsCalculator.MapLimitZ;

            targetMove.x = Mathf.Clamp(targetMove.x, limitX.x, limitX.y);
            targetMove.z = Mathf.Clamp(targetMove.z, limitZ.x, limitZ.y);

            _targetPosition.x = targetMove.x;
            _targetPosition.z = targetMove.z;
        }

        private void HandleZoom(float zoomInput)
        {
            if (_camera != null && _camera.orthographic)
            {
                // Orthographic size: Smaller = zoomed in. Scroll up (positive) = zoom in (decrease size)
                float zoomStep = -zoomInput * _zoomSpeed;
                _targetZoom += zoomStep;
                _targetZoom = Mathf.Clamp(_targetZoom, _zoomLimits.x, _zoomLimits.y);
            }
            else
            {
                // Perspective local Z: Typically negative. Scroll up (positive) = zoom in (move towards 0)
                float zoomStep = zoomInput * _zoomSpeed;
                _targetZoom += zoomStep;
                _targetZoom = Mathf.Clamp(_targetZoom, -_zoomLimits.y, -_zoomLimits.x);
            }
        }

        private void MoveRig()
        {
            // Smooth horizontal positioning
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _moveSmoothing);

            // Smooth zooming
            if (_camera != null && _camera.orthographic)
            {
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetZoom, Time.deltaTime * _zoomSmoothing);
            }
            else if (cameraTransform != null)
            {
                Vector3 targetLocalPos = cameraTransform.localPosition;
                targetLocalPos.z = _targetZoom;
                
                cameraTransform.localPosition = Vector3.Lerp(
                    cameraTransform.localPosition, 
                    targetLocalPos, 
                    Time.deltaTime * _zoomSmoothing
                );
            }
        }

        private void SetActiveControls(bool isActive)
        {
            if (!_isInitialized) return;
            
            if (isActive)
            {
                _cameraManager.InputHandler?.EnableControls();
            }
            else
            {
                _cameraManager.InputHandler?.DisableControls();
            }
        }

        private void SnapPosition(Vector3 groundPosition)
        {
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 currentLookPoint = ray.GetPoint(enter);
                Vector3 rigToGroundOffset = transform.position - currentLookPoint;

                _targetPosition = groundPosition + rigToGroundOffset;
                
                Vector2 limitX = _cameraManager.BoundsCalculator.MapLimitX;
                Vector2 limitZ = _cameraManager.BoundsCalculator.MapLimitZ;
                _targetPosition.x = Mathf.Clamp(_targetPosition.x, limitX.x, limitX.y);
                _targetPosition.z = Mathf.Clamp(_targetPosition.z, limitZ.x, limitZ.y);

                transform.position = _targetPosition;
                
            }
            else
            {
                // Fallback for flat camera
                _targetPosition = groundPosition;
                _targetPosition.y = transform.position.y;
                transform.position = _targetPosition;
            }
        }
    }
}
