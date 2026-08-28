namespace FourCorners.Scripts.Components.Building
{
    /// <summary>
    /// What kind of thing a <see cref="BuildingData"/> describes.
    ///
    /// The two that exist are not new entities: the corner base *is* the central building, and the
    /// three spawners already parented to it *are* the barracks. Modelling them as separate
    /// entities would have duplicated the team ownership, the transform and the lifetime of things
    /// that already exist and already work.
    /// </summary>
    public enum BuildingType : byte
    {
        /// <summary>Unset. A building that reports this was never baked properly.</summary>
        None = 0,

        /// <summary>The corner base itself. Its level drives passive income.</summary>
        Central = 1,

        /// <summary>A spawner. Its level drives how fast and how many it produces.</summary>
        Barracks = 2
    }
}
