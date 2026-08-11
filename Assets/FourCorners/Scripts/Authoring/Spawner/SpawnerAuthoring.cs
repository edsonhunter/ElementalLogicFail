using System.Collections.Generic;
using FourCorners.Scripts.Components.Minion;
using Unity.Entities;
using UnityEngine;

namespace FourCorners.Scripts.Authoring.Spawner
{
    public class SpawnerAuthoring : MonoBehaviour
    {
        public List<UnitModelType> UnitsToSpawn;
        public int spawnAmount = 5;
        public float spawnInterval = 2.0f;

        [Tooltip("Which of this base's three lanes to walk (0, 1 or 2). Lanes are generated " +
                 "automatically from the positions of the four bases — see LaneBakingSystem.")]
        public int laneIndex;

        [Tooltip("OPTIONAL manual lane. Leave empty to use the generated lane. Only fill this in " +
                 "to hand-author an unusual route; any entry here disables generation for this " +
                 "spawner.")]
        public List<Transform> Waypoints;

        public class SpawnerAuthoringBaker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Resolve the owning PlayerBase entity from the parent GameObject.
                // Must be a direct child of a PlayerBaseAuthoring object.
                //
                // Fail loudly at bake time: an unparented spawner used to bake
                // PlayerBaseEntity = Entity.Null, after which SpawnerSystem's authority check
                // and MinionSpawningSystem's team lookup both silently returned forever with no
                // diagnostic anywhere — the spawner simply never fired.
                var parent = authoring.transform.parent;
                if (parent == null || parent.GetComponent<PlayerBaseAuthoring>() == null)
                {
                    Debug.LogError(
                        $"[SpawnerAuthoring] '{authoring.name}' must be a direct child of a GameObject " +
                        "with PlayerBaseAuthoring. It will never spawn anything as authored.",
                        authoring);
                    return;
                }

                var parentEntity = GetEntity(parent, TransformUsageFlags.Dynamic);

                AddComponent(entity, new Components.Spawner.SpawnerData
                {
                    PlayerBaseEntity = parentEntity,
                    SpawnAmount = authoring.spawnAmount,
                    SpawnInterval = authoring.spawnInterval,
                    Timer = 0,
                    IsActive = false,
                    LaneIndex = authoring.laneIndex
                });

                // Unit prefab types this spawner can produce
                if (authoring.UnitsToSpawn != null)
                {
                    var prefabBuffer = AddBuffer<Components.Spawner.SpawnerPrefab>(entity);
                    foreach (var unitType in authoring.UnitsToSpawn)
                        prefabBuffer.Add(new Components.Spawner.SpawnerPrefab { ModelType = unitType });
                }

                AddBuffer<Components.Request.MinionSpawnRequest>(entity);

                var waypointBuffer = AddBuffer<Components.Path.PathWaypoint>(entity);
                if (authoring.Waypoints != null)
                {
                    foreach (var wp in authoring.Waypoints)
                    {
                        if (wp != null)
                            waypointBuffer.Add(new Components.Path.PathWaypoint { Position = wp.position });
                    }
                }
            }
        }
    }
}