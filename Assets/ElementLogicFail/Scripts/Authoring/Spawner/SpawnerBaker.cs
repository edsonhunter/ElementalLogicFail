using Unity.Entities;
using ElementLogicFail.Scripts.Components.Request;

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

            AddBuffer<ElementSpawnRequest>(GetEntity(TransformUsageFlags.None));
        }
    }
}