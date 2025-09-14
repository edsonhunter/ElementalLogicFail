using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
using Unity.Entities;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    public partial struct PoolBootstrapSystem : ISystem, ISystemStartStop
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ElementPrefabReference>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }

        public void OnStartRunning(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            if (entityManager.HasComponent<ElementPoolTag>(SystemAPI.GetSingletonEntity<ElementPrefabReference>()))
            {
                return;
            }
            
            Entity poolEntity = entityManager.CreateEntity();
            Entity registryEntity = SystemAPI.GetSingletonEntity<ElementPrefabReference>();
            ElementPrefabReference registry = entityManager.GetComponentData<ElementPrefabReference>(registryEntity);

            Entity basePrefab = registry.Prefab;
            entityManager.AddComponentData(poolEntity, new ElementPoolTag
            {
                Capacity = 256,
                Count = 0,
                Prefab = basePrefab
            });
            entityManager.AddBuffer<PooledElement>(poolEntity);
            entityManager.AddBuffer<PoolSpawnRequest>(poolEntity);
            entityManager.AddBuffer<PoolReleaseRequest>(poolEntity);

            DynamicBuffer<PooledElement> pooled = entityManager.GetBuffer<PooledElement>(poolEntity);
            int prewarm = 128;
            pooled.EnsureCapacity(prewarm);
            for (int idx = 0; idx < prewarm; idx++)
            {
                Entity instance = entityManager.Instantiate(basePrefab);
                entityManager.AddComponent<Disabled>(instance);
                pooled.Add(new PooledElement { Entity = instance });
            }
            
            ElementPoolTag tag = entityManager.GetComponentData<ElementPoolTag>(registryEntity);
            tag.Capacity = prewarm;
            tag.Count = prewarm;
            entityManager.SetComponentData(poolEntity, tag);
        }

        public void OnStopRunning(ref SystemState state)
        {
        }
    }
}