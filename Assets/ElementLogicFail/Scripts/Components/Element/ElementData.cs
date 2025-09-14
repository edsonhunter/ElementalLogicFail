using Unity.Entities;

namespace ElementLogicFail.Scripts.Components
{
    public struct ElementData : IComponentData
    {
        public ElementType Type;
    }

    public enum ElementType : byte
    {
        Fire,
        Water,
        Earth,
        Wind
    }
}