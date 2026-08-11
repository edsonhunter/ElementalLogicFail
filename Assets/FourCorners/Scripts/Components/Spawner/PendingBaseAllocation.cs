using FourCorners.Scripts.Components.Team;
using Unity.Entities;

namespace FourCorners.Scripts.Components.Spawner
{
    /// <summary>
    /// Added to a server-side connection entity once the team slot is granted. Carries both the
    /// approved corner and the chosen race so BaseAllocationSystem can bind the player to the
    /// exact base — no sequential first-available fallback needed.
    /// </summary>
    public struct PendingBaseAllocation : IComponentData
    {
        public TeamNumber ApprovedTeam;
        public RaceType ApprovedRace;
    }
}
