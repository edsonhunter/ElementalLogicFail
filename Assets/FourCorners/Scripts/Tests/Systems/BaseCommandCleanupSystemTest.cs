using System.Text.RegularExpressions;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Command;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace FourCorners.Scripts.Tests.Systems
{
    /// <summary>
    /// Pins the one-frame lifetime that every command handler is written against.
    ///
    /// Handlers get exactly one look at an intent, which is what lets them be written as ordinary
    /// stateless queries with no bookkeeping about what they have already seen. If this ever stops
    /// destroying intents, a single upgrade press starts applying every frame forever.
    /// </summary>
    [TestFixture]
    public class BaseCommandCleanupSystemTest : ECSTestFixture
    {
        private Entity CreateIntent(bool handled)
        {
            var entity = EntityManager.CreateEntity(typeof(BaseCommand));
            EntityManager.SetComponentData(entity, new BaseCommand
            {
                Type = BaseCommandType.UpgradeCentral,
                Team = TeamNumber.Team1,
                Handled = handled
            });
            return entity;
        }

        private void Tick()
        {
            World.GetOrCreateSystem<BaseCommandCleanupSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void BaseCommandCleanup_DiscardsAHandledIntent()
        {
            var intent = CreateIntent(handled: true);

            Tick();

            Assert.IsFalse(EntityManager.Exists(intent));
        }

        /// <summary>
        /// The diagnostic is the point of this system as much as the cleanup: a command type with
        /// no handler is otherwise completely silent, and the whole of Tier 1 is going to be spent
        /// adding handlers one at a time.
        /// </summary>
        [Test]
        public void BaseCommandCleanup_ReportsAnIntentNothingActedOn()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no handler"));

            var intent = CreateIntent(handled: false);

            Tick();

            Assert.IsFalse(EntityManager.Exists(intent));
        }
    }
}
