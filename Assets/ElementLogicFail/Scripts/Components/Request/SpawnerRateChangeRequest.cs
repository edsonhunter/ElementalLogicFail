using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Request
{
    public struct SpawnerRateChangeRequest : IBufferElementData
    {
        public ElementType Type;
        public float NewRate;
    }
}