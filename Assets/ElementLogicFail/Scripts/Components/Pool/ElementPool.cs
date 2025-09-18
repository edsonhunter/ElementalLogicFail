using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct ElementPool : IComponentData
    {
        public Entity Prefab;
        public int InitialSize;
    }
}