using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// One corner's occupancy, stored as a DynamicBuffer on the MatchStateTag entity and
    /// indexed directly by (int)TeamNumber.
    /// </summary>
    public struct TeamStatusElement : IBufferElementData
    {
        /// <summary>
        /// This corner belongs to someone — whether or not they are currently connected.
        ///
        /// A mid-match disconnect keeps this true and clears <see cref="OccupyingPlayer"/>: the
        /// corner keeps playing and is held for its owner, so nobody else may take it.
        /// </summary>
        public bool IsOccupied;

        /// <summary>
        /// The occupying player's connection entity, or Entity.Null.
        ///
        /// Null while <see cref="IsOccupied"/> is true means the owner has dropped out mid-match
        /// and is expected back — that pair is the only way to express "away".
        /// </summary>
        public Entity OccupyingPlayer;

        /// <summary>
        /// Stable identity of the owner, matched against GoInGameRequest.PlayerId to give a
        /// returning player their corner back. NetworkId cannot be used: it is reassigned on every
        /// reconnect.
        /// </summary>
        public FixedString64Bytes OwnerId;

        /// <summary>
        /// Race the occupant chose. Recorded at accept time so ServerStreamReadySystem can
        /// recover it later without a second round trip to the client.
        /// </summary>
        public RaceType Race;

        /// <summary>
        /// This corner's base was destroyed. The slot stays occupied on purpose — a dead corner
        /// is not up for grabs, and the eliminated player keeps watching rather than being
        /// silently replaced by the next person to connect.
        /// </summary>
        public bool IsEliminated;
    }

    /// <summary>Tag to find the match-state entity without a Singleton pattern.</summary>
    public struct MatchStateTag : IComponentData { }
}
