using ElementLogicFail.Scripts.Components.Particles;
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
                CreationEffect =  GetEntity(authoring.creationEffect, TransformUsageFlags.Dynamic),
                ExplosionEffect = GetEntity(authoring.explosionEffect, TransformUsageFlags.Dynamic)
            });
        }
    }
}