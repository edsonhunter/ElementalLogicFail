using NUnit.Framework;
using Unity.Core;
using Unity.Entities;

namespace FourCorners.Scripts.Tests.Editor
{
    public class ECSTestFixture
    {
        protected World World;
        protected EntityManager EntityManager;

        [SetUp]
        public virtual void Setup()
        {
            World = new World("ECSTestSetup");
            var entitySimulationCommandBufferSystem =
                World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            EntityManager = entitySimulationCommandBufferSystem.EntityManager;
        }

        /// <summary>
        /// Advances the test world's clock.
        ///
        /// A bare World reports DeltaTime = 0, so any system whose behaviour depends on elapsed
        /// time does nothing at all under test. Tests that assert on time-based change must call
        /// this or they assert against a system that cannot have moved.
        /// </summary>
        protected void AdvanceTime(float deltaTime)
        {
            World.SetTime(new TimeData(World.Time.ElapsedTime + deltaTime, deltaTime));
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                World.Dispose();
            }
        }
    }
}
