using System;
using UnityEngine;
using UnityEngine.UI;

namespace FourCorners.Scripts.View
{
    /// <summary>
    /// The in-match HUD. Currently just the Leave button.
    ///
    /// Wire-up (from GameplaySceneController.Loaded):
    ///   - leaveButton.onClick → fires OnLeaveClicked (disconnect + loads MainMenu).
    ///
    /// The callback is injected rather than resolved here because FourCorners.View.asmdef does
    /// not reference the service or manager assemblies — a view physically cannot reach
    /// IMultiplayerService or ISceneManager. Same shape as <see cref="LobbyScreenUI"/>.
    /// </summary>
    public class GameplayScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button leaveButton;

        private Action _onLeave;

        public void Init(Action onLeave)
        {
            _onLeave = onLeave;
            leaveButton.onClick.AddListener(OnLeaveClicked);
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
