using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Request
{
    public struct PoolReleaseRequest : IBufferElementData
    {
        public Entity ReleaseEntity;
    }
}