using FourCorners.Scripts.Components.Team;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Request
{
    /// <summary>
    /// Client → server join request carrying the player's lobby choices.
    /// The server is free to override the team (that slot may already be taken); the race is
    /// always honoured, since races are not exclusive.
    /// </summary>
    public struct GoInGameRequest : IRpcCommand
    {
        /// <summary>Preferred corner (0..Teams.Count-1), or -1 for "any free slot".</summary>
        public int RequestedTeamIndex;

        /// <summary>Chosen race. Decides base visuals and the minion roster.</summary>
        public RaceType RequestedRace;
    }
}
