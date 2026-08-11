using FourCorners.Scripts.Components.Minion;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Systems.Collision
{
    /// <summary>
    /// Ticks MinionData.Cooldown toward zero.
    ///
    /// Server-only: Cooldown is deliberately not a [GhostField], so running this on clients
    /// simulated a value the server never replicates — guaranteed divergence, plus wasted work
    /// in every thin client. This was the only gameplay system without a world filter.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MinionData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (RefRW<MinionData> minionData in SystemAPI.Query<RefRW<MinionData>>())
            {
                if (minionData.ValueRO.Cooldown > 0f)
                {
                    minionData.ValueRW.Cooldown = math.max(0f, minionData.ValueRO.Cooldown - deltaTime);
                }
            }
        }
    }
}
