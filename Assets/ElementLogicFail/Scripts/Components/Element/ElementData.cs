using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Components.Element
{
    public struct ElementData : IComponentData
    {
        public ElementType Type;
        public float Speed;
        public float3 Target;
        public uint RandomSeed;
    }

    public enum ElementType : byte
    {
        Fire,
        Water,
        Earth,
        Wind
    }
}