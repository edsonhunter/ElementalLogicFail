using System;
using System.Linq;
using System.Reflection;

namespace ElementLogicFail.Scripts.Utils
{
    public static class ReflectionExtensions
    {
        public static void AssertForbiddenMethods<T>(this T self, params string[] methods)
        {
            if (methods == null || (methods.Length == 0))
            {
                return;
            }

            var type = self.GetType();
            while (type != typeof(T))
            {
                var methodName = string.Empty;
                if(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                       .Any(method => method.DeclaringType != typeof(T) && Array.IndexOf(methods, (methodName = method.Name)) >= 0))
                {
                    throw new InvalidOperationException($"Method '{methodName}' on '{type.Name}' is not allowed.");
                }
            }
        }
    }
}