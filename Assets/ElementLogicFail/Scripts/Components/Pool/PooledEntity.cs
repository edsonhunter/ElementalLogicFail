using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Pool
{
    public struct PooledEntity : IBufferElementData 
    {
        public Entity Value;
    }
}