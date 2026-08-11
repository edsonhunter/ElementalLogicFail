namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// Single source of truth for the number of corners / team slots.
    ///
    /// This value was previously hardcoded as a literal 4 in the match bootstrap, the server's
    /// rejection log, the lobby label and the relay max-players argument — four places that
    /// could drift apart independently.
    ///
    /// Enum member counts cannot be a compile-time const, so this is asserted against
    /// <see cref="TeamNumber"/> in TeamsTests.
    /// </summary>
    public static class Teams
    {
        public const int Count = 4;
    }
}
