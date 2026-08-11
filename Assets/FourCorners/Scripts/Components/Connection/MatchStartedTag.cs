using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Client-world singleton created by ClientMatchStartedSystem when MatchStartedRpc arrives.
    ///
    /// Decouples the unmanaged RPC consumer from the managed scene transition:
    /// ClientSceneReadySystem and BridgeNotificationSystem both observe this tag rather than
    /// the RPC, so neither needs a managed lookup inside an ISystem.
    /// </summary>
    public struct MatchStartedTag : IComponentData { }
}
