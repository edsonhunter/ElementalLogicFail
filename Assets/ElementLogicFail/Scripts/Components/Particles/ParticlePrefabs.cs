using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Particles
{
    public struct ParticlePrefabs : IComponentData
    {
        public Entity CreationEffect;
        public Entity ExplosionEffect;
    }
}