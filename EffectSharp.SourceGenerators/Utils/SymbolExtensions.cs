using Microsoft.CodeAnalysis;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolExtensions
    {
        public static bool HasAttribute(
            this ISymbol symbol,
            string attributeName,
            string containingNamespace = "EffectSharp.SourceGenerators")
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var className = attr.AttributeClass.Name;
                var classNamespace = attr.AttributeClass.ContainingNamespace.ToDisplayString();

                if (className == attributeName ||
                    className == attributeName + "Attribute"
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

                if (className == attributeName ||
                    className == attributeName + "Attribute"
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

        public static bool ReturnsTaskLike(this IMethodSymbol method, Compilation compilation, out INamedTypeSymbol resultType)
        {
            var returnType = method.ReturnType;

            var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

            resultType = null;

            if (taskType == null)
                return false;

            // Task
            if (SymbolEqualityComparer.Default.Equals(returnType, taskType))
                return true;

            // Task<T>
            if (returnType is INamedTypeSymbol named &&
                named.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, taskOfTType))
            {
                resultType = named.TypeArguments[0] as INamedTypeSymbol;
                return true; 
            }

            return false;
        }
    }
}
