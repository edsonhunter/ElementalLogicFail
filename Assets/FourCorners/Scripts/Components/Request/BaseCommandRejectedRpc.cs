using FourCorners.Scripts.Components.Command;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// The server declining a <see cref="BaseCommandRequest"/>, sent only to whoever asked.
    ///
    /// There is no matching "accepted" message on purpose. Success is already observable: the gold
    /// drops, the building level rises, and both are replicated state the client is watching
    /// anyway. An acknowledgement would be a second, slower channel saying the same thing, and one
    /// the HUD could disagree with.
    ///
    /// NOTE: no [GhostComponent] here — see ReadyForGhostsRequest. On an IRpcCommand it emits a
    /// competing ghost serializer that silently drops the payload on non-IPC transports.
    /// </summary>
    public struct BaseCommandRejectedRpc : IRpcCommand
    {
        /// <summary>Echoed back so a client with several commands in flight knows which one failed.</summary>
        public BaseCommandType Type;

        public BaseCommandRejection Reason;
    }
}
