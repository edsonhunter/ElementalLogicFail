using Unity.Entities;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Authoring.Spawner
{
    public class SpawnerBaker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var prefabEntity = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new Components.Spawner.Spawner
            {
                Type = authoring.type,
                SpawnRate = authoring.spawnRate,
                ElementPrefab = prefabEntity
            });
            
            AddComponent(entity, LocalTransform.Identity);
            
            AddComponent(GetEntity(TransformUsageFlags.None), new SpawnControl
            {
                SpawnRateMultiplier = 1f
            });

            AddBuffer<ElementSpawnRequest>(GetEntity(TransformUsageFlags.None));
        }
    }
}