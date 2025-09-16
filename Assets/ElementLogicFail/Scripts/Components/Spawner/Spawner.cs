using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Spawner
{
    public struct Spawner : IComponentData
    {
        public ElementType Type;
        public Entity ElementPrefab;
        public float SpawnRate;
        public float Timer;
    }
}