using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Client-world singleton raised by ClientDisconnectSystem when this client loses the server —
    /// whether it left on purpose, was dropped, or the host ended the match.
    ///
    /// Mirrors <see cref="MatchStartedTag"/>: an unmanaged marker that BridgeNotificationSystem
    /// turns into a managed event, so the RPC/transport layer never needs a managed lookup.
    /// It is consumed (destroyed) by BridgeNotificationSystem once fanned out, because unlike
    /// MatchStartedTag it describes an instant rather than a state.
    /// </summary>
    public struct ClientDisconnectedTag : IComponentData { }
}
