namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// The playable races. Independent of <see cref="TeamNumber"/>: race is what you look like
    /// and what you spawn, team is which corner you occupy.
    ///
    /// These used to be the same thing — HumanBase was permanently Team1, OrcBase permanently
    /// Team2, and so on — so the corner the server happened to grant you decided your race.
    /// </summary>
    public enum RaceType : byte
    {
        Human = 0,
        Orc = 1,
        Soldier = 2,
        Monster = 3,
    }

    public static class Races
    {
        public const int Count = 4;
    }
}
