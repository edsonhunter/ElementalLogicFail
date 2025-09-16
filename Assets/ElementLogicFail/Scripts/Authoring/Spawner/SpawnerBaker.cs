using Unity.Entities;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;

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
                ElementPrefab = prefabEntity,
                Timer = 0f
            });

            var registryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddComponent(registryEntity, new SpawnerRegistry
            {
                Type = authoring.type,
                SpawnerEntity = entity
            });
            AddBuffer<ElementSpawnRequest>(GetEntity(TransformUsageFlags.None));
        }
    }
}