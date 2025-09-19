using ElementLogicFail.Scripts.Services.Interface;

namespace ElementLogicFail.Scripts.Manager.Interface
{
    public interface IApplication
    {
        public T GetService<T>() where T : IService;
        public T GetManager<T>() where T : IManager;
    }
}