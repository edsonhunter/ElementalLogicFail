using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FourCorners.Scripts.View
{
    /// <summary>
    /// The in-match HUD: a Leave button, and the banner that announces the result.
    ///
    /// Wire-up (from GameplaySceneController.Loaded):
    ///   - leaveButton.onClick → fires OnLeaveClicked (disconnect + loads MainMenu).
    ///   - ShowMatchResult is called when the server declares a winner.
    ///
    /// The callback is injected rather than resolved here because FourCorners.View.asmdef does
    /// not reference the service or manager assemblies — a view physically cannot reach
    /// IMultiplayerService or ISceneManager. Same shape as <see cref="LobbyScreenUI"/>.
    /// </summary>
    public class GameplayScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI matchResultLabel;

        private Action _onLeave;

        public void Init(Action onLeave)
        {
            _onLeave = onLeave;
            leaveButton.onClick.AddListener(OnLeaveClicked);

            if (matchResultLabel != null)
                matchResultLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The match is decided. The player is not kicked out — they stay connected watching the
        /// map until they choose to leave, so this only changes what the HUD says.
        /// </summary>
        public void ShowMatchResult(bool localPlayerWon)
        {
            if (matchResultLabel == null) return;

            matchResultLabel.gameObject.SetActive(true);
            matchResultLabel.text = localPlayerWon ? "VICTORY" : "DEFEAT";
        }

        private void OnLeaveClicked()
        {
            // Disconnecting takes a frame to reach the server and the scene swap is async, so
            // block the button rather than let an impatient second click stack a second load.
            leaveButton.interactable = false;
            _onLeave?.Invoke();
        }

        private void OnDestroy()
        {
            leaveButton.onClick.RemoveAllListeners();
        }
    }
}
