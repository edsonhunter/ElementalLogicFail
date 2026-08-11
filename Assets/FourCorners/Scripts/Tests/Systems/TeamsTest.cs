using System;
using FourCorners.Scripts.Components.Team;
using NUnit.Framework;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class TeamsTest
    {
        /// <summary>
        /// Teams.Count cannot be derived from the enum at compile time, so this guards the one
        /// thing that would silently break if a fifth corner were ever added: the match
        /// bootstrap seeds exactly Teams.Count slots, and a mismatch means a corner that can
        /// never be claimed (or a slot with no corner behind it).
        /// </summary>
        [Test]
        public void TeamsCount_MatchesTeamNumberMemberCount()
        {
            Assert.AreEqual(Enum.GetValues(typeof(TeamNumber)).Length, Teams.Count);
        }

        [Test]
        public void TeamNumber_ValuesAreContiguousFromZero()
        {
            // TeamStatusElement is a DynamicBuffer indexed directly by (int)TeamNumber, so any
            // gap or non-zero start would index the wrong slot.
            var values = (TeamNumber[])Enum.GetValues(typeof(TeamNumber));
            for (int i = 0; i < values.Length; i++)
            {
                Assert.AreEqual(i, (int)values[i], $"TeamNumber.{values[i]} must equal {i}.");
            }
        }
    }
}
