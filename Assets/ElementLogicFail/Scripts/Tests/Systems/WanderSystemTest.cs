using ElementLogicFail.Scripts.Components.Bounds;
using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Systems.Wander;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    [TestFixture]
    public class WanderSystemTest : ECSTestFixture
    {
        [Test]
        public void WanderSystem_MovesEntityInsideBounds()
        {
            var entity = EntityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(ElementData));
            
            EntityManager.CreateSingleton(new WanderArea
            {
                MinArea = new float3(-10, 0, -10),
                MaxArea = new float3(10, 0, 10)
            });
            EntityManager.SetComponentData(entity, EntityTest.CreateElementData(ElementType.Wind, 5f,  1));

            var system = World.CreateSystem<WanderSystem>();
            system.Update(World.Unmanaged);
            World.EntityManager.CompleteAllTrackedJobs();

            var position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
            Assert.IsTrue(position.x >= -10 && position.x <= 10);
            Assert.IsTrue(position.z >= -10 && position.z <= 10);
        }
    }
}