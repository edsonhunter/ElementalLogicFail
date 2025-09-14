using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Components.Element
{
    public struct WanderArea : IComponentData
    {
        public float3 Min;
        public float3 Max;
    }
}