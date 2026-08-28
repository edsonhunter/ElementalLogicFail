using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Components.Economy
{
    /// <summary>
    /// A corner's purse. Lives on the <c>PlayerBase</c> entity, not on the connection.
    ///
    /// That placement is the whole design decision. A player who drops out mid-match keeps their
    /// corner — the base stands, the spawners run, the minions walk — so the gold that base is
    /// earning has to survive the connection that was watching it. Hanging this off the connection
    /// entity would delete a player's economy the moment their wifi blinked, and hand them a
    /// bankrupt corner on reconnect.
    ///
    /// Its own component rather than fields on <c>PlayerBase</c>: that ghost is authored
    /// <c>OptimizationMode.Static</c> because its contents almost never change, and gold changes
    /// every second. <c>Health</c> is split out for the same reason and is the pattern to follow.
    ///
    /// Replicated to everybody, not just the owner. Nothing in the project authors a
    /// <c>GhostOwner</c>, so <c>SendToOwner</c> filtering is unavailable — and in a four-way FFA
    /// where you can already count someone's minions, a public treasury is a fair simplification
    /// rather than a leak. Revisit only if hidden economy becomes a design goal.
    /// </summary>
    public struct PlayerEconomy : IComponentData
    {
        [GhostField] public int Gold;

        /// <summary>
        /// The authored opening balance, kept so a corner can be reset when a *new* player claims
        /// it. Same relationship as <c>Health.Max</c> to <c>Health.Current</c>: without it, the
        /// only thing to restore to would be a constant duplicated away from the authored value.
        ///
        /// Not replicated — nothing on a client needs to know what the balance used to be.
        /// </summary>
        public int StartingGold;

        /// <summary>
        /// Passive earnings per second, including everything the central building's level adds.
        /// Replicated so the HUD can show the rate as well as the balance — "why am I poor" is a
        /// question the number alone cannot answer.
        ///
        /// Derived, not authored: IncomeSystem recomputes it from
        /// <see cref="BaseIncomePerSecond"/> and the building level every tick. Nothing else should
        /// write it, or the next tick will overwrite whatever it wrote.
        /// </summary>
        [GhostField] public int IncomePerSecond;

        /// <summary>
        /// The authored rate before any upgrade, never modified. Same baseline-versus-current split
        /// as <see cref="StartingGold"/> and <c>Health.Max</c>: without it, an upgrade would have to
        /// overwrite the authored number and no reset could ever find its way home.
        /// </summary>
        public int BaseIncomePerSecond;

        /// <summary>
        /// Sub-coin remainder carried between frames. Server-only: no <c>[GhostField]</c>, because
        /// clients have no use for it and it would churn the snapshot every single frame.
        ///
        /// Without it, truncating <c>IncomePerSecond * deltaTime</c> to an int every frame floors
        /// to zero at any plausible frame rate and the player earns nothing at all.
        /// </summary>
        public float Accrued;
    }
}
