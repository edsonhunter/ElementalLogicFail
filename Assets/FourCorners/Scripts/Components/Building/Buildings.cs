namespace FourCorners.Scripts.Components.Building
{
    /// <summary>
    /// Counts that describe a corner's layout, in the same spirit as <c>Teams.Count</c> — the
    /// number was already written as a literal 3 in the command overlay and about to be written
    /// again in the upgrade handler.
    /// </summary>
    public static class Buildings
    {
        /// <summary>
        /// Barracks per corner, and therefore the range of valid <c>BaseCommand.TargetSlot</c>
        /// values for an upgrade. Fixed by the subscene: each PlayerBaseAuthoring object has
        /// exactly three SpawnerAuthoring children, one per lane.
        /// </summary>
        public const int BarracksPerBase = 3;
    }
}
