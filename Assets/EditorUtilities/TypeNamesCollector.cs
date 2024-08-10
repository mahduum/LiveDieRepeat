using System;
using System.Linq;

namespace EditorUtilities
{
    public static class TypeNamesCollector
    {
        public static string[] GetInterfaceImplementors(Type interfaceType, bool valueTypes = true, bool referenceTypes = true, bool allowAbstract = false)
        {
            var allAssembliesTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .ToArray();

            var implementingTypes = allAssembliesTypes
                .Where(t => interfaceType.IsAssignableFrom(t) &&
                            (t.IsValueType == valueTypes || t.IsByRef == referenceTypes)
                            && (!t.IsAbstract || allowAbstract ))
                .ToArray();

            var implementingTypeNames = implementingTypes.Select(t => t.FullName).ToArray();

            return implementingTypeNames;
        }

        public static string[] GetDerivedTypeNames(Type baseType, bool allowAbstract = false)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsAssignableFrom(baseType)
                            && t != baseType
                            && (!t.IsAbstract || allowAbstract))
                .Select(s => s.FullName)
                .ToArray();
        }
        
        //todo create example component class that will display tag property (property drawer)
        //todo create example component that will manage displaying and setting class type property (editor)
    }
}