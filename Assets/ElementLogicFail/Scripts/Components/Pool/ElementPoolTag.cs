using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Element
{
    public struct ElementPoolTag : IComponentData
    {
        public int Capacity;
        public int Count;
        public Entity Prefab;
    }
}