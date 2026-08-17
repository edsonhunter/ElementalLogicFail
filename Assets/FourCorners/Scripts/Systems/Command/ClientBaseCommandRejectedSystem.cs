using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Command
{
    /// <summary>
    /// Turns the server's <see cref="BaseCommandRejectedRpc"/> into a
    /// <see cref="BaseCommandRejectedTag"/> for BridgeNotificationSystem to hand to the UI.
    ///
    /// The RPC is destroyed unconditionally, but the tag is only raised where something will
    /// consume it. Only the presentation client world runs BridgeNotificationSystem, so a tag
    /// raised anywhere else would sit in that world forever — the same unbounded accumulation that
    /// once left 938 ClientSceneReady entities lying around. Nothing but the presentation world can
    /// send a command in the first place, so in practice no other world is ever answered; the guard
    /// is there so that stops being something we have to be right about.
    /// </summary>
    /// <remarks>
    /// Not Burst-compiled, for the same reason as ServerBaseCommandSystem: it only ticks when a
    /// player did something the server refused, and under Burst the log would have to spell the
    /// command and reason as raw integers. Naming them is the entire value of the message.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientBaseCommandRejectedSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BaseCommandRejectedRpc, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            bool presenting = SystemAPI.HasSingleton<PresentationClientTag>();

            foreach (var (rpc, rpcEntity) in
                     SystemAPI.Query<RefRO<BaseCommandRejectedRpc>>()
                         .WithAll<ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(rpcEntity);
                if (!presenting) continue;

                var tag = ecb.CreateEntity();
                ecb.AddComponent(tag, new BaseCommandRejectedTag
                {
                    Type = rpc.ValueRO.Type,
                    Reason = rpc.ValueRO.Reason
                });

                UnityEngine.Debug.Log(
                    $"[ClientBaseCommandRejectedSystem] {rpc.ValueRO.Type} rejected: " +
                    $"{rpc.ValueRO.Reason}.");
            }
        }
    }
}
