using Unity.Entities;

namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// One corner's occupancy, stored as a DynamicBuffer on the MatchStateTag entity and
    /// indexed directly by (int)TeamNumber.
    /// </summary>
    public struct TeamStatusElement : IBufferElementData
    {
        public bool IsOccupied;

        /// <summary>The occupying player's connection entity, or Entity.Null.</summary>
        public Entity OccupyingPlayer;

        /// <summary>
        /// Race the occupant chose. Recorded at accept time so ServerStreamReadySystem can
        /// recover it later without a second round trip to the client.
        /// </summary>
        public RaceType Race;
    }

    /// <summary>Tag to find the match-state entity without a Singleton pattern.</summary>
    public struct MatchStateTag : IComponentData { }
}
