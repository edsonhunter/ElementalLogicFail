using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Systems.Collision;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class CooldownSystemTest : ECSTestFixture
    {
        private const float Step = 0.1f;

        private void Tick()
        {
            AdvanceTime(Step);
            World.GetOrCreateSystem<CooldownSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
        }

        [Test]
        public void CooldownSystem_ReducesCooldownOverTime()
        {
            var entity = EntityManager.CreateEntity(typeof(AttackCooldown));
            const float initialCooldown = 5.0f;
            EntityManager.SetComponentData(entity, new AttackCooldown { Remaining = initialCooldown });

            Tick();

            var remaining = EntityManager.GetComponentData<AttackCooldown>(entity).Remaining;
            Assert.Less(remaining, initialCooldown);
        }

        [Test]
        public void CooldownSystem_DoesNotGoBelowZero()
        {
            var entity = EntityManager.CreateEntity(typeof(AttackCooldown));
            EntityManager.SetComponentData(entity, new AttackCooldown { Remaining = Step });

            Tick();
            Tick();

            var remaining = EntityManager.GetComponentData<AttackCooldown>(entity).Remaining;
            Assert.AreEqual(0f, remaining);
        }
    }
}
