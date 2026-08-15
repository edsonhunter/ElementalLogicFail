using FourCorners.Scripts.Components.Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Ticks every <see cref="AttackCooldown"/> toward zero. AttackSystem consumes it.
    ///
    /// It used to tick MinionData.Cooldown, which nothing read — a timer counting down for no
    /// one. The value moved to its own component so towers and bases can reload too.
    ///
    /// Runs for every attacker, engaged or not, so a minion that has just won a fight can strike
    /// immediately on meeting the next enemy instead of standing there waiting out a reload it
    /// could have served while walking.
    ///
    /// Server-only: AttackCooldown is not replicated, so a client ticking it would be simulating
    /// a value it can never reconcile.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AttackCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackCooldown>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new TickCooldownJob { DeltaTime = SystemAPI.Time.DeltaTime };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct TickCooldownJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref AttackCooldown cooldown)
        {
            if (cooldown.Remaining <= 0f) return;

            cooldown.Remaining = math.max(0f, cooldown.Remaining - DeltaTime);
        }
    }
}
