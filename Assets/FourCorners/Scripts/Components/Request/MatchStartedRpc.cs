using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// Broadcast RPC sent from the Server to ALL connected clients when
    /// the host successfully starts the game (MatchState transitions to Active).
    /// ClientMatchStartedSystem receives this and fires ISystemBridgeService.OnMatchStarted,
    /// which triggers every client's scene transition from Lobby → Gameplay.
    ///
    /// NOTE: no [GhostComponent] here — see ReadyForGhostsRequest. On an IRpcCommand it emits a
    /// competing ghost serializer that silently drops the payload on non-IPC transports.
    /// </summary>
    public struct MatchStartedRpc : IRpcCommand { }
}
