using Unity.Entities;

namespace FourCorners.Scripts.Components.Bounds
{
    /// <summary>
    /// Marks a base whose position has already been handed to the camera, so
    /// LocalPlayerCameraSystem snaps to it exactly once.
    ///
    /// A companion `CameraFocus` tag used to live here and was never referenced by anything.
    /// </summary>
    public struct CameraFocusInitializedTag : IComponentData { }
}
