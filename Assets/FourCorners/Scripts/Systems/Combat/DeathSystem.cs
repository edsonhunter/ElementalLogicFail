using FourCorners.Scripts.Components.Combat;
using Unity.Burst;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Removes whatever has run out of <see cref="Health"/>.
    ///
    /// Only entities tagged <see cref="DestroyOnDeath"/> are destroyed. A corner base will have
    /// health too, and must survive its own destruction as a deactivated ghost — so death is opt
    /// in rather than something every damageable thing inherits.
    ///
    /// This is where the kill reward will be emitted once Tier 1.2 introduces an economy; it is
    /// the one place that knows a unit actually died rather than merely got hurt.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<DestroyOnDeath>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            var job = new DestroyDeadJob { Ecb = ecb };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(DestroyOnDeath))]
    public partial struct DestroyDeadJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter Ecb;

        private void Execute(Entity entity, [EntityIndexInQuery] int sortKey, RefRO<Health> health)
        {
            if (health.ValueRO.Current > 0) return;

            Ecb.DestroyEntity(sortKey, entity);
        }
    }
}
