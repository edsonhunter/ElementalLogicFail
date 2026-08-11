using System.Collections.Generic;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace FourCorners.Scripts.Authoring
{
    /// <summary>
    /// Bakes the race catalog. Place ONE of these in the gameplay subscene.
    ///
    /// Until this exists, spawners use their baked SpawnerPrefab buffer and bases keep their
    /// authored visuals — the game behaves exactly as before. Adding it switches race from
    /// "a property of the corner" to "a runtime choice".
    /// </summary>
    public class RaceCatalogAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class RaceEntry
        {
            public RaceType race;

            [Tooltip("Base visuals for this race. Instantiated under the claimed corner on clients.")]
            public GameObject baseVisualPrefab;

            [Tooltip("Unit models this race's spawners emit. Must not be empty.")]
            public List<UnitModelType> roster = new();
        }

        public List<RaceEntry> races = new();

        public class RaceCatalogBaker : Baker<RaceCatalogAuthoring>
        {
            public override void Bake(RaceCatalogAuthoring authoring)
            {
                if (authoring.races == null || authoring.races.Count == 0)
                {
                    Debug.LogError(
                        "[RaceCatalogAuthoring] No races authored. Remove this component or fill it in — " +
                        "an empty catalog silently disables every spawner.", authoring);
                    return;
                }

                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<RaceCatalogBlob>();
                var raceArray = builder.Allocate(ref root.Races, authoring.races.Count);

                for (int i = 0; i < authoring.races.Count; i++)
                {
                    var entry = authoring.races[i];

                    if (entry.roster == null || entry.roster.Count == 0)
                    {
                        Debug.LogError(
                            $"[RaceCatalogAuthoring] Race '{entry.race}' has an empty roster; its spawners " +
                            "will never produce anything.", authoring);
                    }

                    raceArray[i].Race = entry.race;

                    int rosterCount = entry.roster?.Count ?? 0;
                    var rosterArray = builder.Allocate(ref raceArray[i].Roster, rosterCount);
                    for (int r = 0; r < rosterCount; r++)
                        rosterArray[r] = entry.roster[r];
                }

                var blob = builder.CreateBlobAssetReference<RaceCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                AddBlobAsset(ref blob, out _);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new RaceCatalog { Value = blob });

                // Prefab entities go in a buffer, never in the blob — see RaceBaseVisual.
                var visuals = AddBuffer<RaceBaseVisual>(entity);
                foreach (var entry in authoring.races)
                {
                    if (entry.baseVisualPrefab == null)
                    {
                        Debug.LogWarning(
                            $"[RaceCatalogAuthoring] Race '{entry.race}' has no base visual prefab; " +
                            "that corner will render nothing when claimed.", authoring);
                        continue;
                    }

                    // Must be a prefab ASSET. Referencing an object placed in the subscene bakes a
                    // live scene entity instead of a prefab, so every race renders on every corner.
                    if (entry.baseVisualPrefab.scene.IsValid())
                    {
                        Debug.LogError(
                            $"[RaceCatalogAuthoring] Race '{entry.race}' base visual points at a scene " +
                            $"object ('{entry.baseVisualPrefab.name}'), not a prefab asset. Drag it from " +
                            "the Project window instead, and delete the copy in the Hierarchy.",
                            authoring);
                        continue;
                    }

                    visuals.Add(new RaceBaseVisual
                    {
                        Race = entry.race,
                        Prefab = GetEntity(entry.baseVisualPrefab, TransformUsageFlags.Dynamic)
                    });
                }
            }
        }
    }
}
