using System;
using System.Collections.Generic;
using System.Reflection;

namespace ElementLogicFail.Scripts.Utils.DependencyInjector
{
    public class DITypeInfo
    {
        internal readonly FieldInfo[] InjectableFields;
        private static readonly Dictionary<Type, DITypeInfo> cache = new Dictionary<Type, DITypeInfo>();
        
        private DITypeInfo(FieldInfo[] injectableFields)
        {
            InjectableFields = injectableFields;
        }
        
        internal static DITypeInfo Get(Type type, BindingFlags flags)
        {
            DITypeInfo typeInfo = null;
            if (!cache.TryGetValue(type, out typeInfo))
            {
                typeInfo = Create(type, flags);
                cache[type] = typeInfo;
            }
            return typeInfo;
        }

        private static DITypeInfo Create(Type type, BindingFlags flags)
        {
            List<FieldInfo> injectableFields = new List<FieldInfo>();
            Type currentType = type;
            do
            {
                List<FieldInfo> fields = GetInjectableFields(currentType, flags);
                injectableFields.AddRange(fields);
                currentType = currentType?.BaseType;
            }while(currentType!=null && currentType != typeof(object));
            DITypeInfo typeInfo = new DITypeInfo(injectableFields.ToArray());
            return typeInfo;
        }

        private static List<FieldInfo> GetInjectableFields(Type type, BindingFlags flags)
        {
            FieldInfo[] nonPublicFields = type.GetFields(flags);
            List<FieldInfo> injectableFields = new List<FieldInfo>();
            foreach (FieldInfo field in nonPublicFields)
            {
                InjectAttribute attribute = field.GetCustomAttribute<InjectAttribute>();
                if (attribute != null)
                {
                    injectableFields.Add(field);
                }
            }
            return injectableFields;
        }
    }
}