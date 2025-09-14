using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Spawner
{
    public class SpawnerAuthoring : MonoBehaviour
    {
        public ElementType type;
        public GameObject prefab;
        public float spawnRate;
    }
    
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

            AddBuffer<ElementSpawnRequest>(GetEntity(TransformUsageFlags.None));
        }
    }
}