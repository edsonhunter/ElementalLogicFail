using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Team;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// Covers the slot bookkeeping that makes a mid-match reconnect possible.
    ///
    /// The accept and disconnect systems themselves need a live NetCode connection to drive, which
    /// the bare-World fixture cannot produce — so these exercise the rule those systems encode:
    /// a held corner is occupied with no connection attached, and only its owner can take it back.
    /// </summary>
    [TestFixture]
    public class TeamSlotReclaimTest
    {
        private static TeamStatusElement Held(string ownerId, bool eliminated = false) => new()
        {
            IsOccupied = true,
            OccupyingPlayer = Entity.Null,
            OwnerId = new FixedString64Bytes(ownerId),
            IsEliminated = eliminated
        };

        private static TeamStatusElement Live(string ownerId, Entity connection) => new()
        {
            IsOccupied = true,
            OccupyingPlayer = connection,
            OwnerId = new FixedString64Bytes(ownerId)
        };

        private static TeamStatusElement Free => default;

        /// <summary>
        /// "Away" is the one state the slot has to express: still owned, nobody attached.
        /// Anything that treats IsOccupied alone as "someone is playing this corner" is wrong.
        /// </summary>
        [Test]
        public void HeldSlot_IsOccupiedButHasNoConnection()
        {
            var slot = Held("player-a");

            Assert.IsTrue(slot.IsOccupied, "A held corner still belongs to its owner.");
            Assert.AreEqual(Entity.Null, slot.OccupyingPlayer);
        }

        [Test]
        public void HeldSlot_IsNotOfferedToNewPlayers()
        {
            // Mirrors ResolveTeam: it only ever hands out slots where IsOccupied is false.
            var slot = Held("player-a");

            Assert.IsFalse(!slot.IsOccupied, "A corner held for an absent owner must not read as free.");
        }

        [Test]
        public void Reclaim_MatchesTheOwnerAndNobodyElse()
        {
            var slot = Held("player-a");

            Assert.AreEqual(new FixedString64Bytes("player-a"), slot.OwnerId);
            Assert.AreNotEqual(new FixedString64Bytes("player-b"), slot.OwnerId);
        }

        /// <summary>
        /// A client with no identity must be treated as new rather than matching the first held
        /// corner it finds — otherwise one anonymous player inherits another's base.
        /// </summary>
        [Test]
        public void Reclaim_IgnoresAnEmptyPlayerId()
        {
            var anonymous = default(FixedString64Bytes);

            Assert.IsTrue(anonymous.IsEmpty);
            Assert.AreNotEqual(anonymous, Held("player-a").OwnerId);
        }

        [Test]
        public void LiveSlot_IsNotReclaimable()
        {
            var connection = new Entity { Index = 7, Version = 1 };
            var slot = Live("player-a", connection);

            // ResolveReclaim requires OccupyingPlayer == Entity.Null; someone is already sitting here.
            Assert.AreNotEqual(Entity.Null, slot.OccupyingPlayer);
        }

        [Test]
        public void FreeSlot_CarriesNoOwner()
        {
            Assert.IsFalse(Free.IsOccupied);
            Assert.IsTrue(Free.OwnerId.IsEmpty);
        }

        /// <summary>
        /// An eliminated corner is still held, so its owner can come back and watch — the base is
        /// simply never reallocated to them.
        /// </summary>
        [Test]
        public void EliminatedSlot_IsStillHeldForItsOwner()
        {
            var slot = Held("player-a", eliminated: true);

            Assert.IsTrue(slot.IsOccupied);
            Assert.IsTrue(slot.IsEliminated);
            Assert.AreEqual(new FixedString64Bytes("player-a"), slot.OwnerId);
        }
    }
}
