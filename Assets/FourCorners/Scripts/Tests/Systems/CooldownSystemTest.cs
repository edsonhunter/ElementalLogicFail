using FourCorners.Scripts.Components.Minion;
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

        [Test]
        public void CooldownSystem_ReducesCooldownOverTime()
        {
            var entity = EntityManager.CreateEntity(typeof(MinionData));
            const float initialCooldown = 5.0f;
            EntityManager.SetComponentData(entity, new MinionData { Cooldown = initialCooldown });

            AdvanceTime(Step);
            World.GetOrCreateSystem<CooldownSystem>().Update(World.Unmanaged);

            var newCooldown = EntityManager.GetComponentData<MinionData>(entity).Cooldown;
            Assert.Less(newCooldown, initialCooldown);
        }

        [Test]
        public void CooldownSystem_DoesNotGoBelowZero()
        {
            var entity = EntityManager.CreateEntity(typeof(MinionData));
            EntityManager.SetComponentData(entity, new MinionData { Cooldown = Step });

            var system = World.GetOrCreateSystem<CooldownSystem>();

            AdvanceTime(Step);
            system.Update(World.Unmanaged);
            AdvanceTime(Step);
            system.Update(World.Unmanaged);

            var newCooldown = EntityManager.GetComponentData<MinionData>(entity).Cooldown;
            Assert.AreEqual(0f, newCooldown);
        }
    }
}
