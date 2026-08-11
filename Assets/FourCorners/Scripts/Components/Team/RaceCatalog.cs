using FourCorners.Scripts.Components.Minion;
using Unity.Entities;

namespace FourCorners.Scripts.Components.Team
{
    /// <summary>
    /// Which unit models a race's spawners emit. Pure value data, safe to live in a blob.
    /// </summary>
    public struct RaceDefinition
    {
        public RaceType Race;
        public BlobArray<UnitModelType> Roster;
    }

    /// <summary>
    /// Immutable roster data, shared by every spawner job — hence a BlobAsset.
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
    /// Singleton pointing at the baked roster catalog.
    ///
    /// OPTIONAL. When absent, spawners fall back to their baked SpawnerPrefab buffer and bases
    /// keep whatever visuals their prefab was authored with — i.e. the original
    /// race-is-the-corner behaviour.
    /// </summary>
    public struct RaceCatalog : IComponentData
    {
        public BlobAssetReference<RaceCatalogBlob> Value;
    }

    /// <summary>
    /// Base visual prefab per race, on the same entity as <see cref="RaceCatalog"/>.
    ///
    /// WHY THIS IS A BUFFER AND NOT PART OF THE BLOB
    /// Entity references inside a BlobAsset are NOT remapped when an entity scene is
    /// deserialized — remapping only walks components and buffers. A baked Entity stored in a
    /// blob therefore points at a meaningless index at runtime, and instantiating it aborts the
    /// whole EntityCommandBuffer playback, taking unrelated systems down with it.
    ///
    /// Entity fields in an IBufferElementData are remapped correctly. Never put an Entity in a
    /// blob.
    /// </summary>
    public struct RaceBaseVisual : IBufferElementData
    {
        public RaceType Race;

        /// <summary>Prefab entity for this race's base visuals, or Entity.Null if unauthored.</summary>
        public Entity Prefab;
    }
}
