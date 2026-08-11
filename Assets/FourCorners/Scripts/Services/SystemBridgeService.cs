using System;
using FourCorners.Scripts.Components.Bounds;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Services.Interface;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace FourCorners.Scripts.Services
{
    /// <summary>
    /// The single sanctioned channel between the managed presentation layer and ECS.
    ///
    /// Every operation targets <see cref="ClientServerBootstrap.ClientWorld"/> — the one full
    /// client world this process presents. Iterating World.All is a bug here: ThinClient and
    /// secondary worlds must not receive UI-driven signals, and acting on all of them at once
    /// is what made every client believe it owned the managed scene loader.
    /// </summary>
    public class SystemBridgeService : ISystemBridgeService
    {
        /// <inheritdoc />
        public Action<LobbyStateUpdateEvent> OnLobbyStateUpdate { get; set; }

        /// <inheritdoc />
        public Action OnMatchStarted { get; set; }

        /// <inheritdoc />
        public Action OnJoinRejected { get; set; }

        private EntityQuery _wanderAreaQuery;
        private EntityQuery _sceneLoadedQuery;
        private World _cachedWorld;

        /// <inheritdoc />
        public bool TryGetMapBounds(out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero;
            max = Vector3.zero;

            if (!TryGetQueries(out _)) return false;
            if (_wanderAreaQuery.IsEmpty) return false;

            var area = _wanderAreaQuery.GetSingleton<WanderArea>();
            min = area.MinArea;
            max = area.MaxArea;
            return true;
        }

        /// <inheritdoc />
        public void NotifyClientSceneReady()
        {
            if (!TryGetQueries(out var world))
            {
                Debug.LogWarning("[SystemBridgeService] NotifyClientSceneReady with no client world.");
                return;
            }

            // Idempotent: a lobby re-entry or scene reload must not stack duplicate tags.
            if (!_sceneLoadedQuery.IsEmpty) return;

            world.EntityManager.CreateEntity(typeof(SceneLoadedTag));
            Debug.Log("[SystemBridgeService] SceneLoadedTag injected into the presentation client world.");
        }

        /// <inheritdoc />
        public void SendStartGameRequest()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true })
            {
                Debug.LogWarning("[SystemBridgeService] SendStartGameRequest with no client world.");
                return;
            }

            var em = world.EntityManager;
            var rpc = em.CreateEntity();
            em.AddComponentData(rpc, new StartGameRequest());
            em.AddComponentData(rpc, new SendRpcCommandRequest()); // no target = server
            Debug.Log("[SystemBridgeService] SendStartGameRequest sent from the presentation client world.");
        }

        /// <inheritdoc />
        public void RegisterBridge() => ClientBridgeRegistry.Service = this;

        /// <inheritdoc />
        public void SetLocalPlayerSelection(int desiredTeamIndex, RaceType race)
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true })
            {
                Debug.LogWarning("[SystemBridgeService] SetLocalPlayerSelection with no client world.");
                return;
            }

            // ClientRequestGameSystem seeds this singleton in OnCreate, so it always exists by
            // the time any UI can call in.
            var query = world.EntityManager.CreateEntityQuery(typeof(LocalPlayerSelection));
            if (query.IsEmpty)
            {
                Debug.LogWarning("[SystemBridgeService] LocalPlayerSelection singleton missing.");
                return;
            }

            query.SetSingleton(new LocalPlayerSelection
            {
                DesiredTeamIndex = desiredTeamIndex,
                Race = race
            });

            Debug.Log($"[SystemBridgeService] Selection set: team={desiredTeamIndex}, race={race}.");
        }

        /// <summary>
        /// Resolves the presentation client world and lazily (re)builds the cached queries.
        /// Queries are per-world, so they are rebuilt if the world is ever recreated —
        /// building them per call allocated a fresh EntityQuery every frame during scene load.
        /// </summary>
        private bool TryGetQueries(out World world)
        {
            world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true }) return false;

            if (_cachedWorld != world)
            {
                _cachedWorld = world;
                _wanderAreaQuery = world.EntityManager.CreateEntityQuery(typeof(WanderArea));
                _sceneLoadedQuery = world.EntityManager.CreateEntityQuery(typeof(SceneLoadedTag));
            }

            return true;
        }
    }
}
