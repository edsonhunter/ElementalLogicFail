using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Decides when the match is over and tells everyone.
    ///
    /// Separate from BaseDestructionSystem on purpose: "this corner died" and "the match is
    /// decided" are different questions, and the second one also has to answer for players who
    /// left rather than lost.
    ///
    /// Survivors are counted from the team slots, NOT from PlayerBase.IsActive. Bases are
    /// activated several frames after the match goes Active — the client has to report its
    /// SubScene ready first — so a base-based count reads zero survivors at kickoff and would
    /// declare the match over before it began. Slots are settled during the lobby and only change
    /// on elimination or departure, which is exactly the question being asked.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MatchOutcomeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<MatchStateTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var matchStateEntity = SystemAPI.GetSingletonEntity<MatchStateTag>();
            var matchState = SystemAPI.GetComponent<MatchState>(matchStateEntity);

            if (matchState.Phase != MatchPhase.Active) return;

            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: true);
            var networkIdLookup = SystemAPI.GetComponentLookup<NetworkId>(isReadOnly: true);

            int survivors = 0;
            int winnerNetworkId = 0;

            for (int i = 0; i < teamBuffer.Length; i++)
            {
                var slot = teamBuffer[i];
                if (!slot.IsOccupied || slot.IsEliminated) continue;

                survivors++;

                if (networkIdLookup.TryGetComponent(slot.OccupyingPlayer, out var networkId))
                {
                    winnerNetworkId = networkId.Value;
                }
            }

            // Two or more still in it: nothing decided.
            if (survivors > 1) return;

            // Zero survivors still has to end the match — everyone eliminated, or everyone walked
            // out. Left Active, the server would sit there forever and the next player to connect
            // would join a match that can never be won.
            if (survivors == 0) winnerNetworkId = 0;

            matchState.Phase = MatchPhase.Ended;
            matchState.WinnerNetworkId = winnerNetworkId;

            // Written immediately, matching ServerAcceptGameSystem and ServerDisconnectSystem —
            // deferring MatchState through the ECB lets a same-frame writer replay a stale copy
            // over the phase change.
            SystemAPI.SetComponent(matchStateEntity, matchState);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var playerBuffer = SystemAPI.GetSingletonBuffer<ConnectedPlayerElement>(isReadOnly: true);
            for (int i = 0; i < playerBuffer.Length; i++)
            {
                var rpc = ecb.CreateEntity();
                ecb.AddComponent(rpc, new MatchEndedRpc { WinnerNetworkId = winnerNetworkId });
                ecb.AddComponent(rpc, new SendRpcCommandRequest
                {
                    TargetConnection = playerBuffer[i].ConnectionEntity
                });
            }

            UnityEngine.Debug.Log(
                $"[MatchOutcomeSystem] Match over. Winner NetworkId={winnerNetworkId} " +
                $"({survivors} corner(s) left). Notified {playerBuffer.Length} player(s).");
        }
    }
}
