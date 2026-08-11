using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Consumes TeamRejectedRpc and raises <see cref="JoinRejectedTag"/> so the UI can tell the
    /// player the match is full. Without a consumer these RPC entities also accumulated forever
    /// on the client.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientTeamRejectedSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TeamRejectedRpc, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            bool alreadyRejected = SystemAPI.HasSingleton<JoinRejectedTag>();

            foreach (var (_, reqEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<TeamRejectedRpc>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(reqEntity);

                if (alreadyRejected) continue;
                alreadyRejected = true;

                ecb.AddComponent<JoinRejectedTag>(ecb.CreateEntity());
                UnityEngine.Debug.LogWarning(
                    (FixedString128Bytes)"[ClientTeamRejectedSystem] Join rejected: every corner is occupied.");
            }
        }
    }
}
