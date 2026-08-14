using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Combat
{
    /// <summary>
    /// Hit points for anything that can be damaged — minions today, bases and towers later.
    ///
    /// Deliberately not part of MinionData: bases and towers have no MinionData, and combat
    /// systems should not have to know what kind of thing they are hurting.
    ///
    /// Replicated because the client is the only place health is ever *shown*. Integers rather
    /// than floats so a kill is exact — no "0.0001 HP survivor" from float accumulation, and no
    /// quantisation decision to make when it goes over the wire.
    /// </summary>
    public struct Health : IComponentData
    {
        [GhostField] public int Current;
        [GhostField] public int Max;
    }

    /// <summary>
    /// Marks an entity that should be destroyed outright when its <see cref="Health"/> reaches
    /// zero, rather than surviving in some deactivated form.
    ///
    /// Minions carry this. A player base deliberately will not: when a corner dies it has to be
    /// deactivated and kept around (the team slot, the ghost, the visuals), not deleted. Without
    /// this tag, DeathSystem would silently start eating bases the moment Tier 0.2 gives them
    /// health.
    /// </summary>
    public struct DestroyOnDeath : IComponentData { }
}
