using FourCorners.Scripts.Components.Bounds;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Path;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Spawner
{
    /// <summary>
    /// Drains each spawner's MinionSpawnRequest buffer into real minion entities.
    ///
    /// No [UpdateAfter(SpawnerSystem)]: requests arrive via the EndSimulation ECB, so they are
    /// never visible in the frame they were queued. The one-frame lag is inherent to the ECB,
    /// not something intra-frame ordering can remove.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct MinionSpawningSystem : ISystem
    {
        private NativeParallelHashMap<int, Entity> _modelTypeToPrefab;
        private Random _random;
        private EntityQuery _prefabQuery;
        private int _lastPrefabCount;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WanderArea>();
            // Only run when the match is Active — the lobby gate
            state.RequireForUpdate<MatchState>();

            _modelTypeToPrefab = new NativeParallelHashMap<int, Entity>(16, Allocator.Persistent);
            _random = Random.CreateFromIndex(1234);
            _prefabQuery = state.GetEntityQuery(ComponentType.ReadOnly<MinionPrefabDescriptor>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Global lobby gate: minions must not spawn until the host starts the game.
            var matchState = SystemAPI.GetSingleton<MatchState>();
            if (matchState.Phase != MatchPhase.Active) return;

            var area = SystemAPI.GetSingleton<WanderArea>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            int currentPrefabCount = _prefabQuery.CalculateEntityCount();
            if (currentPrefabCount != _lastPrefabCount)
            {
                _modelTypeToPrefab.Clear();
                var buildMapJob = new BuildPrefabMapJob
                {
                    ModelTypeToPrefab = _modelTypeToPrefab
                };
                buildMapJob.Run();
                _lastPrefabCount = currentPrefabCount;
            }

            var spawnJob = new ProcessSpawningJob
            {
                ModelTypeToPrefab = _modelTypeToPrefab,
                PrefabLookup = SystemAPI.GetComponentLookup<MinionPrefabDescriptor>(true),
                PathLookup = SystemAPI.GetBufferLookup<PathWaypoint>(true),
                PlayerBaseLookup = SystemAPI.GetComponentLookup<PlayerBase>(true),
                MinionLookup = SystemAPI.GetComponentLookup<MinionData>(true),
                Ecb = ecb,
                Area = area,
                Seed = _random.NextUInt()
            };

            state.Dependency = spawnJob.ScheduleParallel(state.Dependency);

            _random.NextUInt();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_modelTypeToPrefab.IsCreated)
            {
                _modelTypeToPrefab.Dispose();
            }
        }
    }

    [BurstCompile]
    public partial struct BuildPrefabMapJob : IJobEntity
    {
        public NativeParallelHashMap<int, Entity> ModelTypeToPrefab;

        private void Execute(Entity entity, RefRO<MinionPrefabDescriptor> prefabDesc)
        {
            if (prefabDesc.ValueRO.ModelType != UnitModelType.None)
            {
                if (!ModelTypeToPrefab.ContainsKey((int)prefabDesc.ValueRO.ModelType))
                {
                    ModelTypeToPrefab.Add((int)prefabDesc.ValueRO.ModelType, entity);
                }
            }
        }
    }

    [BurstCompile]
    public partial struct ProcessSpawningJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashMap<int, Entity> ModelTypeToPrefab;
        [ReadOnly] public ComponentLookup<MinionPrefabDescriptor> PrefabLookup;
        [ReadOnly] public BufferLookup<PathWaypoint> PathLookup;
        [ReadOnly] public ComponentLookup<PlayerBase> PlayerBaseLookup;

        /// <summary>Reads the prefab's baked MinionData so authored stats survive spawning.</summary>
        [ReadOnly] public ComponentLookup<MinionData> MinionLookup;

        public EntityCommandBuffer.ParallelWriter Ecb;
        public WanderArea Area;
        public uint Seed;

        private void Execute(Entity spawnerEntity, [EntityIndexInQuery] int sortKey,
            DynamicBuffer<MinionSpawnRequest> requestBuffer, RefRO<SpawnerData> spawner)
        {
            if (requestBuffer.IsEmpty) return;

            // Resolve team identity once per spawner — not per request.
            if (!PlayerBaseLookup.TryGetComponent(spawner.ValueRO.PlayerBaseEntity, out var owningBase))
            {
                requestBuffer.Clear();
                return;
            }

            var teamNumber = owningBase.TeamNumber;
            var random = Random.CreateFromIndex(Seed + (uint)sortKey);

            for (int i = 0; i < requestBuffer.Length; i++)
            {
                var request = requestBuffer[i];
                // No request.Type guard needed — all requests in this buffer belong to this spawner.

                if (ModelTypeToPrefab.TryGetValue((int)request.ModelType, out var prefabEntity))
                {
                    if (PrefabLookup.TryGetComponent(prefabEntity, out var prefabComponent))
                    {
                        if (prefabComponent.Prefab != Entity.Null)
                        {
                            Entity instance = Ecb.Instantiate(sortKey, prefabComponent.Prefab);

                            if (PathLookup.TryGetBuffer(spawnerEntity, out var spawnerPath))
                            {
                                var instancePath = Ecb.SetBuffer<PathWaypoint>(sortKey, instance);
                                instancePath.AddRange(spawnerPath.AsNativeArray());
                                Ecb.SetComponent(sortKey, instance, new PathFollower { CurrentIndex = 0 });
                            }

                            Ecb.SetComponent(sortKey, instance, LocalTransform.FromPosition(request.Position));

                            // Start from the prefab's baked stats and override only what is
                            // genuinely per-instance. Rebuilding MinionData from scratch here
                            // hardcoded Speed, which made the corresponding MinionAuthoring
                            // inspector field have no runtime effect at all. Combat components
                            // need no such care — Instantiate carries them over untouched.
                            MinionLookup.TryGetComponent(prefabComponent.Prefab, out var minion);
                            minion.TeamNumber = teamNumber;
                            minion.RandomSeed = random.NextUInt();
                            minion.Target = new float3(
                                random.NextFloat(Area.MinArea.x, Area.MaxArea.x),
                                0,
                                random.NextFloat(Area.MinArea.z, Area.MaxArea.z));

                            Ecb.SetComponent(sortKey, instance, minion);
                        }
                    }
                }
            }

            requestBuffer.Clear();
        }
    }
}