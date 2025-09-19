using ElementLogicFail.Scripts.Components.Element;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Spawner
{
    public class SpawnerAuthoring : MonoBehaviour
    {
        public ElementType type;
        public GameObject prefab;
        public float spawnRate;
        public int initialPoolSize;
    }
}