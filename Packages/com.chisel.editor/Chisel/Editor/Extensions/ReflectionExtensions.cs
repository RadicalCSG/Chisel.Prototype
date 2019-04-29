using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Chisel.Editors
{
    public static class ReflectionExtensions
    {
        public static Type GetGenericBaseClass(this Type self, Type genericBaseClass)
        {
            while (self != null && self != typeof(object))
            {
                var foundBaseClass = self.IsGenericType ? self.GetGenericTypeDefinition() : self;
                if (genericBaseClass == foundBaseClass)
                    return self;
                self = self.BaseType;
            }
            return null;
        }

        public static IEnumerable<Type> AllTypes
        {
            get
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        yield return type;
                    }
                }
            }
        }

        public static IEnumerable<Type> AllNonAbstractClasses
        {
            get
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsAbstract ||
                            !type.IsClass)
                            continue;

                        yield return type;
                    }
                }
            }
        }

    }
}
