using FourCorners.Scripts.Components.Command;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// A player asking the server to do something to their base. The first gameplay message in the
    /// project — every other IRpcCommand here is connection or lobby plumbing.
    ///
    /// Note what this does *not* carry: any identification of the base being commanded. The server
    /// derives that from the connection the RPC arrived on, so a client can only ever command the
    /// corner it actually occupies. A base entity, team index or NetworkId in this payload would be
    /// a client asserting something the server must decide, and the first thing anyone would forge.
    ///
    /// NOTE: no [GhostComponent] here — see ReadyForGhostsRequest. On an IRpcCommand it emits a
    /// competing ghost serializer that silently drops the payload on non-IPC transports.
    /// </summary>
    public struct BaseCommandRequest : IRpcCommand
    {
        public BaseCommandType Type;

        /// <summary>
        /// Which slot of the addressed building, for commands where that means anything.
        /// Unvalidated client input until a handler bounds-checks it.
        /// </summary>
        public byte TargetSlot;
    }
}
