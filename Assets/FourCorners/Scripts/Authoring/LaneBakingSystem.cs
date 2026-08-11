using FourCorners.Scripts.Components.Path;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Authoring
{
    /// <summary>
    /// Generates every spawner's lane at bake time from the positions of the corner bases.
    ///
    /// WHY THIS EXISTS
    /// Lanes used to be hand-authored: a List&lt;Transform&gt; per spawner, stored as prefab
    /// overrides in the subscene — 48 references for 12 spawners. That data is invisible in the
    /// Inspector until you look, and it is silently destroyed by ordinary prefab work: swapping
    /// the four base prefabs for one shared prefab dropped every override and nulled the rest,
    /// which left minions with empty lanes and no error anywhere.
    ///
    /// A lane is fully determined by the base layout, so deriving it is strictly better than
    /// storing it. The only per-spawner input left is <see cref="SpawnerData.LaneIndex"/>.
    ///
    /// A spawner whose PathWaypoint buffer is already populated is left alone, so a hand-authored
    /// route still wins if you ever want one.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial struct LaneBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var positions = new NativeArray<float3>(Teams.Count, Allocator.Temp);
            var present = new NativeArray<bool>(Teams.Count, Allocator.Temp);

            // Bases are root objects in the subscene, so LocalTransform.Position is the world
            // position — the same value the manual Transform references used to bake.
            foreach (var (playerBase, transform) in
                     SystemAPI.Query<RefRO<PlayerBase>, RefRO<LocalTransform>>())
            {
                int team = (int)playerBase.ValueRO.TeamNumber;
                if (team < 0 || team >= Teams.Count) continue;

                positions[team] = transform.ValueRO.Position;
                present[team] = true;
            }

            int basesFound = 0;
            for (int i = 0; i < Teams.Count; i++)
                if (present[i]) basesFound++;

            int lanesBuilt = 0;

            foreach (var (spawner, waypoints) in
                     SystemAPI.Query<RefRO<SpawnerData>, DynamicBuffer<PathWaypoint>>())
            {
                // A hand-authored route wins.
                if (!waypoints.IsEmpty) continue;

                var baseEntity = spawner.ValueRO.PlayerBaseEntity;
                if (!SystemAPI.HasComponent<PlayerBase>(baseEntity)) continue;

                int ownTeam = (int)SystemAPI.GetComponent<PlayerBase>(baseEntity).TeamNumber;
                if (ownTeam < 0 || ownTeam >= Teams.Count || !present[ownTeam]) continue;

                BuildLane(waypoints, positions, present, ownTeam, spawner.ValueRO.LaneIndex);
                lanesBuilt++;
            }

            // One line per bake, so "did lanes generate?" is answerable by looking rather than
            // by guessing from minion behaviour.
            if (lanesBuilt > 0)
            {
                UnityEngine.Debug.Log(
                    $"[LaneBakingSystem] Generated {lanesBuilt} lane(s) from {basesFound} base(s).");
            }

            positions.Dispose();
            present.Dispose();
        }

        /// <summary>
        /// Visit every enemy corner once, then come home. <paramref name="laneIndex"/> rotates the
        /// starting point so a base's three spawners take three different routes.
        ///
        /// Home last is load-bearing: PathFollowSystem wraps its index at the end of the buffer,
        /// so "walk home and start over" is that wrap rather than separate logic.
        /// </summary>
        private static void BuildLane(
            DynamicBuffer<PathWaypoint> waypoints,
            in NativeArray<float3> positions,
            in NativeArray<bool> present,
            int ownTeam,
            int laneIndex)
        {
            int enemyCount = Teams.Count - 1;
            int rotation = enemyCount > 0 ? ((laneIndex % enemyCount) + enemyCount) % enemyCount : 0;

            for (int step = 0; step < enemyCount; step++)
            {
                int enemy = (ownTeam + 1 + ((step + rotation) % enemyCount)) % Teams.Count;
                if (present[enemy])
                    waypoints.Add(new PathWaypoint { Position = positions[enemy] });
            }

            waypoints.Add(new PathWaypoint { Position = positions[ownTeam] });
        }
    }
}
