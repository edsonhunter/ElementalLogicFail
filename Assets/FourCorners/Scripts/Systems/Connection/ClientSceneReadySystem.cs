using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Scenes;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// THE SOLE PRODUCER of <see cref="ClientSceneReady"/>. Do not add a second path.
    ///
    /// Presentation client (has <see cref="PresentationClientTag"/>):
    ///   its SubScenes are streamed by the managed scene loader, so it waits for both
    ///   SceneLoadedTag (from GameplaySceneController) and SceneSystem.IsSceneLoaded.
    ///
    /// Secondary full clients and ThinClients:
    ///   nothing drives their SubScene pipeline, so they will never produce SceneReference
    ///   entities. Blocking on them deadlocks the handshake permanently — MatchStartedTag is
    ///   their readiness signal.
    ///
    /// Not [BurstCompile]d: SceneSystem.IsSceneLoaded reaches into the managed scene-streaming
    /// state. It runs a handful of times per session, so the cost is irrelevant.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ClientSceneReadySystem : ISystem
    {
        private EntityQuery _sceneQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            // Runs only between "match started" and "ready" — self-retiring, no manual disable.
            var gate = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStartedTag>()
                .WithNone<ClientSceneReady>();
            state.RequireForUpdate(state.GetEntityQuery(gate));

            _sceneQuery = state.GetEntityQuery(ComponentType.ReadOnly<SceneReference>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<PresentationClientTag>() && !IsPresentationSceneReady(ref state))
                return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            ecb.AddComponent<ClientSceneReady>(ecb.CreateEntity());

            UnityEngine.Debug.Log(
                $"[ClientSceneReadySystem] World '{state.WorldUnmanaged.Name}' is scene-ready.");
        }

        private bool IsPresentationSceneReady(ref SystemState state)
        {
            // The managed scene layer confirms GameplayScene loaded and its bounds are readable.
            if (!SystemAPI.HasSingleton<SceneLoadedTag>()) return false;

            using var sceneEntities = _sceneQuery.ToEntityArray(Allocator.Temp);
            if (sceneEntities.Length == 0) return false;

            foreach (var sceneEntity in sceneEntities)
            {
                if (!SceneSystem.IsSceneLoaded(state.WorldUnmanaged, sceneEntity)) return false;
            }

            return true;
        }
    }
}
