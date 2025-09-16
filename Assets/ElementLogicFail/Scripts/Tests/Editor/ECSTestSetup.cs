using NUnit.Framework;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Tests.Editor
{
    public class ECSTestSetup
    {
        protected World World;
        protected EntityManager EntityManager;

        [SetUp]
        public virtual void Setup()
        {
            World = new World("ECSTestSetup");
            EntityManager = World.EntityManager;
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