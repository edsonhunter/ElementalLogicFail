using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Consumes the server's MatchStartedRpc and raises <see cref="MatchStartedTag"/>.
    ///
    /// It makes no distinction between worlds. The presentation client's
    /// BridgeNotificationSystem turns the tag into the managed scene transition; every other
    /// client world's ClientSceneReadySystem treats it as an immediate readiness signal.
    /// Keeping that decision out of here is what allows this system to be Burst-compiled.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientMatchStartedSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStartedRpc, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            bool alreadyStarted = SystemAPI.HasSingleton<MatchStartedTag>();

            foreach (var (_, reqEntity) in
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                         .WithAll<MatchStartedRpc>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(reqEntity);

                if (alreadyStarted) continue;
                alreadyStarted = true;

                ecb.AddComponent<MatchStartedTag>(ecb.CreateEntity());
                UnityEngine.Debug.Log((FixedString128Bytes)"[ClientMatchStartedSystem] MatchStartedRpc received.");
            }
        }
    }
}
