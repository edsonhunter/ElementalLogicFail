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

        /// <summary>
        /// Winner's NetworkId once <see cref="Phase"/> is <see cref="MatchPhase.Ended"/>, or 0 if
        /// the match ended with nobody left — a mutual kill, or everyone having walked out.
        /// </summary>
        public int WinnerNetworkId;

        /// <summary>Seconds since the match went Active. Drives sudden death.</summary>
        public float ElapsedSeconds;

        /// <summary>
        /// True once the clock has passed the sudden-death threshold and bases have started
        /// taking damage on their own. Latched so the escalation cannot be un-triggered.
        /// </summary>
        public bool SuddenDeathActive;
    }
}
