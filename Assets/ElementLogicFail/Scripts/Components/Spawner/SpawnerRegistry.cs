using ElementLogicFail.Scripts.Components.Element;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Spawner
{
    public struct SpawnerRegistry : IComponentData
    {
        public ElementType Type;
        public Entity SpawnerEntity;
    }
}