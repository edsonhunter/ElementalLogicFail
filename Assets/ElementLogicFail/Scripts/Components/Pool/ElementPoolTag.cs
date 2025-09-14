using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct ElementPoolTag : IComponentData
    {
        public int Capacity;
        public int Count;
        public Entity Prefab;
    }
}