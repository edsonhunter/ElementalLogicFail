using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Particles
{
    public struct ParticlePrefabs : IComponentData
    {
        public Entity ParticlePrefab;
        public int PoolSize;
    }
}