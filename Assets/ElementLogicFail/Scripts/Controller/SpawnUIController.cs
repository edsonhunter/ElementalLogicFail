using System.Collections.Generic;
using ElementLogicFail.Scripts.Components.Element;
using ElementLogicFail.Scripts.Components.Request;
using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Controller
{
    public class SpawnUIController : MonoBehaviour
    {
        [field: SerializeField] public List<SpawnerButtonController> spawnerControls;

        private EntityManager _entityManager;
        private Entity _settingsEntity;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _settingsEntity = _entityManager.CreateEntity();
            _entityManager.AddBuffer<SpawnerRateChangeRequest>(_settingsEntity);

            foreach (var control in spawnerControls)
            {
                var currentControl = control;
                currentControl.Initialize();

                currentControl.increaseButton.onClick.AddListener(() =>
                {
                    float newRate = currentControl.IncreaseRate();
                    AddRateChangeRequest(currentControl.elementType, newRate);
                });
                currentControl.decreaseButton.onClick.AddListener(() =>
                {
                    float newRate = currentControl.DecreaseRate();
                    AddRateChangeRequest(currentControl.elementType, newRate);
                });
            }
        }

        private void AddRateChangeRequest(ElementType type, float rate)
        {
            if (!_entityManager.Exists(_settingsEntity))
            {
                return;
            }

            var buffer = _entityManager.GetBuffer<SpawnerRateChangeRequest>(_settingsEntity);
            buffer.Add(new SpawnerRateChangeRequest { Type = type, NewRate = rate });
        }
    }
}