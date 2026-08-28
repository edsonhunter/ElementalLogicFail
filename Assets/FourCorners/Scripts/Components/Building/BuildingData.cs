using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Building
{
    /// <summary>
    /// The upgrade state of one building, and deliberately the *only* mutable thing an upgrade
    /// writes.
    ///
    /// Everything a level affects is derived from it at the point of use rather than baked into the
    /// thing it affects: SpawnerSystem computes an effective interval and wave size from
    /// <see cref="Level"/>, IncomeSystem computes an effective rate from it. The tempting
    /// alternative — having the upgrade handler decrement SpawnerData.SpawnInterval directly — has
    /// no way back. The authored value would be overwritten, so nothing would know what level zero
    /// used to look like, and resetting a corner for a new occupant would need a second copy of
    /// every baseline. Derivation keeps the authored numbers meaning exactly what the inspector
    /// says, and makes a reset one assignment.
    ///
    /// <c>SendDataForChildEntity</c> is not optional here. Barracks are child entities of the base
    /// ghost, and NetCode silently declines to serialise components on children without it — the
    /// levels would replicate perfectly on the host and not at all over the wire, which is the
    /// class of bug that only shows up on someone else's screen.
    /// </summary>
    [GhostComponent(SendDataForChildEntity = true)]
    public struct BuildingData : IComponentData
    {
        public BuildingType Type;

        /// <summary>
        /// Which of its kind this is, and the value a client puts in
        /// <c>BaseCommandRequest.TargetSlot</c> to address it. Zero for a Central, since there is
        /// only ever one.
        ///
        /// Replicated, and deliberately not borrowed from <c>SpawnerData.LaneIndex</c> even though
        /// both are baked from the same authoring field. SpawnerData is server-only, so a client
        /// reading it to build an upgrade UI would find nothing at all — and beyond that, which
        /// lane a barracks feeds and how a player names it are two different ideas that only
        /// happen to coincide today. Nothing compares the two, so there is nothing to drift.
        /// </summary>
        [GhostField] public byte Slot;

        /// <summary>
        /// Upgrades bought so far. Zero is a legitimate value — an un-upgraded building — so unlike
        /// <see cref="BuildingType"/> there is nothing invalid to distinguish here.
        /// </summary>
        [GhostField] public int Level;
    }
}
