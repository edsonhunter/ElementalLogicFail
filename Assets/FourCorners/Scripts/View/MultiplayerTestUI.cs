using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FourCorners.Scripts.View
{
    public class MultiplayerTestUI : MonoBehaviour
    {
        public TMP_InputField JoinCodeInput;
        public TextMeshProUGUI StatusText;
        public Button HostButton;
        public Button JoinButton;
        private Func<int, Task<string>> _hostRelayGameAsync;
        private Func<string, Task<bool>> _joinRelayGameAsync;
        

        public async void Init(Task authenticateAsync, Func<int, Task<string>> hostRelayGameAsync, Func<string, Task<bool>> joinRelayGameAsync)
        {
            try
            {
                _hostRelayGameAsync = hostRelayGameAsync;
                _joinRelayGameAsync = joinRelayGameAsync;
            
                StatusText.text = "Authenticating...";
                await authenticateAsync;
                StatusText.text = "Authenticated. Ready to Host or Join.";
            
                HostButton.onClick.AddListener(HostGame);
                JoinButton.onClick.AddListener(JoinGame);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async void HostGame()
        {
            try
            {
                StatusText.text = "Hosting...";
                string code = await _hostRelayGameAsync(4);
                StatusText.text = $"Hosted! Join Code: {code}";
                Debug.Log($"Join Code: {code}");
                GUIUtility.systemCopyBuffer = code;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async void JoinGame()
        {
            try
            {
                if (string.IsNullOrEmpty(JoinCodeInput.text))
                {
                    StatusText.text = "Enter a Join Code first!";
                    return;
                }

                StatusText.text = "Joining...";
                await _joinRelayGameAsync(JoinCodeInput.text);
                StatusText.text = "Joined!";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
