using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Tests
{
    /// <summary>
    /// Archetype factories for system tests.
    ///
    /// These must mirror what the systems actually query. They had drifted: the spawner factory
    /// still built the pre-PlayerBase-refactor archetype, so SpawnerSystem's query could never
    /// match it and the tests asserted against a system that never ran.
    /// </summary>
    public static class EntityTest
    {
        /// <summary>Deterministic seed — test fixtures must not depend on UnityEngine.Random.</summary>
        private const uint TestSeed = 12345;

        public static MinionData CreateMinionData(TeamNumber type, float speed)
        {
            return new MinionData
            {
                TeamNumber = type,
                Speed = speed,
                Target = float3.zero,
                RandomSeed = TestSeed
            };
        }

        /// <summary>
        /// Creates something that can fight: a transform to measure range against, health to lose,
        /// and an attack that is ready to swing immediately.
        ///
        /// <paramref name="cooldown"/> defaults to 0 so a test can assert on the first hit without
        /// first having to tick a wind-up it does not care about.
        /// </summary>
        public static Entity CreateCombatant(
            EntityManager entityManager,
            float3 position,
            int health,
            int damage = 1,
            float interval = 1f,
            float range = 2f,
            float cooldown = 0f)
        {
            var entity = entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(Health),
                typeof(AttackStats),
                typeof(AttackCooldown),
                typeof(DestroyOnDeath));

            entityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            entityManager.SetComponentData(entity, new Health { Current = health, Max = health });
            entityManager.SetComponentData(entity, new AttackStats
            {
                Damage = damage,
                Interval = interval,
                Range = range
            });
            entityManager.SetComponentData(entity, new AttackCooldown { Remaining = cooldown });

            return entity;
        }

        /// <summary>Locks <paramref name="attacker"/> onto <paramref name="target"/>.</summary>
        public static void Engage(EntityManager entityManager, Entity attacker, Entity target)
        {
            entityManager.AddComponentData(attacker, new Engagement { Target = target });
        }

        /// <summary>
        /// Creates the MatchState singleton in the Active phase. SpawnerSystem and
        /// MinionSpawningSystem both gate on this, so tests must supply it.
        /// </summary>
        public static Entity CreateActiveMatchState(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity(typeof(MatchStateTag), typeof(MatchState));
            entityManager.SetComponentData(entity, new MatchState { Phase = MatchPhase.Active });
            return entity;
        }

        /// <summary>Creates an activated corner base — the authority SpawnerSystem checks.</summary>
        public static Entity CreateTestPlayerBase(EntityManager entityManager, TeamNumber team, bool isActive = true)
        {
            var entity = entityManager.CreateEntity(typeof(PlayerBase));
            entityManager.SetComponentData(entity, new PlayerBase
            {
                TeamNumber = team,
                IsActive = isActive,
                NetworkId = isActive ? 1 : 0
            });
            return entity;
        }

        /// <summary>
        /// Creates a spawner owned by <paramref name="playerBaseEntity"/>.
        ///
        /// LocalToWorld, not LocalTransform: SpawnerJob queries RefRO&lt;LocalToWorld&gt;. Getting
        /// this wrong does not fail loudly — the entity simply never matches the query.
        /// </summary>
        public static Entity CreateTestSpawner(
            EntityManager entityManager,
            Entity playerBaseEntity,
            float spawnInterval,
            float timer)
        {
            var entity = entityManager.CreateEntity(
                typeof(SpawnerData),
                typeof(LocalToWorld),
                typeof(MinionSpawnRequest),
                typeof(SpawnerPrefab));

            entityManager.SetComponentData(entity, new SpawnerData
            {
                PlayerBaseEntity = playerBaseEntity,
                SpawnInterval = spawnInterval,
                Timer = timer,
                IsActive = true,
                SpawnAmount = 1
            });

            entityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });

            var prefabs = entityManager.GetBuffer<SpawnerPrefab>(entity);
            prefabs.Add(new SpawnerPrefab { ModelType = UnitModelType.Warrior });

            return entity;
        }
    }
}
