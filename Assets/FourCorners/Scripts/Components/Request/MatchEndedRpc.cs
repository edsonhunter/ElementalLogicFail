using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// Server → every client: the match is over.
    ///
    /// NEVER add [GhostComponent] to this. Codegen would emit a ghost serializer that competes
    /// with the RPC serializer and silently drops the payload on non-IPC transports — it works
    /// over local IPC and fails over Relay.
    /// </summary>
    public struct MatchEndedRpc : IRpcCommand
    {
        /// <summary>NetworkId of the last player standing, or 0 if nobody survived.</summary>
        public int WinnerNetworkId;
    }
}
