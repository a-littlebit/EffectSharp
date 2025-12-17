using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolExtensions
    {
        public static bool IsAssignableTo(
            this ITypeSymbol type,
            INamedTypeSymbol targetType)
        {
            // Walk the inheritance chain
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, targetType))
                {
                    return true;
                }
                if (targetType.TypeKind == TypeKind.Interface && current is INamedTypeSymbol named)
                {
                    // Check implemented interfaces
                    foreach (var iface in named.AllInterfaces)
                    {
                        if (SymbolEqualityComparer.Default.Equals(iface, targetType))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool TryGetGenericArgument(
            this ITypeSymbol type,
            INamedTypeSymbol genericDefinition,
            int argumentIndex,
            out INamedTypeSymbol typeArgument)
        {
            typeArgument = null;

            // Walk the inheritance chain
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current is INamedTypeSymbol named)
                {
                    // Check self constructed from generic interface (rare, for interface types)
                    if (named.IsGenericType &&
                        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, genericDefinition) &&
                        named.TypeArguments.Length > argumentIndex)
                    {
                        typeArgument = named.TypeArguments[argumentIndex] as INamedTypeSymbol;
                        return typeArgument != null;
                    }

                    // Check implemented interfaces
                    if (genericDefinition.TypeKind == TypeKind.Interface)
                    {
                        foreach (var iface in named.AllInterfaces)
                        {
                            if (iface.IsGenericType &&
                                SymbolEqualityComparer.Default.Equals(iface.ConstructedFrom, genericDefinition) &&
                                iface.TypeArguments.Length > argumentIndex)
                            {
                                typeArgument = iface.TypeArguments[argumentIndex] as INamedTypeSymbol;
                                return typeArgument != null;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasAttribute(
            this ISymbol symbol,
            string attributeName,
            string containingNamespace = "EffectSharp.SourceGenerators")
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var className = attr.AttributeClass.Name;
                var classNamespace = attr.AttributeClass.ContainingNamespace.ToDisplayString();

                if ((className == attributeName ||
                    className == attributeName + "Attribute")
                    && classNamespace == containingNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        public static AttributeData GetAttributeData(
            this ISymbol symbol,
            string attributeName,
            string containingNamespace = "EffectSharp.SourceGenerators")
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var className = attr.AttributeClass.Name;
                var classNamespace = attr.AttributeClass.ContainingNamespace.ToDisplayString();

                if ((className == attributeName ||
                    className == attributeName + "Attribute")
                    && classNamespace == containingNamespace)
                {
                    return attr;
                }
            }
            return null;
        }

        public static T GetNamedArgument<T>(
            this AttributeData attributeData,
            string argumentName,
            T defaultValue = default)
        {
            var namedArg = attributeData.NamedArguments
                .FirstOrDefault(kv => kv.Key == argumentName);
            if (namedArg.Value.Value is T value)
            {
                return value;
            }
            return defaultValue;
        }

        public static List<T> GetNamedArgumentList<T>(
            this AttributeData attributeData,
            string argumentName)
        {
            var result = new List<T>();
            var namedArg = attributeData.NamedArguments
                .FirstOrDefault(kv => kv.Key == argumentName);
            if (namedArg.Value.Values != null)
            {
                foreach (var typedConstant in namedArg.Value.Values)
                {
                    if (typedConstant.Value is T value)
                    {
                        result.Add(value);
                    }
                }
            }
            return result;
        }

        public static bool ReturnsTaskLike(this IMethodSymbol method, Compilation compilation, out INamedTypeSymbol resultType)
        {
            var returnType = method.ReturnType;

            var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

            resultType = null;

            if (taskType == null)
                return false;

            // Task<T>
            if (returnType is INamedTypeSymbol named &&
                named.TryGetGenericArgument(taskOfTType, 0, out resultType))
            {
                return true;
            }

            // Task
            if (returnType.IsAssignableTo(taskType))
                return true;

            return false;
        }
    }
}
