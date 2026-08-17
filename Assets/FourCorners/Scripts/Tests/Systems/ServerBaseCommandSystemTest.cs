using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Command;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// The trust boundary of the whole gameplay input path, so it is worth being thorough about.
    ///
    /// Everything here turns on one property: a client's message says what it wants done, never to
    /// whom. The corner is derived from the connection the message arrived on, which is state only
    /// the server writes — so the tests that matter most are the ones showing a sender cannot reach
    /// a corner that is not theirs, no matter what they send.
    /// </summary>
    [TestFixture]
    public class ServerBaseCommandSystemTest : ECSTestFixture
    {
        private Entity _matchState;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _matchState = EntityTest.CreateMatchStateWithSlots(EntityManager, MatchPhase.Active);
        }

        /// <summary>Gives <paramref name="team"/> an owner and an activated base, and returns the connection.</summary>
        private Entity OccupyWithLiveBase(TeamNumber team, bool eliminated = false)
        {
            var connection = EntityManager.CreateEntity();

            var slots = EntityManager.GetBuffer<TeamStatusElement>(_matchState);
            slots[(int)team] = new TeamStatusElement
            {
                IsOccupied = true,
                OccupyingPlayer = connection,
                IsEliminated = eliminated
            };

            EntityTest.CreateTestPlayerBase(EntityManager, team);
            return connection;
        }

        private void Tick()
        {
            World.GetOrCreateSystem<ServerBaseCommandSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        private NativeArray<BaseCommand> AcceptedCommands()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommand>());
            return query.ToComponentDataArray<BaseCommand>(Allocator.Temp);
        }

        private BaseCommandRejection SoleRejection()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommandRejectedRpc>());
            using var rejections = query.ToComponentDataArray<BaseCommandRejectedRpc>(Allocator.Temp);

            Assert.AreEqual(1, rejections.Length, "Expected exactly one rejection.");
            return rejections[0].Reason;
        }

        private int RejectionCount()
        {
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommandRejectedRpc>());
            return query.CalculateEntityCount();
        }

        [Test]
        public void ServerBaseCommand_AcceptsACommandFromTheCornersOwner()
        {
            var connection = OccupyWithLiveBase(TeamNumber.Team2);
            EntityTest.CreateBaseCommandRpc(
                EntityManager, connection, BaseCommandType.UpgradeBarracks, targetSlot: 2);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(1, accepted.Length);
            Assert.AreEqual(BaseCommandType.UpgradeBarracks, accepted[0].Type);
            Assert.AreEqual(TeamNumber.Team2, accepted[0].Team);
            Assert.AreEqual(2, accepted[0].TargetSlot);
            Assert.AreEqual(connection, accepted[0].SourceConnection);
            Assert.AreEqual(0, RejectionCount());
        }

        /// <summary>
        /// The command names no base, so the only base it can possibly reach is the sender's. This
        /// is the property that makes the channel safe, and the one a future refactor is most
        /// likely to lose by "helpfully" adding a target to the payload.
        /// </summary>
        [Test]
        public void ServerBaseCommand_AddressesTheSendersOwnCornerAndNoOther()
        {
            var teamOne = OccupyWithLiveBase(TeamNumber.Team1);
            var teamThree = OccupyWithLiveBase(TeamNumber.Team3);

            EntityTest.CreateBaseCommandRpc(EntityManager, teamThree, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(1, accepted.Length);
            Assert.AreEqual(TeamNumber.Team3, accepted[0].Team);
            Assert.AreNotEqual(teamOne, accepted[0].SourceConnection);

            var addressed = EntityManager.GetComponentData<PlayerBase>(accepted[0].BaseEntity);
            Assert.AreEqual(TeamNumber.Team3, addressed.TeamNumber);
        }

        [Test]
        public void ServerBaseCommand_RejectsASenderThatHoldsNoCorner()
        {
            OccupyWithLiveBase(TeamNumber.Team1);
            var stranger = EntityManager.CreateEntity();

            EntityTest.CreateBaseCommandRpc(EntityManager, stranger, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(BaseCommandRejection.NotYourBase, SoleRejection());
        }

        /// <summary>
        /// A corner held for a player who dropped out keeps fighting but stops building — its slot
        /// carries a null connection, which matches nobody.
        /// </summary>
        [Test]
        public void ServerBaseCommand_RejectsACommandAimedAtAHeldCorner()
        {
            var connection = EntityManager.CreateEntity();

            var slots = EntityManager.GetBuffer<TeamStatusElement>(_matchState);
            slots[(int)TeamNumber.Team1] = new TeamStatusElement
            {
                IsOccupied = true,
                OccupyingPlayer = Entity.Null
            };
            EntityTest.CreateTestPlayerBase(EntityManager, TeamNumber.Team1);

            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(BaseCommandRejection.NotYourBase, SoleRejection());
        }

        [Test]
        public void ServerBaseCommand_RejectsAnEliminatedPlayer()
        {
            var connection = OccupyWithLiveBase(TeamNumber.Team1, eliminated: true);
            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(BaseCommandRejection.Eliminated, SoleRejection());
        }

        [Test]
        public void ServerBaseCommand_RejectsWhileTheMatchIsNotRunning()
        {
            EntityManager.SetComponentData(_matchState, new MatchState { Phase = MatchPhase.Lobby });
            var connection = OccupyWithLiveBase(TeamNumber.Team1);

            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(BaseCommandRejection.MatchNotActive, SoleRejection());
        }

        [Test]
        public void ServerBaseCommand_RejectsAnUnsetCommandType()
        {
            var connection = OccupyWithLiveBase(TeamNumber.Team1);
            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.None);

            Tick();

            Assert.AreEqual(BaseCommandRejection.MalformedCommand, SoleRejection());
        }

        /// <summary>
        /// Normal for the few frames between a corner being granted and BaseAllocationSystem
        /// activating it — the player has a slot but nothing to command yet.
        /// </summary>
        [Test]
        public void ServerBaseCommand_RejectsWhenTheCornerHasNoLiveBase()
        {
            var connection = EntityManager.CreateEntity();

            var slots = EntityManager.GetBuffer<TeamStatusElement>(_matchState);
            slots[(int)TeamNumber.Team1] = new TeamStatusElement
            {
                IsOccupied = true,
                OccupyingPlayer = connection
            };
            EntityTest.CreateTestPlayerBase(EntityManager, TeamNumber.Team1, isActive: false);

            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.UpgradeCentral);

            Tick();

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(BaseCommandRejection.BaseUnavailable, SoleRejection());
        }

        /// <summary>
        /// Recording an RPC addressed to a connection that no longer exists throws during ECB
        /// playback — and a throw there discards every command every system buffered that frame, so
        /// this would surface as some unrelated system mysteriously failing.
        /// </summary>
        [Test]
        public void ServerBaseCommand_SaysNothingToAConnectionThatHasAlreadyGone()
        {
            var connection = OccupyWithLiveBase(TeamNumber.Team1);
            EntityTest.CreateBaseCommandRpc(EntityManager, connection, BaseCommandType.UpgradeCentral);
            EntityManager.DestroyEntity(connection);

            Assert.DoesNotThrow(Tick);

            using var accepted = AcceptedCommands();
            Assert.AreEqual(0, accepted.Length);
            Assert.AreEqual(0, RejectionCount(), "There is nobody left to tell.");
        }

        /// <summary>
        /// Every request is destroyed on the frame it is read, accepted or not. An RPC path that
        /// only cleans up on the happy branch accumulates entities forever — the ClientSceneReady
        /// bug, which reached 938 instances before anything noticed.
        /// </summary>
        [Test]
        public void ServerBaseCommand_ConsumesTheRequestEvenWhenItRefusesIt()
        {
            var stranger = EntityManager.CreateEntity();
            EntityTest.CreateBaseCommandRpc(EntityManager, stranger, BaseCommandType.UpgradeCentral);

            Tick();

            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BaseCommandRequest>());
            Assert.AreEqual(0, query.CalculateEntityCount());
        }
    }
}
