using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct ElementPool : IComponentData
    {
        public int ElementType;
        public Entity Prefab;
        public int InitialSize;
    }
}