using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Connection;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class MatchOutcomeSystemTest : ECSTestFixture
    {
        private Entity _matchState;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _matchState = EntityTest.CreateMatchStateWithSlots(EntityManager, MatchPhase.Active);
        }

        /// <summary>Occupies a corner with a connection that owns <paramref name="networkId"/>.</summary>
        private void Occupy(TeamNumber team, int networkId, bool eliminated = false)
        {
            var connection = EntityManager.CreateEntity(typeof(NetworkId));
            EntityManager.SetComponentData(connection, new NetworkId { Value = networkId });

            var slots = EntityManager.GetBuffer<TeamStatusElement>(_matchState);
            slots[(int)team] = new TeamStatusElement
            {
                IsOccupied = true,
                OccupyingPlayer = connection,
                IsEliminated = eliminated
            };

            var roster = EntityManager.GetBuffer<ConnectedPlayerElement>(_matchState);
            roster.Add(new ConnectedPlayerElement { NetworkId = networkId, ConnectionEntity = connection });
        }

        private void Tick()
        {
            World.GetOrCreateSystem<MatchOutcomeSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private MatchState State() => EntityManager.GetComponentData<MatchState>(_matchState);

        private int SentRpcCount()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchEndedRpc>());
            return query.CalculateEntityCount();
        }

        /// <summary>
        /// The case that would break a match at kickoff: bases are not activated for several
        /// frames after the phase goes Active, so an outcome check driven by active bases would
        /// declare a winner before anyone had one.
        /// </summary>
        [Test]
        public void MatchOutcomeSystem_DoesNotEndWhileTwoPlayersRemain()
        {
            Occupy(TeamNumber.Team1, networkId: 1);
            Occupy(TeamNumber.Team2, networkId: 2);

            Tick();

            Assert.AreEqual(MatchPhase.Active, State().Phase);
            Assert.AreEqual(0, SentRpcCount());
        }

        [Test]
        public void MatchOutcomeSystem_EndsWithTheLastPlayerStanding()
        {
            Occupy(TeamNumber.Team1, networkId: 1);
            Occupy(TeamNumber.Team2, networkId: 2, eliminated: true);

            Tick();

            Assert.AreEqual(MatchPhase.Ended, State().Phase);
            Assert.AreEqual(1, State().WinnerNetworkId);
        }

        [Test]
        public void MatchOutcomeSystem_NotifiesEveryConnectedPlayer()
        {
            Occupy(TeamNumber.Team1, networkId: 1);
            Occupy(TeamNumber.Team2, networkId: 2, eliminated: true);

            Tick();

            // Both players are told, including the one who lost.
            Assert.AreEqual(2, SentRpcCount());
        }

        /// <summary>
        /// Left Active with nobody in it, the server would sit forever and the next player to
        /// connect would join a match that cannot be won.
        /// </summary>
        [Test]
        public void MatchOutcomeSystem_EndsWithNoWinnerWhenEveryoneIsGone()
        {
            Occupy(TeamNumber.Team1, networkId: 1, eliminated: true);
            Occupy(TeamNumber.Team2, networkId: 2, eliminated: true);

            Tick();

            Assert.AreEqual(MatchPhase.Ended, State().Phase);
            Assert.AreEqual(0, State().WinnerNetworkId);
        }

        [Test]
        public void MatchOutcomeSystem_IgnoresMatchesThatHaveNotStarted()
        {
            EntityManager.SetComponentData(_matchState, new MatchState { Phase = MatchPhase.Lobby });
            Occupy(TeamNumber.Team1, networkId: 1);

            Tick();

            Assert.AreEqual(MatchPhase.Lobby, State().Phase);
            Assert.AreEqual(0, SentRpcCount());
        }
    }
}
