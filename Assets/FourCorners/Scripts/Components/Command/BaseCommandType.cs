namespace FourCorners.Scripts.Components.Command
{
    /// <summary>
    /// What a player is asking their own base to do.
    ///
    /// One discriminator on one RPC rather than an RPC type per action. Every IRpcCommand costs a
    /// generated serializer and a slot in the RPC collection, and these payloads are identical —
    /// splitting is worth it only when a command genuinely needs different data, at which point it
    /// is a different message rather than a wider one.
    ///
    /// Nothing here is handled yet: Tier 1.1 builds the channel, and the systems that spend gold
    /// and raise building levels arrive in 1.2/1.3. Until then an accepted command is delivered,
    /// found to have no handler, and reported by BaseCommandCleanupSystem.
    /// </summary>
    public enum BaseCommandType : byte
    {
        /// <summary>
        /// Never sent deliberately. A command arriving as None is a malformed message — a default
        /// struct that escaped somewhere — and the server rejects it rather than guessing.
        /// </summary>
        None = 0,

        /// <summary>Raise one barracks a level, addressed by <c>TargetSlot</c>.</summary>
        UpgradeBarracks = 1,

        /// <summary>Raise the central building a level. <c>TargetSlot</c> is unused.</summary>
        UpgradeCentral = 2
    }
}
