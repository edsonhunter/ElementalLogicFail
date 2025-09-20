

using System.Text;
using ElementLogicFail.Scripts.Components.Element;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace ElementLogicFail.Scripts.Controller
{
    public class MemoryReporter : MonoBehaviour
    {
        public TextMeshProUGUI displayText;
        private GUIStyle _style;
        private readonly StringBuilder _stringBuilder = new StringBuilder(512);

        private EntityManager _entityManager;
        private EntityQuery _activeElementsQuery;
        private EntityQuery _totalElementsQuery;

        private int _totalEntityCount;
        private int _activeElementCount;
        private int _pooledElementCount;
        private float _deltaTime;

        private ProfilerRecorder _totalReservedMemoryRecorder;
        private ProfilerRecorder _gcReservedMemoryRecorder;

        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            _totalElementsQuery = _entityManager.CreateEntityQuery(typeof(ElementData));
            _activeElementsQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ElementData>(),
                ComponentType.Exclude<Disabled>() 
            );
            
            _style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
        }

        void OnEnable()
        {
            _totalReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _gcReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        }

        void OnDisable()
        {
            _totalReservedMemoryRecorder.Dispose();
            _gcReservedMemoryRecorder.Dispose();
        }

        void Update()
        {
            _totalEntityCount = _entityManager.GetAllEntities(Allocator.Temp).Length;
            int totalElementCount = _totalElementsQuery.CalculateEntityCount();
            _activeElementCount = _activeElementsQuery.CalculateEntityCount();
            _pooledElementCount = totalElementCount - _activeElementCount;

            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        void OnGUI()
        {
            _stringBuilder.Clear();
            _stringBuilder.AppendLine("--- Performance Stats ---");
            
            float fps = 1.0f / _deltaTime;
            _stringBuilder.AppendLine($"FPS: {fps:0.}");
            _stringBuilder.AppendLine("--- Entities ---");
            
            _stringBuilder.AppendLine($"Total Entities: {_totalEntityCount}");
            _stringBuilder.AppendLine($"Active Elves: {_activeElementCount}");
            _stringBuilder.AppendLine($"Pooled Elves: {_pooledElementCount}");
            _stringBuilder.AppendLine("--- Memory ---");

            long totalReservedMB = _totalReservedMemoryRecorder.LastValue / (1024 * 1024);
            long gcReservedMB = _gcReservedMemoryRecorder.LastValue / (1024 * 1024);
            _stringBuilder.AppendLine($"Total Reserved: {totalReservedMB} MB");
            _stringBuilder.AppendLine($"GC Reserved: {gcReservedMB} MB");

            string statsText = _stringBuilder.ToString();
  
            if (displayText != null)
            {
                displayText.text = statsText;
            }
        }
    }
}