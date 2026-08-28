using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
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

        /// <summary>A bare minion — just enough to be found by team.</summary>
        public static Entity CreateTestMinion(EntityManager entityManager, TeamNumber team)
        {
            var entity = entityManager.CreateEntity(typeof(MinionData));
            entityManager.SetComponentData(entity, CreateMinionData(team, speed: 1f));
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

        /// <summary>
        /// Creates the match-state entity with an empty team slot per corner and an empty roster —
        /// the shape MatchStateBootstrapSystem produces on the server.
        /// </summary>
        public static Entity CreateMatchStateWithSlots(EntityManager entityManager, MatchPhase phase)
        {
            var entity = entityManager.CreateEntity(
                typeof(MatchStateTag),
                typeof(MatchState),
                typeof(MatchClock),
                typeof(TeamStatusElement),
                typeof(ConnectedPlayerElement));

            entityManager.SetComponentData(entity, new MatchState { Phase = phase });

            var slots = entityManager.GetBuffer<TeamStatusElement>(entity);
            for (int i = 0; i < Teams.Count; i++)
            {
                slots.Add(new TeamStatusElement { IsOccupied = false, OccupyingPlayer = Entity.Null });
            }

            return entity;
        }

        /// <summary>
        /// Creates the pair of components a base command arrives as on the server.
        ///
        /// ReceiveRpcCommandRequest is ordinary IComponentData, so a command can be delivered by
        /// hand without a live connection — which is the only reason ServerBaseCommandSystem is
        /// testable at all. Everything else in the connection pipeline needs a real NetCode
        /// handshake and is therefore not covered here.
        /// </summary>
        public static Entity CreateBaseCommandRpc(
            EntityManager entityManager,
            Entity sourceConnection,
            BaseCommandType type,
            byte targetSlot = 0)
        {
            var entity = entityManager.CreateEntity(
                typeof(BaseCommandRequest),
                typeof(ReceiveRpcCommandRequest));

            entityManager.SetComponentData(entity, new BaseCommandRequest
            {
                Type = type,
                TargetSlot = targetSlot
            });
            entityManager.SetComponentData(entity, new ReceiveRpcCommandRequest
            {
                SourceConnection = sourceConnection
            });

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
        /// A corner base with a purse, as PlayerBaseAuthoring bakes it.
        ///
        /// The economy lives on the base rather than the connection so it survives a disconnect —
        /// anything testing it has to build that shape or it is testing something else.
        /// </summary>
        public static Entity CreateTestPlayerBaseWithEconomy(
            EntityManager entityManager,
            TeamNumber team,
            int gold = 0,
            int incomePerSecond = 0,
            bool isActive = true)
        {
            var entity = CreateTestPlayerBase(entityManager, team, isActive);

            entityManager.AddComponentData(entity, new PlayerEconomy
            {
                Gold = gold,
                StartingGold = gold,
                IncomePerSecond = incomePerSecond,

                // The baseline, not the effective rate: IncomeSystem derives IncomePerSecond from
                // this and the central building's level every tick. A factory that set only the
                // former would produce a base that silently earns nothing.
                BaseIncomePerSecond = incomePerSecond
            });

            entityManager.AddComponentData(entity, new BuildingData
            {
                Type = BuildingType.Central,
                Level = 0
            });

            return entity;
        }

        /// <summary>An accepted, already-authenticated command intent, as the dispatcher emits it.</summary>
        public static Entity CreateBaseCommand(
            EntityManager entityManager,
            Entity baseEntity,
            TeamNumber team,
            BaseCommandType type,
            byte targetSlot = 0,
            Entity sourceConnection = default)
        {
            var entity = entityManager.CreateEntity(typeof(BaseCommand));
            entityManager.SetComponentData(entity, new BaseCommand
            {
                BaseEntity = baseEntity,
                Team = team,
                Type = type,
                TargetSlot = targetSlot,
                SourceConnection = sourceConnection
            });
            return entity;
        }

        /// <summary>Records a kill for BountySystem to pay out.</summary>
        public static Entity CreateKillEvent(
            EntityManager entityManager, TeamNumber killerTeam, TeamNumber victimTeam)
        {
            var entity = entityManager.CreateEntity(typeof(KillEvent));
            entityManager.SetComponentData(entity, new KillEvent
            {
                KillerTeam = killerTeam,
                VictimTeam = victimTeam
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
            float timer,
            int slot = 0)
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
                SpawnAmount = 1,
                LaneIndex = slot
            });

            // A spawner is a barracks. SpawnerSystem derives its effective wave size and interval
            // from this level, so its absence would quietly mean "level zero" rather than fail.
            entityManager.AddComponentData(entity, new BuildingData
            {
                Type = BuildingType.Barracks,
                Slot = (byte)slot,
                Level = 0
            });

            entityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });

            var prefabs = entityManager.GetBuffer<SpawnerPrefab>(entity);
            prefabs.Add(new SpawnerPrefab { ModelType = UnitModelType.Warrior });

            return entity;
        }
    }
}
