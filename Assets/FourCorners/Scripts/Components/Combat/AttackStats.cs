using Unity.Entities;

namespace FourCorners.Scripts.Components.Combat
{
    /// <summary>
    /// What an attacker does when it lands a blow. Baked, then constant for the entity's life —
    /// buffs and upgrades will produce new values rather than mutating these in place.
    ///
    /// Server-only. Clients never resolve combat, so replicating this would be pure bandwidth.
    /// </summary>
    public struct AttackStats : IComponentData
    {
        /// <summary>Hit points removed per landed attack.</summary>
        public int Damage;

        /// <summary>Seconds between attacks. Reloads <see cref="AttackCooldown.Remaining"/>.</summary>
        public float Interval;

        /// <summary>
        /// How close the target must be to be hit. EngagementSystem breaks off at a multiple of
        /// this, so the two values together give the hysteresis that stops a duel flickering.
        /// </summary>
        public float Range;
    }

    /// <summary>
    /// Time left before this entity may attack again. Ticked by AttackCooldownSystem, consumed by
    /// AttackSystem.
    ///
    /// Separate from <see cref="AttackStats"/> because it changes every frame while the stats do
    /// not, and separate from MinionData — where it used to live — because a tower has a reload
    /// timer and no concept of being a minion.
    /// </summary>
    public struct AttackCooldown : IComponentData
    {
        public float Remaining;
    }
}
