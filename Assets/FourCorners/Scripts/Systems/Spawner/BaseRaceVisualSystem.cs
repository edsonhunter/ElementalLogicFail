using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Spawner
{
    /// <summary>
    /// Tracks which race's visuals have been spawned under a claimed base, so the system can
    /// tell "already correct" from "needs rebuilding" without rescanning children.
    /// </summary>
    public struct SpawnedBaseVisual : ICleanupComponentData
    {
        public RaceType Race;
        public Entity VisualInstance;
    }

    /// <summary>
    /// Client-side: instantiates the race visuals for each claimed corner, driven by the
    /// replicated PlayerBase.Race.
    ///
    /// No-op unless a RaceCatalog is authored in the subscene, in which case bases keep whatever
    /// visuals their own prefab carries. This is what lets race be chosen at runtime instead of
    /// being fixed by which corner prefab sits in that spot.
    ///
    /// Not [BurstCompile]d: instantiate-and-parent through an ECB with a cleanup component is
    /// structural work that runs a handful of times per match.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct BaseRaceVisualSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RaceCatalog>();
            state.RequireForUpdate<PlayerBase>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalog = SystemAPI.GetSingleton<RaceCatalog>();
            if (!catalog.Value.IsCreated) return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            SpawnMissingVisuals(ref state, catalog, ecb);
            CleanUpReleasedVisuals(ref state, ecb);
        }

        private void SpawnMissingVisuals(ref SystemState state, RaceCatalog catalog, EntityCommandBuffer ecb)
        {
            foreach (var (playerBase, baseEntity) in
                     SystemAPI.Query<RefRO<PlayerBase>>()
                         .WithNone<SpawnedBaseVisual>()
                         .WithEntityAccess())
            {
                if (!playerBase.ValueRO.IsActive) continue;

                ref var blob = ref catalog.Value.Value;
                int index = blob.IndexOf(playerBase.ValueRO.Race);
                if (index < 0) continue;

                // By ref: RaceDefinition holds a BlobArray and may not be copied out of blob
                // storage. Only the prefab Entity is read out here, which is safe to copy.
                var visualPrefab = blob.Races[index].BaseVisualPrefab;
                if (visualPrefab == Entity.Null) continue;

                var instance = ecb.Instantiate(visualPrefab);
                ecb.AddComponent(instance, new Parent { Value = baseEntity });
                ecb.AddComponent(instance, LocalTransform.Identity);

                ecb.AddComponent(baseEntity, new SpawnedBaseVisual
                {
                    Race = playerBase.ValueRO.Race,
                    VisualInstance = instance
                });
            }
        }

        /// <summary>
        /// Tears the visuals down when the corner is released (a player disconnected) or when
        /// the same corner is reclaimed by a different race.
        /// </summary>
        private void CleanUpReleasedVisuals(ref SystemState state, EntityCommandBuffer ecb)
        {
            var baseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: true);

            foreach (var (visual, baseEntity) in
                     SystemAPI.Query<RefRO<SpawnedBaseVisual>>().WithEntityAccess())
            {
                bool stillOwned =
                    baseLookup.TryGetComponent(baseEntity, out var playerBase) &&
                    playerBase.IsActive &&
                    playerBase.Race == visual.ValueRO.Race;

                if (stillOwned) continue;

                if (visual.ValueRO.VisualInstance != Entity.Null)
                    ecb.DestroyEntity(visual.ValueRO.VisualInstance);

                ecb.RemoveComponent<SpawnedBaseVisual>(baseEntity);
            }
        }
    }
}
