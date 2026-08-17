using FourCorners.Scripts.Components.Command;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Command
{
    /// <summary>
    /// Ends the life of every <see cref="BaseCommand"/> intent, and complains about the ones
    /// nothing acted on.
    ///
    /// The one-frame lifetime falls out of how this project defers structural change rather than
    /// being enforced here. ServerBaseCommandSystem records the intent into the end-of-frame ECB,
    /// so it first becomes visible on the following frame; every handler sees it on that frame;
    /// this records the destroy, which plays back at the end of that same frame. Order among the
    /// handlers is irrelevant — none of them can miss it, and none of them can see it twice.
    ///
    /// Both ordering attributes are load-bearing, unusually for this project.
    ///
    /// <c>OrderLast</c> is for the diagnostic: destroying is order-free, but reading
    /// <see cref="BaseCommand.Handled"/> is not, and a handler updating after this system would set
    /// the flag too late and earn a false accusation. Handlers must therefore not be OrderLast
    /// themselves.
    ///
    /// The <c>UpdateBefore</c> is for correctness, and it is the subtle one.
    /// EndSimulationEntityCommandBufferSystem is *also* OrderLast, and ordering within that bucket
    /// is otherwise arbitrary — so this could sort after it, and the destroy would go into the
    /// following frame's buffer instead. The intent would then survive two frames and every handler
    /// would apply it twice: one click, two upgrades, and nothing in the logs to suggest why.
    ///
    /// The warning is the point of this system as much as the cleanup. A command type whose handler
    /// was never written, or whose query stopped matching, is otherwise perfectly silent — the RPC
    /// sends, the server accepts, and nothing happens, with nothing anywhere saying so.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct BaseCommandCleanupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BaseCommand>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (command, entity) in
                     SystemAPI.Query<RefRO<BaseCommand>>().WithEntityAccess())
            {
                if (!command.ValueRO.Handled)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[BaseCommandCleanupSystem] {command.ValueRO.Type} from Team " +
                        $"{command.ValueRO.Team} passed a full frame with no handler and is being " +
                        "discarded. Expected until the system that implements it lands.");
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
