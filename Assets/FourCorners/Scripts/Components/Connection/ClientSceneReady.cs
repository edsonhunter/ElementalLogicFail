using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// The single client-side readiness signal: this world has everything it needs to start
    /// receiving ghost snapshots.
    ///
    /// INVARIANT: ClientSceneReadySystem is the only producer. If a second path to signal
    /// readiness seems necessary, that is the bug — three competing producers of the old
    /// SceneLoadedTag is what deadlocked the handshake in multi-client sessions.
    /// </summary>
    public struct ClientSceneReady : IComponentData { }
}
