using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Components.Request
{
    public struct PoolSpawnRequest : IBufferElementData
    {
        public ElementType Type;
        public float3 Position;
    }
}