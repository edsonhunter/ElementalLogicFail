using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Systems.Combat;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class DeathSystemTest : ECSTestFixture
    {
        private void Tick()
        {
            World.GetOrCreateSystem<DeathSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();
        }

        [Test]
        public void DeathSystem_DestroysEntityAtZeroHealth()
        {
            var entity = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10);
            EntityManager.SetComponentData(entity, new Health { Current = 0, Max = 10 });

            Tick();

            Assert.IsFalse(EntityManager.Exists(entity));
        }

        [Test]
        public void DeathSystem_LeavesWoundedSurvivorsAlone()
        {
            var entity = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10);
            EntityManager.SetComponentData(entity, new Health { Current = 1, Max = 10 });

            Tick();

            Assert.IsTrue(EntityManager.Exists(entity));
        }

        /// <summary>
        /// The tag is what keeps DeathSystem off player bases once Tier 0.2 gives them health —
        /// a destroyed corner has to survive as a deactivated ghost, not vanish.
        /// </summary>
        [Test]
        public void DeathSystem_IgnoresEntitiesWithoutDestroyOnDeath()
        {
            var entity = EntityTest.CreateCombatant(EntityManager, float3.zero, health: 10);
            EntityManager.RemoveComponent<DestroyOnDeath>(entity);
            EntityManager.SetComponentData(entity, new Health { Current = 0, Max = 10 });

            // Something else in the world must still carry the tag, or the system declines to run
            // at all and the test would pass for the wrong reason.
            EntityTest.CreateCombatant(EntityManager, new float3(10f, 0f, 0f), health: 10);

            Tick();

            Assert.IsTrue(EntityManager.Exists(entity));
        }
    }
}
