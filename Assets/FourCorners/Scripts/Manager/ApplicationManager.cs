using System;
using FourCorners.Scripts.Manager.Interface;
using FourCorners.Scripts.Services.Interface;
using FourCorners.Scripts.Utils.DependencyInjector;
using UnityEngine;

namespace FourCorners.Scripts.Manager
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
            _services.Register((T)service);
        }

        public void RegisterManager<T>(IManager manager) where T : IManager
        {
            _managers.Register((T)manager);
        }

        private void Awake()
        {
            name = nameof(ApplicationManager);
            _services = new DIContainer();
            _managers = new DIContainer();
            DontDestroyOnLoad(this);
        }
    }
}
