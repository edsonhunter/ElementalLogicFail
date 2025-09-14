using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Authoring.Element
{
    public class ElementBaker : Baker<ElementAuthoring>
    {
        public override void Bake(ElementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ElementTag>(entity);
            AddComponent(entity, new ElementData
            {
                Type = authoring.Type
            });
            AddComponent(entity, LocalTransform.Identity);
        }
    }
}