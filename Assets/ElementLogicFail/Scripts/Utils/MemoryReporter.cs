using UnityEngine;

namespace ElementLogicFail.Scripts.Utils
{
    public class MemoryReporter : MonoBehaviour
    {
        public void OnGUI()
        {
            long totalMemory = System.GC.GetTotalMemory(false);
            GUI.Label(new Rect(10, 10, 300, 20), $"Memory Used: {totalMemory / (1024 * 1024)} MB");
        }
    }
}