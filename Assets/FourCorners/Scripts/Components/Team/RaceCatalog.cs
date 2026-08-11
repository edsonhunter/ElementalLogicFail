using FourCorners.Scripts.Components.Minion;
using Unity.Entities;

namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// Per-race static data: which base visuals to show and which unit models to spawn.
    /// </summary>
    public struct RaceDefinition
    {
        public RaceType Race;

        /// <summary>Prefab entity for this race's base visuals, or Entity.Null if unauthored.</summary>
        public Entity BaseVisualPrefab;

        /// <summary>Unit models this race's spawners emit.</summary>
        public BlobArray<UnitModelType> Roster;
    }

    /// <summary>
    /// Immutable, shared across every spawner job — hence a BlobAsset rather than a buffer.
    /// </summary>
    public struct RaceCatalogBlob
    {
        public BlobArray<RaceDefinition> Races;

        /// <summary>
        /// Index of <paramref name="race"/> in <see cref="Races"/>, or -1 if absent.
        ///
        /// Returns an index rather than the definition itself: RaceDefinition contains a
        /// BlobArray, so it may only ever be accessed by ref out of blob storage — handing it
        /// back through an `out` parameter would copy it, which the Entities analyser rejects
        /// (EA0009). Callers do `ref var def = ref catalog.Races[index];`.
        ///
        /// Linear scan over at most <see cref="Races.Count"/> entries, and Burst-friendly.
        /// </summary>
        public int IndexOf(RaceType race)
        {
            for (int i = 0; i < Races.Length; i++)
            {
                if (Races[i].Race == race) return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// Singleton pointing at the baked catalog.
    ///
    /// OPTIONAL. When absent, spawners fall back to their baked SpawnerPrefab buffer and bases
    /// keep whatever visuals their prefab was authored with — i.e. the original
    /// race-is-the-corner behaviour. Author a RaceCatalogAuthoring in the gameplay subscene to
    /// switch the game over to runtime race selection.
    /// </summary>
    public struct RaceCatalog : IComponentData
    {
        public BlobAssetReference<RaceCatalogBlob> Value;
    }
}
