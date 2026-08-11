using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Sends GoInGameRequest as soon as this client has a NetworkId, carrying the lobby choices
    /// from the <see cref="LocalPlayerSelection"/> singleton.
    ///
    /// That selection used to be a `public static int DesiredTeamIndex` on this struct which
    /// nothing in the project ever assigned, so every client silently requested "any slot" and
    /// team selection was dead on arrival.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientRequestGameSystem : ISystem
    {
        private EntityQuery _pendingNetworkIdQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .WithNone<ClientLobbyJoinedTag>();
            _pendingNetworkIdQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(_pendingNetworkIdQuery);

            // Seeded here so the singleton always exists — the UI overwrites it if the player
            // makes a choice, and thin clients (which have no UI) still send a valid request.
            var selectionEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<LocalPlayerSelection>(selectionEntity);
            state.EntityManager.SetComponentData(selectionEntity, LocalPlayerSelection.Default);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var selection = SystemAPI.GetSingleton<LocalPlayerSelection>();

            using var connectionEntities = _pendingNetworkIdQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in connectionEntities)
            {
                // Mark the connection as having joined so this fires exactly once.
                // NetworkStreamInGame is deliberately NOT added — ghost streaming waits for
                // ReadyForGhostsRequest once the client's SubScenes are baked.
                ecb.AddComponent<ClientLobbyJoinedTag>(entity);

                FixedString128Bytes log = "[ClientRequestGameSystem] GoInGameRequest team=";
                log.Append(selection.DesiredTeamIndex);
                log.Append((FixedString32Bytes)" race=");
                log.Append((int)selection.Race);
                UnityEngine.Debug.Log(log);

                var req = ecb.CreateEntity();
                ecb.AddComponent(req, new GoInGameRequest
                {
                    RequestedTeamIndex = selection.DesiredTeamIndex,
                    RequestedRace = selection.Race
                });
                ecb.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
            }
        }
    }
}
