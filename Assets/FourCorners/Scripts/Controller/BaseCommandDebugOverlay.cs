using System;
using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Spawner;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace FourCorners.Scripts.Controller
{
    /// <summary>
    /// A scaffold that lets the command channel be exercised in play before there is a HUD.
    ///
    /// The real in-match HUD is Tier 1.4 and belongs in <c>FourCorners.View</c> with the rest of
    /// the UI. This exists because 1.1 ships a channel whose only observable effect is a console
    /// line, and a tier that cannot be pressed cannot be verified — the same argument that put a
    /// Leave button in the gameplay scene. Delete it when GameplayScreenUI grows real controls.
    ///
    /// Immediate-mode and asset-free on purpose, exactly like <see cref="CombatFeedbackOverlay"/>:
    /// no prefab, no serialized Button, nothing to wire in the scene and nothing to re-bake. It is
    /// attached at runtime by GameplaySceneController rather than placed in GameplayScene, so
    /// removing it later is a file deletion and not a scene edit.
    ///
    /// It splits the two directions exactly the way the bridge rule says to, and is a compact
    /// illustration of why the rule is shaped that way. Commands go out through the injected
    /// callback, because they *change* simulation state. The gold readout is pulled straight from
    /// <see cref="ClientServerBootstrap.ClientWorld"/>, because it only observes — and routing a
    /// number that changes every second through an <c>Action</c> would be exactly the per-frame
    /// bridge traffic CombatFeedbackOverlay exists to avoid.
    /// </summary>
    public class BaseCommandDebugOverlay : MonoBehaviour
    {
        private const int BarracksSlots = Buildings.BarracksPerBase;

        /// <summary>How long the last rejection stays on screen.</summary>
        private const float NoticeDuration = 4f;

        private Action<BaseCommandType, int> _send;
        private string _notice;
        private float _noticeExpiresAt;

        private EntityQuery _baseQuery;
        private EntityQuery _networkIdQuery;
        private World _cachedWorld;
        private string _levels = string.Empty;

        public void Init(Action<BaseCommandType, int> send)
        {
            _send = send;
        }

        /// <summary>
        /// This player's purse, or false while there is nothing to read — before the corner is
        /// allocated, or for a spectator who has none.
        ///
        /// Gold is a GhostField, so on a client these are the replicated values: the same numbers
        /// the server will check a purchase against.
        /// </summary>
        private bool TryGetLocalEconomy(out PlayerEconomy economy)
        {
            economy = default;

            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true }) return false;

            // Queries are per-world and rebuilding one per frame allocates; cached against the
            // world so a recreated world still gets fresh queries.
            if (_cachedWorld != world)
            {
                _cachedWorld = world;
                _baseQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerBase>(), ComponentType.ReadOnly<PlayerEconomy>());
                _networkIdQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkId>());
            }

            if (_networkIdQuery.IsEmpty) return false;
            int localId = _networkIdQuery.GetSingleton<NetworkId>().Value;

            using var baseEntities = _baseQuery.ToEntityArray(Allocator.Temp);
            using var bases = _baseQuery.ToComponentDataArray<PlayerBase>(Allocator.Temp);
            using var economies = _baseQuery.ToComponentDataArray<PlayerEconomy>(Allocator.Temp);

            for (int i = 0; i < bases.Length; i++)
            {
                if (!bases[i].IsActive) continue;
                if (bases[i].NetworkId != localId) continue;

                economy = economies[i];
                _levels = DescribeLevels(world, baseEntities[i]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renders the local corner's building levels as one line.
        ///
        /// Everything read here is replicated: BuildingData carries SendDataForChildEntity, so a
        /// client sees its barracks levels as well as its central one. The children are reached
        /// through LinkedEntityGroup — the ghost hierarchy itself — because the component that
        /// records which base owns a spawner is server-only by design and there is nothing on a
        /// client to read it from.
        ///
        /// Falls back to the central level alone if that buffer is not there, rather than
        /// pretending the barracks are at level zero.
        /// </summary>
        private string DescribeLevels(World world, Entity localBase)
        {
            var em = world.EntityManager;

            int central = em.HasComponent<BuildingData>(localBase)
                ? em.GetComponentData<BuildingData>(localBase).Level
                : 0;

            var line = new System.Text.StringBuilder();
            line.Append("Central L").Append(central);

            if (!em.HasBuffer<LinkedEntityGroup>(localBase)) return line.ToString();

            var children = em.GetBuffer<LinkedEntityGroup>(localBase, isReadOnly: true);

            for (int slot = 0; slot < BarracksSlots; slot++)
            {
                int level = -1;

                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i].Value;
                    if (!em.HasComponent<BuildingData>(child)) continue;

                    var building = em.GetComponentData<BuildingData>(child);
                    if (building.Type != BuildingType.Barracks || building.Slot != slot) continue;

                    level = building.Level;
                    break;
                }

                line.Append("  B").Append(slot).Append(level < 0 ? "?" : "L" + level);
            }

            return line.ToString();
        }

        /// <summary>
        /// Shows why the server said no. Fed by the scene controller rather than subscribed to
        /// here — FourCorners.Controller can see the service interface, but a debug overlay owning
        /// a bridge subscription would own unsubscribing from it too, and that is one more
        /// lifetime to get wrong for something temporary.
        /// </summary>
        public void ShowRejection(BaseCommandType type, BaseCommandRejection reason)
        {
            _notice = $"{type} rejected: {reason}";
            _noticeExpiresAt = Time.time + NoticeDuration;
        }

        private void OnGUI()
        {
            if (_send == null) return;

            const float width = 220f;
            const float lineHeight = 24f;
            const int lines = BarracksSlots + 5;
            var area = new Rect(12f, Screen.height - lines * lineHeight - 24f, width,
                lines * lineHeight + 12f);

            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("Base commands (debug)");

            GUILayout.Label(TryGetLocalEconomy(out var economy)
                ? $"Gold {economy.Gold}  (+{economy.IncomePerSecond}/s)"
                : "Gold —");

            if (GUILayout.Button("Upgrade central"))
                _send(BaseCommandType.UpgradeCentral, 0);

            for (int slot = 0; slot < BarracksSlots; slot++)
            {
                if (GUILayout.Button($"Upgrade barracks {slot}"))
                    _send(BaseCommandType.UpgradeBarracks, slot);
            }

            // Levels are read rather than tracked. The server owns them, they arrive as replicated
            // state, and a local guess would drift the moment a purchase was refused.
            GUILayout.Label(_levels);

            if (_notice != null && Time.time < _noticeExpiresAt)
                GUILayout.Label(_notice);

            GUILayout.EndArea();
        }
    }
}
