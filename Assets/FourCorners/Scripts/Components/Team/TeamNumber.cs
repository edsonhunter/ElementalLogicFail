namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// The four corners. Member count must stay in sync with <see cref="Teams.Count"/>.
    ///
    /// A parallel TeamColor enum with six members used to live here, and MinionData blind-cast
    /// TeamNumber to it. Nothing ever rendered the result, so it has been removed rather than
    /// left as a mismatched pair waiting to be cast across.
    /// </summary>
    public enum TeamNumber
    {
        Team1 = 0,
        Team2 = 1,
        Team3 = 2,
        Team4 = 3,
    }
}
