using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Consumes LobbyStateUpdateRpc and folds it into the <see cref="LobbyStateSnapshot"/>
    /// singleton. It does not touch the managed layer — BridgeNotificationSystem observes the
    /// snapshot and drives the UI, which keeps this system unmanaged and Burst-compiled.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientLobbyStateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LobbyStateUpdateRpc, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));

            // One-time structural change during world init, before any job can be scheduled.
            // CreateEntity()/AddComponent<T>() rather than CreateEntity(typeof(T)) — the latter
            // takes a managed ComponentType[] and is not Burst-compatible.
            var snapshotEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<LobbyStateSnapshot>(snapshotEntity);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var snapshot = SystemAPI.GetSingletonRW<LobbyStateSnapshot>();

            foreach (var (rpc, reqEntity) in
                     SystemAPI.Query<RefRO<LobbyStateUpdateRpc>>()
                         .WithAll<ReceiveRpcCommandRequest>()
                         .WithEntityAccess())
            {
                snapshot.ValueRW.IsHost = rpc.ValueRO.IsHost;
                snapshot.ValueRW.PlayerCount = rpc.ValueRO.PlayerCount;
                snapshot.ValueRW.Version++;

                FixedString128Bytes log = "[ClientLobbyStateSystem] Lobby update: PlayerCount=";
                log.Append(rpc.ValueRO.PlayerCount);
                log.Append((FixedString32Bytes)", IsHost=");
                log.Append(rpc.ValueRO.IsHost ? '1' : '0');
                UnityEngine.Debug.Log(log);

                ecb.DestroyEntity(reqEntity);
            }
        }
    }
}
