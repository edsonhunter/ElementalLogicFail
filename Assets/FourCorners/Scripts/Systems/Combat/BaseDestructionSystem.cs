using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Combat
{
    /// <summary>
    /// Turns a corner whose health has run out into an eliminated corner.
    ///
    /// This is the counterpart to DeathSystem for the one thing that must NOT be destroyed. The
    /// base entity survives: it is a ghost carrying the team's identity, and the eliminated player
    /// is still connected and still watching. Only its *participation* ends.
    ///
    /// Deactivating the corner is all this does. Silencing the spawners and clearing the field is
    /// CornerTeardownSystem's job, which reacts to any corner going inactive — so a corner that
    /// dies and a corner whose owner left are cleaned up by the same code.
    ///
    /// The slot is left occupied. Freeing it would hand a dead corner to the next player to
    /// connect, which is both unfair and confusing; ServerDisconnectSystem is the only thing that
    /// frees a slot.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BaseDestructionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchStateTag>();
            state.RequireForUpdate<PlayerBase>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: false);

            foreach (var (playerBase, health) in
                     SystemAPI.Query<RefRW<PlayerBase>, RefRO<Health>>())
            {
                if (!playerBase.ValueRO.IsActive) continue;
                if (health.ValueRO.Current > 0) continue;

                var team = playerBase.ValueRO.TeamNumber;
                int networkId = playerBase.ValueRO.NetworkId;

                // NetworkId is kept, unlike a disconnect: the corner is dead but still belongs to
                // someone, and the match outcome needs to know who is out.
                playerBase.ValueRW.IsActive = false;

                int index = (int)team;
                if (index >= 0 && index < teamBuffer.Length)
                {
                    var slot = teamBuffer[index];
                    slot.IsEliminated = true;
                    teamBuffer[index] = slot;
                }

                UnityEngine.Debug.Log(
                    $"[BaseDestructionSystem] Team {team} (NetworkId={networkId}) eliminated — base destroyed.");
            }
        }
    }
}
