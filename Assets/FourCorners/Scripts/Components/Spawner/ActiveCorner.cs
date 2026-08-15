using Unity.Entities;

namespace FourCorners.Scripts.Components.Spawner
{
    /// <summary>
    /// Marks a corner that has been claimed and not yet torn down.
    ///
    /// This exists so that "a corner stopped playing" is a single observable event rather than a
    /// cleanup routine every caller has to remember. <see cref="PlayerBase.IsActive"/> alone
    /// cannot serve: it is false both for a corner nobody ever claimed and for one that has just
    /// died, and only the second needs its spawners silenced and its minions removed.
    ///
    /// Added by BaseAllocationSystem on claim, removed by CornerTeardownSystem once the corner has
    /// been cleaned up. Anything that wants to retire a corner just sets IsActive = false.
    /// </summary>
    public struct ActiveCorner : IComponentData { }
}
