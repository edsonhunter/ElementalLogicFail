using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// Sent by the Host client to the server to request transitioning the match
    /// from Lobby → Active, which unblocks minion spawning.
    /// The server validates that: (a) sender has HostTag, (b) PlayerCount >= 2.
    ///
    /// NOTE: no [GhostComponent] here — see ReadyForGhostsRequest. On an IRpcCommand it emits a
    /// competing ghost serializer that silently drops the payload on non-IPC transports.
    /// </summary>
    public struct StartGameRequest : IRpcCommand { }
}
