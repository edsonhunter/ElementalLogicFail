using System;
using FourCorners.Scripts.Components.Command;
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
    /// It sends through the injected callback and never touches a World. Reading the client world
    /// directly is sanctioned for observers (CombatFeedbackOverlay does exactly that), but this
    /// *changes* simulation state, and everything that changes simulation state goes through the
    /// bridge.
    /// </summary>
    public class BaseCommandDebugOverlay : MonoBehaviour
    {
        private const int BarracksSlots = 3;

        /// <summary>How long the last rejection stays on screen.</summary>
        private const float NoticeDuration = 4f;

        private Action<BaseCommandType, int> _send;
        private string _notice;
        private float _noticeExpiresAt;

        public void Init(Action<BaseCommandType, int> send)
        {
            _send = send;
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
            var area = new Rect(12f, Screen.height - (BarracksSlots + 3) * lineHeight - 24f, width,
                (BarracksSlots + 3) * lineHeight + 12f);

            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("Base commands (debug)");

            if (GUILayout.Button("Upgrade central"))
                _send(BaseCommandType.UpgradeCentral, 0);

            for (int slot = 0; slot < BarracksSlots; slot++)
            {
                if (GUILayout.Button($"Upgrade barracks {slot}"))
                    _send(BaseCommandType.UpgradeBarracks, slot);
            }

            if (_notice != null && Time.time < _noticeExpiresAt)
                GUILayout.Label(_notice);

            GUILayout.EndArea();
        }
    }
}
