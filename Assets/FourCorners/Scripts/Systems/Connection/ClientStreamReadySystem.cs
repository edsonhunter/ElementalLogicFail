using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Once <see cref="ClientSceneReady"/> exists, brings this client's connection in-game
    /// locally and asks the server to start streaming ghosts.
    ///
    /// All world discrimination now lives in ClientSceneReadySystem, which is why this system
    /// is a plain unmanaged query and Burst-compiled.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientStreamReadySystem : ISystem
    {
        private EntityQuery _pendingConnectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<ClientSceneReady>();

            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId, ClientLobbyJoinedTag>()
                .WithNone<NetworkStreamInGame>();
            _pendingConnectionQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(_pendingConnectionQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            using var connectionEntities = _pendingConnectionQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in connectionEntities)
            {
                UnityEngine.Debug.Log(
                    (FixedString128Bytes)"[ClientStreamReadySystem] Scene ready — going in-game and requesting ghosts.");

                // Start expecting ghost snapshots locally.
                ecb.AddComponent<NetworkStreamInGame>(entity);

                // Ask the server to start transmitting them.
                var req = ecb.CreateEntity();
                ecb.AddComponent<ReadyForGhostsRequest>(req);
                ecb.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
            }
        }
    }
}
