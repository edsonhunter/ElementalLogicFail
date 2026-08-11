using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Spawner
{
    /// <summary>
    /// A corner base. The sole authority for team identity and activation — spawners derive
    /// both from here via SpawnerData.PlayerBaseEntity.
    /// </summary>
    [GhostComponent]
    public struct PlayerBase : IComponentData
    {
        /// <summary>
        /// Which corner this is. Baked from PlayerBaseAuthoring and never reassigned.
        ///
        /// Replicated: without [GhostField] every base reported Team1 on clients, so any
        /// client-side visual keyed on team silently rendered the wrong thing.
        /// </summary>
        [GhostField] public Team.TeamNumber TeamNumber;

        /// <summary>
        /// Race the occupying player chose. Replicated so clients can render the right visuals.
        /// Meaningless while <see cref="IsActive"/> is false.
        /// </summary>
        [GhostField] public Team.RaceType Race;

        /// <summary>True once a player has claimed this corner. Drives BaseVisibilitySystem.</summary>
        [GhostField] public bool IsActive;

        /// <summary>NetworkId of the owning player, or 0 when unclaimed.</summary>
        [GhostField] public int NetworkId;
    }
}
