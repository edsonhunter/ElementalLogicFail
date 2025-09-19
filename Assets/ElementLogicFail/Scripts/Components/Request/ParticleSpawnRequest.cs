using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Components.Request
{
    public struct ParticleSpawnRequest : IBufferElementData
    {
        public Entity Prefab;
        public float3 Position;
        public int Count;
    }
}