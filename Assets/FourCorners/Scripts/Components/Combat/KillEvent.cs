using Unity.Entities;

namespace FourCorners.Scripts.Components.Combat
{
    /// <summary>
    /// A unit died, and this is who is responsible. Emitted by the damage-apply pass, which is the
    /// only place in the project that knows both facts at once.
    ///
    /// Carries teams rather than entities on purpose. A frame passes before anything reads this,
    /// and the most interesting kill — two minions trading fatal blows — leaves the killer dead and
    /// destroyed by then. Entity references would go stale exactly when the reward matters most,
    /// so the teams are snapshotted at the moment of the blow.
    ///
    /// Consumed and destroyed by BountySystem. Unlike <c>BaseCommand</c> there is no cleanup
    /// system, because there is exactly one consumer and it can do its own tidying.
    /// </summary>
    public struct KillEvent : IComponentData
    {
        /// <summary>The team that lands the killing blow and collects the bounty.</summary>
        public Team.TeamNumber KillerTeam;

        /// <summary>The team that lost a unit. Unused today; a scoreboard will want it.</summary>
        public Team.TeamNumber VictimTeam;
    }
}
