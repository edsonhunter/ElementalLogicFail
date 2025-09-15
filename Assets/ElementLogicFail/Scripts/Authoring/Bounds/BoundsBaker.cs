using ElementLogicFail.Scripts.Components.Bounds;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Authoring.Bounds
{
    public class BoundsBaker : Baker<BoundsAuthoring>
    {
        public override void Bake(BoundsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new WanderArea
            {
                MinArea = authoring.min,
                MaxArea = authoring.max
            });
        }
    }
}