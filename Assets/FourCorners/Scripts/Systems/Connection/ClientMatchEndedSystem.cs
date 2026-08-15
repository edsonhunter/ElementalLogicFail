using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Consumes the server's MatchEndedRpc and raises <see cref="MatchEndedTag"/>.
    ///
    /// Resolves "did I win?" here, while this world's own NetworkId is at hand, so the managed
    /// layer receives an answer rather than two ids to compare.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientMatchEndedSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchEndedRpc, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // A client that never finished connecting has no NetworkId; it simply cannot be the
            // winner, so treat the id as one nobody holds.
            int localNetworkId = SystemAPI.TryGetSingleton<NetworkId>(out var networkId)
                ? networkId.Value
                : 0;

            bool alreadyEnded = SystemAPI.HasSingleton<MatchEndedTag>();

            foreach (var (rpc, reqEntity) in
                     SystemAPI.Query<RefRO<MatchEndedRpc>>()
                         .WithAll<ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(reqEntity);

                if (alreadyEnded) continue;
                alreadyEnded = true;

                int winner = rpc.ValueRO.WinnerNetworkId;

                var tagEntity = ecb.CreateEntity();
                ecb.AddComponent(tagEntity, new MatchEndedTag
                {
                    WinnerNetworkId = winner,
                    LocalPlayerWon = winner != 0 && winner == localNetworkId
                });

                FixedString128Bytes log = "[ClientMatchEndedSystem] MatchEndedRpc received. Winner NetworkId=";
                log.Append(winner);
                UnityEngine.Debug.Log(log);
            }
        }
    }
}
