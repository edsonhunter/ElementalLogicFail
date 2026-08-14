using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Systems.Combat;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class EngagementSystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<EngagementSystem>().Update(World.Unmanaged);

            // Engagement is removed structurally, so nothing is visible until the ECB plays back.
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void EngagementSystem_KeepsEngagementWhileTargetIsAliveAndClose()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.IsTrue(EntityManager.HasComponent<Engagement>(attacker));
        }

        [Test]
        public void EngagementSystem_ReleasesWhenTargetDies()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            EntityManager.SetComponentData(target, new Health { Current = 0, Max = 10 });
            Tick();

            Assert.IsFalse(EntityManager.HasComponent<Engagement>(attacker));
        }

        [Test]
        public void EngagementSystem_ReleasesWhenTargetIsDestroyed()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            EntityManager.DestroyEntity(target);
            Tick();

            Assert.IsFalse(EntityManager.HasComponent<Engagement>(attacker));
        }

        /// <summary>
        /// Beyond attack range but inside the break threshold, the fight has to hold — otherwise
        /// a contact impulse nudging the pair apart would end it.
        /// </summary>
        [Test]
        public void EngagementSystem_HoldsThroughSmallSeparation()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(3f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.IsTrue(EntityManager.HasComponent<Engagement>(attacker));
        }

        [Test]
        public void EngagementSystem_ReleasesWhenTargetIsFarBeyondBreakRange()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(50f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.IsFalse(EntityManager.HasComponent<Engagement>(attacker));
        }
    }
}
