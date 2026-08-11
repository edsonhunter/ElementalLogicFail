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
            state.RequireForUpdate<RaceBaseVisual>();
            state.RequireForUpdate<PlayerBase>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            SpawnMissingVisuals(ref state, ecb);
            CleanUpReleasedVisuals(ref state, ecb);
        }

        private void SpawnMissingVisuals(ref SystemState state, EntityCommandBuffer ecb)
        {
            var visuals = SystemAPI.GetSingletonBuffer<RaceBaseVisual>(isReadOnly: true);

            foreach (var (playerBase, baseEntity) in
                     SystemAPI.Query<RefRO<PlayerBase>>()
                         .WithNone<SpawnedBaseVisual>()
                         .WithEntityAccess())
            {
                if (!playerBase.ValueRO.IsActive) continue;

                var visualPrefab = Entity.Null;
                for (int i = 0; i < visuals.Length; i++)
                {
                    if (visuals[i].Race != playerBase.ValueRO.Race) continue;
                    visualPrefab = visuals[i].Prefab;
                    break;
                }

                if (visualPrefab == Entity.Null) continue;

                // Defensive: a bad prefab reference here aborts the ENTIRE EndSimulation ECB
                // playback, silently dropping unrelated systems' commands — which is how a
                // broken visual reference previously stopped minions from receiving their lane.
                if (!state.EntityManager.Exists(visualPrefab))
                {
                    UnityEngine.Debug.LogError(
                        $"[BaseRaceVisualSystem] Race {playerBase.ValueRO.Race} has an invalid base " +
                        "visual prefab entity. Skipping so ECB playback is not aborted.");
                    continue;
                }

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
