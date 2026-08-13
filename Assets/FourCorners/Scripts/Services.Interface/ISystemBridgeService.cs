using System;
using FourCorners.Scripts.Components.Team;
using UnityEngine;

namespace FourCorners.Scripts.Services.Interface
{
    public interface ISystemBridgeService : IService
    {
        /// <summary>
        /// Records the player's lobby choices before connecting.
        /// <paramref name="desiredTeamIndex"/> is a preference — the server grants another
        /// corner if that one is taken. The race is always honoured.
        /// </summary>
        void SetLocalPlayerSelection(int desiredTeamIndex, RaceType race);

        /// <summary>
        /// Reads the baked WanderArea singleton from the presentation client world.
        /// Returns false while the SubScene is still streaming — callers must poll on the
        /// return value, never on the bounds themselves (a map centred on the origin is
        /// indistinguishable from "not loaded yet").
        /// </summary>
        bool TryGetMapBounds(out Vector3 min, out Vector3 max);

        /// <summary>
        /// Signals from the managed scene layer that GameplayScene finished loading and its
        /// map bounds are readable. Idempotent. Consumed by ClientSceneReadySystem.
        /// </summary>
        void NotifyClientSceneReady();

        /// <summary>
        /// Unloads every entity scene from the client and server worlds.
        ///
        /// SubScene's own teardown unloads only the *first* scene entity matching its GUID, so a
        /// copy that ever got loaded twice can never be recovered from: every later GetSingleton
        /// against baked data (RaceCatalog, WanderArea) throws "there are 2 entities". This is
        /// the sweep that clears the remainder.
        ///
        /// Call it only when no match is in progress. Unloading a subscene that holds prespawned
        /// ghosts while a connection is still in-game makes the ghost collection reference a
        /// prefab that no longer exists ("PrespawnSceneList ... ENTITY_NOT_FOUND").
        /// </summary>
        void UnloadEntityScenes();

        /// <summary>
        /// Fired when the server's lobby state changes.
        /// UI subscribes to update player count and show/hide the Start button.
        /// </summary>
        Action<LobbyStateUpdateEvent> OnLobbyStateUpdate { get; set; }

        /// <summary>
        /// Current lobby state, or false if the server has not sent one yet.
        ///
        /// OnLobbyStateUpdate is edge-triggered, and the first update arrives while the client
        /// is still on the connection screen — so a subscriber attaching later (the lobby scene)
        /// misses it entirely. Late subscribers must pull once after subscribing.
        /// </summary>
        bool TryGetLobbyState(out LobbyStateUpdateEvent state);

        /// <summary>
        /// Fired when the server broadcasts MatchStartedRpc.
        /// Every client in the lobby subscribes to this to transition → GameplayScene.
        /// </summary>
        Action OnMatchStarted { get; set; }

        /// <summary>
        /// True once this client knows the match is running.
        ///
        /// OnMatchStarted has the same edge-triggered problem as OnLobbyStateUpdate, and it is
        /// worse for a player joining a match that already started: the server answers their join
        /// with MatchStartedRpc immediately, so the event fires while they are still on the
        /// connection screen. The lobby must pull this after subscribing or it will wait forever.
        /// </summary>
        bool IsMatchStarted { get; }

        /// <summary>
        /// Fired when this client loses the server — it left, was dropped, or the host ended the
        /// match. Scenes that only make sense while connected subscribe to bail out to the menu.
        /// </summary>
        Action OnDisconnected { get; set; }

        /// <summary>
        /// Fired when the server refuses the join because every corner is occupied.
        /// UI subscribes to tell the player instead of leaving them on a silent screen.
        /// </summary>
        Action OnJoinRejected { get; set; }

        /// <summary>
        /// Called by the Host's Start button. Creates a StartGameRequest RPC entity
        /// inside the presentation client world and sends it to the server.
        /// </summary>
        void SendStartGameRequest();

        /// <summary>
        /// Publishes this service to <see cref="ClientBridgeRegistry"/> so the client world's
        /// BridgeNotificationSystem can reach it.
        /// </summary>
        void RegisterBridge();
    }
}
