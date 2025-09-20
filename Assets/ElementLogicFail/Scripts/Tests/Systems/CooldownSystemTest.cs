using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Systems.Collision;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    [TestFixture]
    public class CooldownSystemTest : ECSTestFixture
    {
        [Test]
        public void CooldownSystem_ReducesCooldownOverTime()
        {
            var entity = EntityManager.CreateEntity(typeof(ElementData));
            float initialCooldown = 5.0f;
            EntityManager.SetComponentData(entity, new ElementData { Cooldown = initialCooldown });
            
            World.GetOrCreateSystem<CooldownSystem>().Update(World.Unmanaged);
            
            var newCooldown = EntityManager.GetComponentData<ElementData>(entity).Cooldown;
            Assert.Less(newCooldown, initialCooldown);
        }

        [Test]
        public void CooldownSystem_DoesNotGoBelowZero()
        {
            var entity = EntityManager.CreateEntity(typeof(ElementData));
            EntityManager.SetComponentData(entity, new ElementData { Cooldown = 0.1f });

            var system = World.GetOrCreateSystem<CooldownSystem>();
            system.Update(World.Unmanaged);
            system.Update(World.Unmanaged);
            
            var newCooldown = EntityManager.GetComponentData<ElementData>(entity).Cooldown;
            Assert.AreEqual(0f, newCooldown);
        }
    }
}