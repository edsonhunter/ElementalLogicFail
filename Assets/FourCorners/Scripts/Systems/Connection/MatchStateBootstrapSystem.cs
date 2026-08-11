using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Runs once during server startup to guarantee the MatchState entity exists
    /// before ServerAcceptGameSystem processes any GoInGameRequest RPCs.
    ///
    /// Replaces the manual MatchStateAuthoring Baker — no Server sub-scene required.
    /// Idempotent: if a MatchStateTag entity already exists (e.g. from baking),
    /// this system disables itself immediately without creating a duplicate.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct MatchStateBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // If a baked entity already carries MatchStateTag, do nothing
            var existing = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStateTag>()
                .Build(ref state);

            if (!existing.IsEmpty)
            {
                state.Enabled = false;
                return;
            }

            // Create the canonical MatchState entity
            var matchStateEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<MatchStateTag>(matchStateEntity);

            // MatchPhase state — starts as WaitingForPlayers, gates minion spawning.
            // HostNetworkId = 0 means "no host elected yet".
            state.EntityManager.AddComponentData(matchStateEntity, new MatchState
            {
                Phase = MatchPhase.WaitingForPlayers,
                HostNetworkId = 0
            });

            // Player roster — grows as players are accepted by ServerAcceptGameSystem
            state.EntityManager.AddBuffer<ConnectedPlayerElement>(matchStateEntity);

            var buffer = state.EntityManager.AddBuffer<TeamStatusElement>(matchStateEntity);

            // One unoccupied slot per TeamNumber value
            for (int i = 0; i < Teams.Count; i++)
            {
                buffer.Add(new TeamStatusElement
                {
                    IsOccupied = false,
                    OccupyingPlayer = Entity.Null
                });
            }

            UnityEngine.Debug.Log($"[MatchStateBootstrapSystem] MatchState entity created with {Teams.Count} team slots.");

            // One-shot: disable after initialization so it never runs again
            state.Enabled = false;
        }

        // OnUpdate intentionally empty — all work is done in OnCreate
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}