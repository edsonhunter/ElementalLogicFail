using Unity.Entities;

namespace FourCorners.Scripts.Components.Spawner
{
    /// <summary>
    /// Server-side wave spawner. Not replicated.
    ///
    /// This previously carried [GhostComponent] with [GhostField] on NetworkId/IsActive, and a
    /// comment claiming clients could react to spawner activation. They could not: spawners are
    /// child entities of the base ghost, and Netcode does not serialise components on children
    /// without [GhostComponent(SendDataForChildEntity = true)]. Nothing client-side reads this,
    /// so the honest fix is to stop pretending it replicates.
    ///
    /// PlayerBase remains the sole authority for team identity and activation
    /// (see BaseAllocationSystem); the fields below are server-local mirrors.
    /// </summary>
    public struct SpawnerData : IComponentData
    {
        /// <summary>
        /// Baked link to the owning PlayerBase entity.
        /// All team identity and activation state is read from this entity via ComponentLookup.
        /// </summary>
        public Entity PlayerBaseEntity;

        public int SpawnAmount;
        public float SpawnInterval;
        public float Timer;

        /// <summary>
        /// Which of the owning base's three lanes this spawner walks (0, 1, 2).
        ///
        /// Each lane visits all enemy bases then returns home; the index only decides the
        /// starting rotation, so the three spawners of a base fan out instead of overlapping.
        /// Consumed by LaneBakingSystem.
        /// </summary>
        public int LaneIndex;

        /// <summary>NetworkId of the owning player, or 0 when the corner is unclaimed.</summary>
        public int NetworkId;

        /// <summary>Mirror of PlayerBase.IsActive. Never gate simulation on this — gate on the base.</summary>
        public bool IsActive;
    }
}
