using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using ElementLogicFail.Scripts.Components.Spawner;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ElementLogicFail.Scripts.Systems.Collision
{
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    public partial struct CollisionSystem : ISystem
    {
        private ComponentLookup<ElementData> _elementLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<SpawnerRegistry> _spawnerRegistryLookup;
        
        private NativeParallelHashMap<int, Entity> _typeToSpawnerMap;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SimulationSingleton>();
            
            _elementLookup = SystemAPI.GetComponentLookup<ElementData>(true);
            _localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            _spawnerRegistryLookup = SystemAPI.GetComponentLookup<SpawnerRegistry>(true);
            
            _typeToSpawnerMap = new NativeParallelHashMap<int, Entity>(16, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _elementLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _spawnerRegistryLookup.Update(ref state);
            
            _typeToSpawnerMap.Clear();
            foreach (var (registry, entity) in SystemAPI.Query<RefRO<SpawnerRegistry>>().WithEntityAccess())
            {
                _typeToSpawnerMap[(int)registry.ValueRO.Type] = registry.ValueRO.SpawnerEntity;
            }
            
            SimulationSingleton simulation = SystemAPI.GetSingleton<SimulationSingleton>();
            EndSimulationEntityCommandBufferSystem.Singleton endSimulationEntityCommandBufferSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer.ParallelWriter parallelWriter = endSimulationEntityCommandBufferSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var job = new CollisionEventJob
            {
                ElementLookup = _elementLookup,
                LocalTransformLookup = _localTransformLookup,
                TypeToSpawnerMap = _typeToSpawnerMap,
                EntityCommandBuffer = parallelWriter,
            };
            
            state.Dependency = job.Schedule(simulation, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_typeToSpawnerMap.IsCreated)
                _typeToSpawnerMap.Dispose();
        }
    }
    
    public struct CollisionEventJob : ICollisionEventsJob
    {
        [ReadOnly] public ComponentLookup<ElementData> ElementLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public NativeParallelHashMap<int, Entity>  TypeToSpawnerMap;
        
        public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

        public void Execute(CollisionEvent collisionEvent)
        {
            Entity a = collisionEvent.EntityA;
            Entity b = collisionEvent.EntityB;

            if (!ElementLookup.HasComponent(a) || !ElementLookup.HasComponent(b))
            {
                return;
            }
            
            var dataA = ElementLookup[a];
            var dataB = ElementLookup[b];
            if (dataA.Cooldown > 0f || dataB.Cooldown > 0f)
            {
                return;
            }
            
            float3 position = 0.5f * (LocalTransformLookup[a].Position + LocalTransformLookup[b].Position);

            if (dataA.Type == dataB.Type)
            {
                if (TypeToSpawnerMap.TryGetValue((int)dataA.Type, out var spawnerEntity))
                {
                    EntityCommandBuffer.SetComponent(0, a, new ElementData
                    {
                        Type = dataA.Type,
                        Speed = dataA.Speed,
                        RandomSeed = dataA.RandomSeed,
                        Target = dataA.Target,
                        Cooldown = 10f,
                    });
                    EntityCommandBuffer.SetComponent(0, b, new ElementData
                    {
                        Type = dataB.Type,
                        Speed = dataB.Speed,
                        RandomSeed = dataB.RandomSeed,
                        Target = dataB.Target,
                        Cooldown = 2f,
                    });

                    EntityCommandBuffer.AppendToBuffer(0, spawnerEntity, new ElementSpawnRequest
                    {
                        Type = dataA.Type,
                        Position = position
                    });
                }
            }
            else
            {
                EntityCommandBuffer.DestroyEntity(0, a);
                EntityCommandBuffer.DestroyEntity(0, b);
            }
        }
    }
}