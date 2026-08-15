using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Combat;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class BaseAttackSystemTest : ECSTestFixture
    {
        private Entity CreateBase(TeamNumber team, float3 position, int health, bool isActive = true)
        {
            var entity = EntityManager.CreateEntity(
                typeof(PlayerBase),
                typeof(LocalTransform),
                typeof(Health));

            EntityManager.SetComponentData(entity, new PlayerBase
            {
                TeamNumber = team,
                IsActive = isActive,
                NetworkId = isActive ? 1 : 0
            });
            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(position));
            EntityManager.SetComponentData(entity, new Health { Current = health, Max = health });

            return entity;
        }

        /// <summary>A minion that can attack right now, positioned at <paramref name="position"/>.</summary>
        private Entity CreateAttacker(TeamNumber team, float3 position, int damage = 5, float range = 2f)
        {
            var entity = EntityTest.CreateCombatant(
                EntityManager, position, health: 10, damage: damage, interval: 1f, range: range);

            EntityManager.AddComponentData(entity, EntityTest.CreateMinionData(team, speed: 1f));
            return entity;
        }

        private void Tick()
        {
            World.GetOrCreateSystem<BaseAttackSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
        }

        private int HealthOf(Entity entity) => EntityManager.GetComponentData<Health>(entity).Current;

        [Test]
        public void BaseAttackSystem_DamagesEnemyBaseInRange()
        {
            var enemyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100);
            CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);

            Tick();

            Assert.AreEqual(95, HealthOf(enemyBase));
        }

        [Test]
        public void BaseAttackSystem_IgnoresOwnBase()
        {
            var ownBase = CreateBase(TeamNumber.Team1, float3.zero, health: 100);
            CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);

            Tick();

            Assert.AreEqual(100, HealthOf(ownBase));
        }

        [Test]
        public void BaseAttackSystem_IgnoresBaseOutOfRange()
        {
            var enemyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100);
            CreateAttacker(TeamNumber.Team1, new float3(50f, 0f, 0f), damage: 5, range: 2f);

            Tick();

            Assert.AreEqual(100, HealthOf(enemyBase));
        }

        /// <summary>An unclaimed or already-destroyed corner is not a punching bag.</summary>
        [Test]
        public void BaseAttackSystem_IgnoresInactiveBase()
        {
            var emptyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100, isActive: false);
            CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);

            Tick();

            Assert.AreEqual(100, HealthOf(emptyBase));
        }

        /// <summary>
        /// The design is that a minion hits the base it is walking past and keeps going, so base
        /// damage must not depend on Engagement — and a minion locked in a duel ignores buildings.
        /// </summary>
        [Test]
        public void BaseAttackSystem_SkipsMinionsBusyFightingAnotherMinion()
        {
            var enemyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100);
            var attacker = CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);
            EntityTest.Engage(EntityManager, attacker, Entity.Null);

            Tick();

            Assert.AreEqual(100, HealthOf(enemyBase));
        }

        [Test]
        public void BaseAttackSystem_ReloadsBetweenHits()
        {
            var enemyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100);
            CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);

            Tick();
            Tick();

            Assert.AreEqual(95, HealthOf(enemyBase));
        }

        [Test]
        public void BaseAttackSystem_AppliesEveryAttackersDamage()
        {
            var enemyBase = CreateBase(TeamNumber.Team2, float3.zero, health: 100);
            CreateAttacker(TeamNumber.Team1, new float3(1f, 0f, 0f), damage: 5);
            CreateAttacker(TeamNumber.Team3, new float3(-1f, 0f, 0f), damage: 7);

            Tick();

            Assert.AreEqual(88, HealthOf(enemyBase));
        }
    }
}
