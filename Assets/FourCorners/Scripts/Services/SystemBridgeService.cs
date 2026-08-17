using System;
using FourCorners.Scripts.Components.Bounds;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Services.Interface;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Scenes;
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

        /// <inheritdoc />
        public Action OnDisconnected { get; set; }

        /// <inheritdoc />
        public Action<bool> OnMatchEnded { get; set; }

        /// <inheritdoc />
        public Action<BaseCommandType, BaseCommandRejection> OnBaseCommandRejected { get; set; }

        private EntityQuery _wanderAreaQuery;
        private EntityQuery _sceneLoadedQuery;
        private EntityQuery _lobbyStateQuery;
        private EntityQuery _matchStartedQuery;
        private World _cachedWorld;

        /// <inheritdoc />
        public bool IsMatchStarted => TryGetQueries(out _) && !_matchStartedQuery.IsEmpty;

        private const string PlayerIdPrefsKey = "FourCorners.LocalPlayerId";
        private FixedString64Bytes _localPlayerId;

        /// <summary>
        /// A stable identity for this installation, so a player who drops out of a running match
        /// can be handed their corner back.
        ///
        /// Deliberately not the NetworkId — the server issues a new one on every connection, so a
        /// returning player looks like a stranger. Deliberately not the UGS PlayerId either: the
        /// direct-connect path never authenticates, and a player on a LAN with no internet must
        /// still be able to reconnect.
        ///
        /// Persisted so it survives the process, which is exactly the case that matters — a
        /// crash, or someone closing the game and coming back.
        /// </summary>
        private FixedString64Bytes LocalPlayerId
        {
            get
            {
                if (!_localPlayerId.IsEmpty) return _localPlayerId;

                var stored = PlayerPrefs.GetString(PlayerIdPrefsKey, string.Empty);
                if (string.IsNullOrEmpty(stored))
                {
                    stored = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(PlayerIdPrefsKey, stored);
                    PlayerPrefs.Save();
                    Debug.Log($"[SystemBridgeService] Minted a new local player id: {stored}.");
                }

                _localPlayerId = stored;
                return _localPlayerId;
            }
        }

        /// <inheritdoc />
        public bool TryGetLobbyState(out LobbyStateUpdateEvent state)
        {
            state = default;

            if (!TryGetQueries(out _)) return false;
            if (_lobbyStateQuery.IsEmpty) return false;

            var snapshot = _lobbyStateQuery.GetSingleton<LobbyStateSnapshot>();

            // Version 0 means the server has not sent a lobby update yet, so IsHost/PlayerCount
            // are not meaningful — do not let the UI render defaults as if they were real.
            if (snapshot.Version == 0) return false;

            state = new LobbyStateUpdateEvent
            {
                IsHost = snapshot.IsHost,
                PlayerCount = snapshot.PlayerCount
            };
            return true;
        }

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
        public void UnloadEntityScenes()
        {
            UnloadEntityScenes(ClientServerBootstrap.ClientWorld);
            UnloadEntityScenes(ClientServerBootstrap.ServerWorld);
        }

        /// <summary>
        /// Unloads every entity scene in one world, rather than one match by GUID the way
        /// SubScene.OnDisable does. GameplaySubscene is the only entity scene in the project, and
        /// it is meaningful only while GameplayScene is loaded, so "all of them" is the honest
        /// scope — and it is the only formulation that clears a duplicate if one ever appears.
        /// </summary>
        private static void UnloadEntityScenes(World world)
        {
            if (world is not { IsCreated: true }) return;

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SceneReference>());
            using var sceneEntities = query.ToEntityArray(Allocator.Temp);
            if (sceneEntities.Length == 0) return;

            foreach (var sceneEntity in sceneEntities)
            {
                SceneSystem.UnloadScene(
                    world.Unmanaged, sceneEntity, SceneSystem.UnloadParameters.DestroyMetaEntities);
            }

            Debug.Log($"[SystemBridgeService] Unloaded {sceneEntities.Length} entity scene(s) from '{world.Name}'.");
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
        public void SendBaseCommand(BaseCommandType type, int targetSlot = 0)
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true })
            {
                Debug.LogWarning("[SystemBridgeService] SendBaseCommand with no client world.");
                return;
            }

            if (type == BaseCommandType.None)
            {
                // Caught here rather than shipped: the server rejects it anyway, and a round trip
                // is a slow way to learn that a caller left the argument at its default.
                Debug.LogWarning("[SystemBridgeService] SendBaseCommand ignored: command type is None.");
                return;
            }

            if (targetSlot is < 0 or > byte.MaxValue)
            {
                Debug.LogWarning(
                    $"[SystemBridgeService] SendBaseCommand ignored: slot {targetSlot} does not fit " +
                    "the wire format.");
                return;
            }

            var em = world.EntityManager;
            var rpc = em.CreateEntity();
            em.AddComponentData(rpc, new BaseCommandRequest
            {
                Type = type,
                TargetSlot = (byte)targetSlot
            });
            em.AddComponentData(rpc, new SendRpcCommandRequest()); // no target = server

            Debug.Log($"[SystemBridgeService] SendBaseCommand {type} slot={targetSlot}.");
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
                Race = race,
                PlayerId = LocalPlayerId
            });

            Debug.Log($"[SystemBridgeService] Selection set: team={desiredTeamIndex}, race={race}, id={LocalPlayerId}.");
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
                _lobbyStateQuery = world.EntityManager.CreateEntityQuery(typeof(LobbyStateSnapshot));
                _matchStartedQuery = world.EntityManager.CreateEntityQuery(typeof(MatchStartedTag));
            }

            return true;
        }
    }
}
