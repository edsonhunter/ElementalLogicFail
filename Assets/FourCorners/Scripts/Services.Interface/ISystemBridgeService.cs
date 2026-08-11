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
        /// Fired when the server's lobby state changes.
        /// UI subscribes to update player count and show/hide the Start button.
        /// </summary>
        Action<LobbyStateUpdateEvent> OnLobbyStateUpdate { get; set; }

        /// <summary>
        /// Fired when the server broadcasts MatchStartedRpc.
        /// Every client in the lobby subscribes to this to transition → GameplayScene.
        /// </summary>
        Action OnMatchStarted { get; set; }

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
