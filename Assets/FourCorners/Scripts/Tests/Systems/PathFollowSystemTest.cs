using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Path;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Path;
using FourCorners.Scripts.Tests.Editor;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Tests.Systems
{
    [TestFixture]
    public class PathFollowSystemTest : ECSTestFixture
    {
        private const float Step = 0.5f;

        /// <summary>
        /// A minion on a one-waypoint lane, sitting at the origin and aimed down +X.
        ///
        /// The waypoint is far away on purpose: PathFollowSystem advances its index once it gets
        /// within 0.2 units, and a wrapped index would restart the walk and muddy the assertion.
        /// </summary>
        private Entity CreateWalker()
        {
            var entity = EntityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(PathFollower),
                typeof(MinionData),
                typeof(PathWaypoint));

            EntityManager.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            EntityManager.SetComponentData(entity, new PathFollower { CurrentIndex = 0 });
            EntityManager.SetComponentData(entity, EntityTest.CreateMinionData(TeamNumber.Team1, speed: 10f));

            var waypoints = EntityManager.GetBuffer<PathWaypoint>(entity);
            waypoints.Add(new PathWaypoint { Position = new float3(100f, 0f, 0f) });

            return entity;
        }

        private void Tick()
        {
            AdvanceTime(Step);
            World.GetOrCreateSystem<PathFollowSystem>().Update(World.Unmanaged);
            EntityManager.CompleteAllTrackedJobs();
        }

        private float3 PositionOf(Entity entity) =>
            EntityManager.GetComponentData<LocalTransform>(entity).Position;

        [Test]
        public void PathFollowSystem_MovesMinionTowardsItsWaypoint()
        {
            var minion = CreateWalker();

            Tick();

            Assert.Greater(PositionOf(minion).x, 0f);
        }

        /// <summary>
        /// The whole of "minions stop to fight" is this: an engaged minion no longer matches the
        /// movement query. There is no combat state machine in PathFollowSystem to get wrong.
        /// </summary>
        [Test]
        public void PathFollowSystem_DoesNotMoveEngagedMinion()
        {
            var minion = CreateWalker();
            EntityTest.Engage(EntityManager, minion, Entity.Null);

            Tick();

            Assert.AreEqual(0f, PositionOf(minion).x);
        }

        [Test]
        public void PathFollowSystem_ResumesAfterEngagementIsReleased()
        {
            var minion = CreateWalker();
            EntityTest.Engage(EntityManager, minion, Entity.Null);

            Tick();
            Assert.AreEqual(0f, PositionOf(minion).x, "Guard: the minion should be held while engaged.");

            EntityManager.RemoveComponent<Engagement>(minion);
            Tick();

            Assert.Greater(PositionOf(minion).x, 0f);
        }

        /// <summary>
        /// A released minion picks up from the waypoint it had already reached rather than
        /// restarting its lane — the index survives the fight because nothing touches it.
        /// </summary>
        [Test]
        public void PathFollowSystem_KeepsWaypointIndexAcrossAFight()
        {
            var minion = CreateWalker();
            var waypoints = EntityManager.GetBuffer<PathWaypoint>(minion);
            waypoints.Add(new PathWaypoint { Position = new float3(0f, 0f, 100f) });
            EntityManager.SetComponentData(minion, new PathFollower { CurrentIndex = 1 });

            EntityTest.Engage(EntityManager, minion, Entity.Null);
            Tick();
            EntityManager.RemoveComponent<Engagement>(minion);
            Tick();

            Assert.AreEqual(1, EntityManager.GetComponentData<PathFollower>(minion).CurrentIndex);
            Assert.Greater(PositionOf(minion).z, 0f);
        }
    }
}
