using ElementLogicFail.Scripts.Components.Element;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ElementLogicFail.Scripts.Controller
{
    public class SpawnerButtonController : MonoBehaviour
    {
        public string name;
        public ElementType elementType;
        public Button increaseButton;
        public Button decreaseButton;
        public TextMeshProUGUI spawnRateText;

        private float _currentSpawnRate = 1f;
        private const float RateChangeAmount = 0.5f;

        public void Initialize()
        {
            UpdateText();
        }

        public float IncreaseRate()
        {
            _currentSpawnRate += RateChangeAmount;
            UpdateText();
            return _currentSpawnRate;
        }

        public float DecreaseRate()
        {
            _currentSpawnRate = Mathf.Max(0.5f, _currentSpawnRate - RateChangeAmount);
            UpdateText();
            return _currentSpawnRate;
        }

        private void UpdateText()
        {
            if (spawnRateText != null)
            {
                spawnRateText.text = $"{elementType} Rate: {_currentSpawnRate:F1}x";
            }
        }
    }
}