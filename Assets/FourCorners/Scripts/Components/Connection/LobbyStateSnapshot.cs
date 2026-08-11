using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Client-world singleton holding the latest lobby state received from the server.
    /// Written by the unmanaged ClientLobbyStateSystem, read by the managed
    /// BridgeNotificationSystem which fans it out to the UI.
    ///
    /// <see cref="Version"/> increments on every server update so the managed reader can
    /// detect a change without needing to observe the RPC entity itself.
    /// </summary>
    public struct LobbyStateSnapshot : IComponentData
    {
        public bool IsHost;
        public int PlayerCount;
        public uint Version;
    }
}
