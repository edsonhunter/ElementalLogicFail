using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Attaches <see cref="ConnectionState"/> to every connection the moment it has a NetworkId.
    ///
    /// NetCode never adds this component on its own — without it a drop produces no observable
    /// signal at all. It used to be added inside ServerAcceptGameSystem, which meant only
    /// connections that had already been granted a corner were watched: a client that dropped
    /// during the handshake, or one rejected because the match was full, disappeared in complete
    /// silence. Doing it here covers every connection for the cost of one structural change each.
    ///
    /// <see cref="ServerDisconnectSystem"/> needs no matching guard — ReleaseTeamSlot and
    /// RemoveFromRoster both no-op for a connection that never held a slot.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ServerDisconnectSystem))]
    public partial struct ServerConnectionObserverSystem : ISystem
    {
        private EntityQuery _unobservedQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .WithNone<ConnectionState>();
            _unobservedQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(_unobservedQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            ecb.AddComponent<ConnectionState>(_unobservedQuery, EntityQueryCaptureMode.AtPlayback);
        }
    }
}
