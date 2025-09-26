using ElementLogicFail.Scripts.Components.Particles;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Authoring.Particles
{
    public class ParticleBaker : Baker<ParticleAuthoring>
    {
        public override void Bake(ParticleAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ParticlePrefabs
            {
                ParticlePrefab =  GetEntity(authoring.particlePrefab, TransformUsageFlags.Dynamic),
                PoolSize = authoring.poolSize
            });
            
            AddBuffer<ParticleSpawnRequest>(entity);
        }
    }
}