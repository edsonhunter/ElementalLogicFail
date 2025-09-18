using System;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Services.Interface;
using ElementLogicFail.Scripts.Utils;
using ElementLogicFail.Scripts.Utils.DependencyInjector;
using UnityEngine;

namespace ElementLogicFail.Scripts.Manager
{
    public class ApplicationManager : MonoBehaviour, IApplication
    {
        private DIContainer Services;
        private DIContainer Managers;
        
        public T GetService<T>() where T : IService
        {
            return Services.Get<T>();
        }

        public T GetManager<T>() where T : IManager
        {
            return Managers.Get<T>();
        }

        private void RegisterServices<T>(IService service) where T : IService
        {
            if(!(service is T))
            {
                throw new InvalidOperationException($"Cannot register service {service.GetType().FullName} into the interface {typeof(T).FullName}");
            }
            
            Services.Register(service);
        }

        private void RegisterManager<T>(IManager manager) where T : IManager
        {
            if (!(manager is T))
            {
                throw new InvalidOperationException($"Cannot register manager {manager.GetType().FullName} into the interface {typeof(T).FullName}");
            }
            
            Managers.Register(manager);
        }

        private void Awake()
        {
            Services = new DIContainer();
            Managers = new DIContainer();
        }
    }
}