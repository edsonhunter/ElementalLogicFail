using FourCorners.Scripts.Components.Team;
using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// What this client asked for in the lobby. Written by the UI through ISystemBridgeService,
    /// read once by ClientRequestGameSystem when the connection comes up.
    ///
    /// Replaces ClientRequestGameSystem.DesiredTeamIndex, a `public static int` on an ISystem
    /// that nothing ever wrote — so team choice never reached the server at all. A static is
    /// also process-scoped rather than world-scoped, so every client world in a Multiplayer
    /// Play Mode session would have shared one value.
    /// </summary>
    public struct LocalPlayerSelection : IComponentData
    {
        /// <summary>Preferred corner (0..Teams.Count-1), or -1 for "any free slot".</summary>
        public int DesiredTeamIndex;

        /// <summary>Chosen race. Independent of which corner is granted.</summary>
        public RaceType Race;

        public static LocalPlayerSelection Default => new()
        {
            DesiredTeamIndex = -1,
            Race = RaceType.Human
        };
    }
}
