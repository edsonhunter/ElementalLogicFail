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
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            EntityManager.CreateSingleton(new WanderArea
            {
                MinArea = new float3(-10, 0, -10),
                MaxArea = new float3(10, 0, 10)
            });
        }
        
        [Test]
        public void WanderSystem_MovesEntityTowardsTarget()
        {
            var entity = EntityManager.CreateEntity(typeof(LocalTransform), typeof(ElementData));
            var initialPosition = new float3(0, 0, 0);
            var targetPosition = new float3(5, 0, 5);
            
            EntityManager.SetComponentData(entity, new LocalTransform { Position = initialPosition, Scale = 1 });
            var elementData = EntityTest.CreateElementData(ElementType.Wind, 5f,  0);
            elementData.Target = targetPosition;
            EntityManager.SetComponentData(entity, elementData);

            World.GetOrCreateSystem<WanderSystem>().Update(World.Unmanaged);

            var position = EntityManager.GetComponentData<LocalTransform>(entity).Position;
            Assert.IsTrue(position.x >= -10 && position.x <= 10);
            Assert.IsTrue(position.z >= -10 && position.z <= 10);
        }

        [Test]
        public void WanderSystem_WhenTargetReached_AssignsNewTarget()
        {
            var entity = EntityManager.CreateEntity(typeof(LocalTransform), typeof(ElementData));
            var initialTarget = new float3(5, 0, 5);
            
            EntityManager.SetComponentData(entity, new LocalTransform { Position = initialTarget, Scale = 1 });
            var elementData = EntityTest.CreateElementData(ElementType.Wind, 5f,  0);
            elementData.Target = initialTarget;
            EntityManager.SetComponentData(entity, elementData);

            World.GetOrCreateSystem<WanderSystem>().Update(World.Unmanaged);
            
            var newTarget = EntityManager.GetComponentData<ElementData>(entity).Target;
            Assert.IsFalse(newTarget.Equals(initialTarget));
        }
    }
}