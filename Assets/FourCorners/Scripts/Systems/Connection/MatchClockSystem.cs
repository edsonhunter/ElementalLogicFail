using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Runs the match clock and forces an ending if nobody manages one.
    ///
    /// A four-way free-for-all rewards turtling: the safe play is to fortify and let the other
    /// three grind each other down, which can leave two survivors staring at each other forever.
    /// Past <see cref="SuddenDeathAfterSeconds"/> every remaining corner starts taking damage on
    /// its own, ramping with time, so the match always resolves.
    ///
    /// Escalating damage rather than an abrupt tie-break on the whistle: a match that ends because
    /// bases got fragile still ends on someone's play, whereas one that ends because a timer said
    /// so ends on nothing.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MatchClockSystem : ISystem
    {
        /// <summary>How long players get before the map starts killing them.</summary>
        private const float SuddenDeathAfterSeconds = 600f;

        /// <summary>Seconds between sudden-death ticks.</summary>
        private const float TickInterval = 5f;

        /// <summary>Damage on the first tick. Every subsequent tick adds this much again.</summary>
        private const int DamagePerTick = 10;

        private float _tickTimer;
        private int _ticksElapsed;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchStateTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var matchStateEntity = SystemAPI.GetSingletonEntity<MatchStateTag>();
            var matchState = SystemAPI.GetComponent<MatchState>(matchStateEntity);

            if (matchState.Phase != MatchPhase.Active)
            {
                // Reset so a second match on the same server world does not inherit a clock that
                // is already past the threshold.
                _tickTimer = 0f;
                _ticksElapsed = 0;
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            matchState.ElapsedSeconds += deltaTime;

            if (matchState.ElapsedSeconds < SuddenDeathAfterSeconds)
            {
                SystemAPI.SetComponent(matchStateEntity, matchState);
                return;
            }

            if (!matchState.SuddenDeathActive)
            {
                matchState.SuddenDeathActive = true;
                UnityEngine.Debug.Log(
                    $"[MatchClockSystem] Sudden death after {matchState.ElapsedSeconds:F0}s — " +
                    "every remaining base now decays.");
            }

            SystemAPI.SetComponent(matchStateEntity, matchState);

            _tickTimer += deltaTime;
            if (_tickTimer < TickInterval) return;

            _tickTimer -= TickInterval;
            _ticksElapsed++;

            int damage = DamagePerTick * _ticksElapsed;

            // Written straight through rather than queued: this runs once every few seconds over
            // at most Teams.Count entities, so the two-phase damage pipeline would be ceremony.
            state.CompleteDependency();

            foreach (var (playerBase, health) in
                     SystemAPI.Query<RefRO<PlayerBase>, RefRW<Health>>())
            {
                if (!playerBase.ValueRO.IsActive) continue;

                health.ValueRW.Current = math.max(0, health.ValueRO.Current - damage);
            }
        }
    }
}
