using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Authoring.Pool
{
    public class ElementPoolAuthoringBaker : Baker<ElementPoolAuthoring>
    {
        public override void Bake(ElementPoolAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ElementPoolConfig
            {
                InitialSize = authoring.InitialSize
            });
            AddBuffer<ElementSpawnRequest>(entity);
            var prefabEntity = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);
            AddComponent(prefabEntity, new ElementPrefabReference { Prefab = prefabEntity });
        }
    }
}