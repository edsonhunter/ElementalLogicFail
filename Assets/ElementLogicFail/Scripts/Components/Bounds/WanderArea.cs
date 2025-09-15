using Unity.Entities;
using Unity.Mathematics;

namespace ElementLogicFail.Scripts.Components.Bounds
{
    public struct WanderArea : IComponentData
    {
        public float3 MinArea;
        public float3 MaxArea;
    }
}