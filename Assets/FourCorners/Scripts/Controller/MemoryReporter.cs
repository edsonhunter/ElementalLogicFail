using System.Text;
using FourCorners.Scripts.Components.Minion;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;

namespace FourCorners.Scripts.Controller
{
    public class MemoryReporter : MonoBehaviour
    {
        public TextMeshProUGUI displayText;
        private GUIStyle _style;
        private readonly StringBuilder _stringBuilder = new StringBuilder(512);

        private EntityManager _entityManager;
        private EntityQuery _minionsQuery;

        private int _totalEntityCount;
        private int _minionCount;
        private float _deltaTime;

        private ProfilerRecorder _totalReservedMemoryRecorder;
        private ProfilerRecorder _gcReservedMemoryRecorder;

        private World _cachedWorld;

        void Start()
        {
            _cachedWorld = World.DefaultGameObjectInjectionWorld;
            if (_cachedWorld != null && _cachedWorld.IsCreated)
            {
                _entityManager = _cachedWorld.EntityManager;
                CreateQueries();
            }

            _style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
        }

        private void CreateQueries()
        {
            // One query. There used to be two identical ones (typeof(T) and
            // ComponentType.ReadOnly<T>() match the same archetypes), differenced to produce a
            // "Pooled Minions" figure that has read exactly 0 since pooling was removed.
            _minionsQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<MinionData>());
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
            if (_cachedWorld == null || !_cachedWorld.IsCreated || _cachedWorld != World.DefaultGameObjectInjectionWorld)
            {
                _cachedWorld = World.DefaultGameObjectInjectionWorld;
                if (_cachedWorld != null && _cachedWorld.IsCreated)
                {
                    _entityManager = _cachedWorld.EntityManager;
                    CreateQueries();
                }
                else return; // Wait for world to be ready
            }

            // Using EntityManager.Debug.EntityCount gets the true count without allocating a NativeArray like GetAllEntities does!
#if UNITY_EDITOR
            _totalEntityCount = _entityManager.Debug.EntityCount;
#endif
            _minionCount = _minionsQuery.CalculateEntityCount();

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
            _stringBuilder.AppendLine($"Minions: {_minionCount}");
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
