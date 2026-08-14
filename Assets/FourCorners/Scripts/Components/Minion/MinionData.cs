using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Minion
{
    public struct MinionData : IComponentData
    {
        /// <summary>Owning team. Copied from the spawner's PlayerBase at spawn time.</summary>
        [GhostField] public Team.TeamNumber TeamNumber;

        /// <summary>Movement speed in units/second. Baked from MinionAuthoring.</summary>
        [GhostField] public float Speed;

        /// <summary>Current wander destination. Only meaningful for minions without a lane.</summary>
        [GhostField] public float3 Target;

        /// <summary>Per-instance seed for movement noise. Assigned at spawn, not at bake.</summary>
        [GhostField] public uint RandomSeed;

        // The attack cooldown used to live here. It moved to Components.Combat.AttackCooldown so
        // that bases and towers — which have a reload timer but are not minions — can share the
        // one system that ticks it.
    }
}
