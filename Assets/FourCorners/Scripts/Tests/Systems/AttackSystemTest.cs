using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Systems.Combat;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class AttackSystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<AttackSystem>().Update(World.Unmanaged);

            // AttackSystem writes health from a job rather than through an ECB, so nothing has
            // landed until the chain is drained.
            EntityManager.CompleteAllTrackedJobs();
        }

        private int HealthOf(Entity entity) => EntityManager.GetComponentData<Health>(entity).Current;

        [Test]
        public void AttackSystem_DamagesEngagedTarget()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, damage: 3);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.AreEqual(7, HealthOf(target));
        }

        [Test]
        public void AttackSystem_ReloadsAfterAttacking()
        {
            var attacker = EntityTest.CreateCombatant(
                EntityManager, float3.zero, health: 10, damage: 3, interval: 5f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();
            Tick();

            // The second tick must find the attacker reloading, not swinging again.
            Assert.AreEqual(7, HealthOf(target));
            Assert.AreEqual(5f, EntityManager.GetComponentData<AttackCooldown>(attacker).Remaining);
        }

        [Test]
        public void AttackSystem_DoesNotDamageTargetOutOfRange()
        {
            var attacker = EntityTest.CreateCombatant(
                EntityManager, float3.zero, health: 10, damage: 3, range: 2f);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(50f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.AreEqual(10, HealthOf(target));
        }

        /// <summary>
        /// Engagement is removed through an end-of-frame ECB, so an attacker keeps pointing at a
        /// target that died earlier in the same frame. The guard, not system ordering, is what
        /// stops it swinging at a corpse.
        /// </summary>
        [Test]
        public void AttackSystem_DoesNotDamageAlreadyDeadTarget()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, damage: 3);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityManager.SetComponentData(target, new Health { Current = 0, Max = 10 });
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.AreEqual(0, HealthOf(target));

            // The wasted swing must not have cost a reload either.
            Assert.AreEqual(0f, EntityManager.GetComponentData<AttackCooldown>(attacker).Remaining);
        }

        /// <summary>
        /// Two attackers on one target is the case the parallel-report / single-apply split
        /// exists for: both hits must land, not race and lose one.
        /// </summary>
        [Test]
        public void AttackSystem_AppliesEveryAttackersDamageToASharedTarget()
        {
            var target = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10);

            var first = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10, damage: 3);
            var second = EntityTest.CreateCombatant(EntityManager, new float3(-1f, 0f, 0f), health: 10, damage: 4);
            EntityTest.Engage(EntityManager, first, target);
            EntityTest.Engage(EntityManager, second, target);

            Tick();

            Assert.AreEqual(3, HealthOf(target));
        }

        /// <summary>
        /// Two evenly matched minions used to annihilate each other every single time: attacks are
        /// all decided before any are applied, so both swung on the frame either of them died, and
        /// "whoever survives continues the walk" never once happened.
        /// </summary>
        [Test]
        public void AttackSystem_LeavesASurvivorWhenBothBlowsWouldBeLethal()
        {
            var first = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 3, damage: 3);
            var second = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 3, damage: 3);
            EntityTest.Engage(EntityManager, first, second);
            EntityTest.Engage(EntityManager, second, first);

            Tick();

            int firstHealth = HealthOf(first);
            int secondHealth = HealthOf(second);

            Assert.AreNotEqual(0, firstHealth + secondHealth,
                "Both combatants died — the duel produced no survivor.");
            Assert.IsTrue(firstHealth == 0 || secondHealth == 0,
                "Exactly one of them should have fallen.");
        }

        [Test]
        public void AttackSystem_ClampsOverkillAtZero()
        {
            var attacker = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10, damage: 999);
            var target = EntityTest.CreateCombatant(EntityManager, new float3(1f, 0f, 0f), health: 10);
            EntityTest.Engage(EntityManager, attacker, target);

            Tick();

            Assert.AreEqual(0, HealthOf(target));
        }
    }
}
