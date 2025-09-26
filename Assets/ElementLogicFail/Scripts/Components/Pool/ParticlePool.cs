using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct ParticlePool : IComponentData
    {
        public Entity Prefab;
        public int PoolSize;
    }
}