using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Pool;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ElementLogicFail.Scripts.Systems.Pool
{
    [BurstCompile]
    public partial struct PoolSystem : ISystem
    {
        private EntityQuery _poolQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _poolQuery = state.GetEntityQuery(ComponentType.ReadOnly<ElementPoolConfig>(),
                ComponentType.ReadOnly<ElementPrefabReference>(), ComponentType.ReadWrite<ElementSpawnRequest>());
            state.RequireForUpdate(_poolQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            Entity poolEntity = _poolQuery.GetSingletonEntity();
            ElementPoolConfig config = state.EntityManager.GetComponentData<ElementPoolConfig>(poolEntity);
            DynamicBuffer<ElementSpawnRequest> spawnBuffer = state.EntityManager.GetBuffer<ElementSpawnRequest>(poolEntity);
            var prefabReference = state.EntityManager.GetComponentData<ElementPrefabReference>(poolEntity);

            foreach (ElementSpawnRequest spawnRequest in spawnBuffer)
            {
                Entity element = GetOrCreate(ref state, ref entityCommandBuffer, prefabReference.Prefab,
                    spawnRequest.Type, spawnRequest.Position);
                entityCommandBuffer.SetComponent(element,
                    LocalTransform.FromPositionRotationScale(spawnRequest.Position, quaternion.identity,
                        1f));
            }
            
            spawnBuffer.Clear();
            entityCommandBuffer.Playback(state.EntityManager);
            entityCommandBuffer.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }

        private Entity GetOrCreate(ref SystemState state, ref EntityCommandBuffer entityCommandBuffer, Entity prefab, ElementType type,
            float3 position)
        {
            var entityManager = state.EntityManager;
            using NativeArray<Entity> candidates = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ElementPoolConfig>(),
                ComponentType.ReadOnly<ElementSpawnRequest>()).ToEntityArray(Allocator.Temp);

            for (int idx = 0; idx < candidates.Length; ++idx)
            {
                Entity candidate = candidates[idx];
                ElementData elementData = entityManager.GetComponentData<ElementData>(candidate);

                if (elementData.Type == type)
                {
                    entityCommandBuffer.RemoveComponent<ElementPoolTag>(candidate);
                    entityCommandBuffer.SetEnabled(candidate, true);
                    return candidate;
                }
            }

            var newElement = entityCommandBuffer.Instantiate(prefab);
            entityCommandBuffer.AddComponent(newElement, new ElementData
            {
                Type = type,
                Speed = 3,
                Target = position,
            });
            return newElement;
        }
    }
}