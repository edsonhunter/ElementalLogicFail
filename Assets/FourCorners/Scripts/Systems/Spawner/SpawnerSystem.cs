using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Building;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Spawner
{
    /// <summary>
    /// Ticks each active spawner's wave timer and queues MinionSpawnRequest entries.
    ///
    /// No [UpdateBefore(MinionSpawningSystem)]: requests are written through the
    /// EndSimulation ECB, so they cannot be visible to the consumer until the next frame
    /// regardless of intra-frame ordering. The attribute protected nothing.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<MatchState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Same lobby gate as MinionSpawningSystem. Without it this kept appending to an
            // uncapped DynamicBuffer<MinionSpawnRequest> before the match began, which then
            // discharged as one enormous wave the instant spawning was unblocked.
            if (SystemAPI.GetSingleton<MatchState>().Phase != MatchPhase.Active) return;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Read-only lookup: spawners check their owning base's IsActive flag.
            // This keeps TeamNumber exclusively on PlayerBase — no duplication in SpawnerData.
            var playerBaseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: true);

            // Optional. Without a RaceCatalog authored in the subscene, spawners fall back to
            // their baked SpawnerPrefab buffer — i.e. the original per-corner roster.
            SystemAPI.TryGetSingleton<RaceCatalog>(out var raceCatalog);

            var job = new SpawnerJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = ecb,
                Seed = (uint)(SystemAPI.Time.ElapsedTime * 1000f) + 1,
                PlayerBaseLookup = playerBaseLookup,
                BuildingLookup = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true),
                RaceCatalog = raceCatalog.Value
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct SpawnerJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;
        public uint Seed;

        [ReadOnly] public ComponentLookup<PlayerBase> PlayerBaseLookup;

        /// <summary>
        /// This spawner's barracks level. A spawner with no BuildingData behaves as level zero
        /// rather than refusing to spawn — a missing building should cost an upgrade, not a lane.
        /// </summary>
        [ReadOnly] public ComponentLookup<BuildingData> BuildingLookup;

        /// <summary>Optional. Default (uncreated) means "fall back to the baked roster".</summary>
        [ReadOnly] public BlobAssetReference<RaceCatalogBlob> RaceCatalog;

        private void Execute(
            Entity entity,
            [EntityIndexInQuery] int sortKey,
            ref SpawnerData spawner,
            RefRO<LocalToWorld> worldTransform,
            DynamicBuffer<SpawnerPrefab> prefabs)
        {
            // Tested against the authored values, so a spawner deliberately authored as disabled
            // stays disabled no matter what level it reaches.
            if (spawner.SpawnInterval <= 0f || spawner.SpawnAmount <= 0) return;

            // Authority check: read IsActive from the owning PlayerBase.
            if (!PlayerBaseLookup.TryGetComponent(spawner.PlayerBaseEntity, out var owningBase) || !owningBase.IsActive)
                return;

            // Derived per tick from the barracks level; SpawnerData keeps the authored baseline
            // untouched, so the inspector values still mean what they say and resetting a corner
            // for a new occupant is one assignment rather than a restore from a saved copy.
            int level = BuildingLookup.TryGetComponent(entity, out var barracks) ? barracks.Level : 0;
            float interval = BuildingUpgrade.EffectiveSpawnInterval(spawner.SpawnInterval, level);
            int amount = BuildingUpgrade.EffectiveSpawnAmount(spawner.SpawnAmount, level);

            spawner.Timer += DeltaTime;
            if (spawner.Timer < interval) return;

            spawner.Timer -= interval;

            var random = Unity.Mathematics.Random.CreateFromIndex(Seed ^ (uint)sortKey);

            for (int i = 0; i < amount; i++)
            {
                if (!TryPickModel(owningBase.Race, prefabs, ref random, out var selectedType)) return;

                Ecb.AppendToBuffer(sortKey, entity, new MinionSpawnRequest
                {
                    ModelType = selectedType,
                    Position = worldTransform.ValueRO.Position
                });
            }
        }

        /// <summary>
        /// Picks from the occupying player's race roster when a catalog is authored, otherwise
        /// from this spawner's baked prefab buffer. The fallback keeps the game working
        /// unchanged before the race catalog has been set up in the subscene.
        /// </summary>
        private bool TryPickModel(
            RaceType race,
            in DynamicBuffer<SpawnerPrefab> prefabs,
            ref Unity.Mathematics.Random random,
            out UnitModelType model)
        {
            if (RaceCatalog.IsCreated)
            {
                ref var catalog = ref RaceCatalog.Value;
                int index = catalog.IndexOf(race);
                if (index >= 0)
                {
                    // By ref: RaceDefinition holds a BlobArray and may not be copied out of
                    // blob storage.
                    ref var definition = ref catalog.Races[index];
                    if (definition.Roster.Length > 0)
                    {
                        model = definition.Roster[random.NextInt(0, definition.Roster.Length)];
                        return true;
                    }
                }
            }

            if (prefabs.Length > 0)
            {
                model = prefabs[random.NextInt(0, prefabs.Length)].ModelType;
                return true;
            }

            model = default;
            return false;
        }
    }
}