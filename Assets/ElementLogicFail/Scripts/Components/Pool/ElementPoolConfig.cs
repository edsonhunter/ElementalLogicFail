using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct ElementPoolConfig : IComponentData
    {
        public int InitialSize;
    }
}