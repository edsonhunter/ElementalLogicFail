using ElementLogicFail.Scripts.Components.Particles;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Systems.Particles;
using ElementLogicFail.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Tests.Systems
{
    [TestFixture]
    public class ParticleSystemTest : ECSTestFixture
    {
        [Test]
        public void ParticleSystem_WhenRequestExists_SpawnsParticle()
        {
            var particlePrefab = EntityManager.CreateEntity();
            var particleManager = EntityManager.CreateEntity(typeof(ParticlePrefabs));
            var requestBuffer = EntityManager.AddBuffer<ParticleSpawnRequest>(particleManager);
            requestBuffer.Add(new ParticleSpawnRequest
            {
                Prefab = particlePrefab,
                Position = new float3(5, 0, 5),
            });
            
            World.GetOrCreateSystem<ParticleSystem>().Update(World.Unmanaged);
            World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().Update();

            var query = EntityManager.CreateEntityQuery(typeof(ParticleEffectData));
            Assert.AreEqual(1, query.CalculateEntityCount());
        }
        
        [Test]
        public void ParticleSystem_ClearsRequestBufferAfterProcessing()
        {
            var particlePrefab = EntityManager.CreateEntity();
            var particleManager = EntityManager.CreateEntity(typeof(ParticlePrefabs));
            var requestBuffer = EntityManager.AddBuffer<ParticleSpawnRequest>(particleManager);
            requestBuffer.Add(new ParticleSpawnRequest { Prefab = particlePrefab });
            
            World.GetOrCreateSystem<ParticleSystem>().Update(World.Unmanaged);
            
            Assert.AreEqual(0, requestBuffer.Length);
        }
    }
}