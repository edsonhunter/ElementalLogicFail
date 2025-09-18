using System;
using System.Collections.Generic;
using System.Reflection;
using ElementLogicFail.Scripts.Utils.DependencyInjector;

namespace ElementLogicFail.Scripts.Utils
{
    public class DIContainer
    {
        private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Dictionary<Type, object> instanceMap = new Dictionary<Type, object>();

        public DIContainer()
        {
            instanceMap[typeof(DIContainer)] = this;
        }

        public void Register<T>(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            instanceMap[typeof(T)] = instance;
        }

        public void Resolve()
        {
            foreach (var instance in instanceMap.Values)
            {
                Inject(instance);
            }
        }

        public T Get<T>()
        {
            try
            {
                return (T)instanceMap[typeof(T)];
            }
            catch (KeyNotFoundException e)
            {
                if (typeof(T).IsInterface)
                {
                    throw new KeyNotFoundException(
                        $"The type '{typeof(T).FullName}' was not registered in the DI container.");
                }

                throw new KeyNotFoundException($"The type '{typeof(T).FullName}' is not an interface.");
            }
        }

        private void Inject(object instance)
        {
            Type type = instance.GetType();
            DITypeInfo typeInfo = DITypeInfo.Get(type, InstanceBindingFlags);
            for (int injectindex = 0; injectindex < typeInfo.InjectableFields.Length; injectindex++)
            {
                FieldInfo fieldInfo = typeInfo.InjectableFields[injectindex];
                object value = null;
                if (instanceMap.TryGetValue(fieldInfo.FieldType, out value))
                {
                    fieldInfo.SetValue(instance, value);
                }
            }
        }
    }
}