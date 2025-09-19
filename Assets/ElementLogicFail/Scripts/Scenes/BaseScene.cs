using System;
using System.Threading.Tasks;
using ElementLogicFail.Scripts.Manager.Interface;
using ElementLogicFail.Scripts.Scenes.Interface;
using ElementLogicFail.Scripts.Services.Interface;
using UnityEngine;

namespace ElementLogicFail.Scripts.Scenes
{
    public abstract class BaseScene<T> : BaseScene where T : class, ISceneData
    {
        public new T SceneData => (T) base.SceneData;
    }
    
    public abstract class BaseScene : MonoBehaviour, IBaseScene
    {
        public bool IsActiveScene { get; private set; }
        
        internal ISceneData SceneData;
        internal IApplication Application;
        
        protected virtual async Task Loading() { await Task.Yield(); }
        protected virtual void Loaded() { }
        protected virtual void Loop() { }
        protected virtual void Unload() { }
        
        public async Task FireLoading()
        {
            await Loading();
        }
        
        public void FireLoaded()
        {
            Loaded();
        }

        public void FireLoop()
        {
            Loop();
        }

        public void FireUnload()
        {
            Unload();
        }

        public void Init(IApplication application, ISceneData data)
        {
            Application = application;
            SceneData = data;
        }
        
        public void SetActiveScene(bool activeScene)
        {
            IsActiveScene = activeScene;
        }

        protected T GetService<T>() where T : IService
        {
            return Application.GetService<T>();
        }

        protected T GetManager<T>() where T : IManager
        {
            return Application.GetManager<T>();
        }
    }
}