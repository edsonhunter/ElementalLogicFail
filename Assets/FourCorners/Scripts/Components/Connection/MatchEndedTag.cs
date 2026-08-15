using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Client-world singleton created by ClientMatchEndedSystem when MatchEndedRpc arrives.
    ///
    /// Mirrors <see cref="MatchStartedTag"/>: an unmanaged marker that BridgeNotificationSystem
    /// turns into a managed event, keeping the RPC consumer free of managed lookups.
    ///
    /// The win/lose comparison is resolved here rather than in the UI because the answer needs
    /// this world's own NetworkId, which is a simulation-side fact.
    /// </summary>
    public struct MatchEndedTag : IComponentData
    {
        /// <summary>NetworkId of the winner, or 0 if nobody survived.</summary>
        public int WinnerNetworkId;

        /// <summary>True when the winner is this client.</summary>
        public bool LocalPlayerWon;
    }
}
