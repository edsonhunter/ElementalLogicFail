using System;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Services.Interface;
using ElementLogicFail.Scripts.Utils.DependencyInjector;
using UnityEngine;

namespace ElementLogicFail.Scripts.Manager
{
    public class ApplicationManager : MonoBehaviour, IApplication
    {
        private DIContainer _services;
        private DIContainer _managers;
        
        public T GetService<T>() where T : IService
        {
            return _services.Get<T>();
        }

        public T GetManager<T>() where T : IManager
        {
            return _managers.Get<T>();
        }

        public void RegisterServices<T>(IService service) where T : IService
        {
            if(!(service is T))
            {
                throw new InvalidOperationException($"Cannot register service {service.GetType().FullName} into the interface {typeof(T).FullName}");
            }

            _services.Register((T)service);
        }

        public void RegisterManager<T>(IManager manager) where T : IManager
        {
            if (!(manager is T))
            {
                throw new InvalidOperationException($"Cannot register manager {manager.GetType().FullName} into the interface {typeof(T).FullName}");
            }

            _managers.Register((T)manager);
        }

        private void Awake()
        {
            name = nameof(ApplicationManager);
            _services = new DIContainer();
            _managers = new DIContainer();
            DontDestroyOnLoad(this);
        }

        public void StartGame()
        {
            _managers.Resolve();
            _services.Resolve();
        }
    }
}