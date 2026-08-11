using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Holds the current phase of the match. Stored on the MatchStateTag entity.
    /// Server is sole writer. Clients receive updates via LobbyStateUpdateRpc.
    /// </summary>
    public struct MatchState : IComponentData
    {
        public MatchPhase Phase;

        /// <summary>
        /// NetworkId of the player currently authorised to start the match, or 0 for none.
        ///
        /// Explicit rather than "whoever sits at index 0 of the connected-player buffer".
        /// That positional rule held only because players were never removed; once disconnect
        /// handling compacts the buffer it silently promotes an arbitrary player.
        /// </summary>
        public int HostNetworkId;
    }
}
