using System;
using System.Collections.Generic;

namespace FourCorners.Scripts.Utils.DependencyInjector
{
    /// <summary>
    /// A registry of singletons keyed by their interface type.
    ///
    /// The reflection-based field injection this once carried ([Inject], DITypeInfo, Resolve())
    /// was never used — the attribute appeared zero times in the project — so every service and
    /// manager was already resolved through Get&lt;T&gt;(). The unreachable half has been removed
    /// rather than left to imply a wiring mechanism that does not exist.
    /// </summary>
    public class DIContainer
    {
        private readonly Dictionary<Type, object> _instanceMap = new();

        public DIContainer()
        {
            _instanceMap[typeof(DIContainer)] = this;
        }

        public void Register<T>(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            _instanceMap[typeof(T)] = instance;
        }

        public T Get<T>()
        {
            if (_instanceMap.TryGetValue(typeof(T), out var instance)) return (T)instance;

            // The old message reported "is not an interface" for the far more common case of a
            // type that simply was not registered.
            throw new KeyNotFoundException(
                $"The type '{typeof(T).FullName}' was not registered in the DI container. " +
                "Register it in Bootstrapper.SetupServices or SetupManagers.");
        }
    }
}
