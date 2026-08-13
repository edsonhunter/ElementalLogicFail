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
        private EntityQuery _alreadyReadyQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<MatchStartedTag>();

            // The gate used to be WithAll<MatchStartedTag>().WithNone<ClientSceneReady>() and was
            // commented as self-retiring. It is not: the two live on separate entities, so the
            // MatchStartedTag entity never gains ClientSceneReady and the query matches forever —
            // this system was minting a fresh ClientSceneReady entity every single frame. Nothing
            // noticed until something asked for it as a singleton and got hundreds of instances.
            _alreadyReadyQuery = state.GetEntityQuery(ComponentType.ReadOnly<ClientSceneReady>());

            _sceneQuery = state.GetEntityQuery(ComponentType.ReadOnly<SceneReference>());
        }

        public void OnUpdate(ref SystemState state)
        {
            // Deferred creation means this stays true for the rest of the frame it is recorded
            // in, and false from the next one on — so exactly one entity is ever created per
            // session. ClientDisconnectSystem clears it, which re-arms this for the next match.
            if (!_alreadyReadyQuery.IsEmpty) return;

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
