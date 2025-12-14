using Microsoft.CodeAnalysis;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolExtensions
    {
        public static bool HasAttribute(this ISymbol symbol, string attributeName)
        {
            attributeName = "global::EffectSharp.SourceGenerators." + attributeName;
            foreach (var attr in symbol.GetAttributes())
            {
                var fullname = attr.AttributeClass.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat);

                if (fullname == attributeName ||
                    fullname == attributeName + "Attribute")
                {
                    return true;
                }
            }

            return false;
        }

        public static TAttributeData GetAttributeData<TAttributeData>(
            this ISymbol symbol,
            string attributeName)
            where TAttributeData : AttributeData
        {
            attributeName = "global::EffectSharp.SourceGenerators." + attributeName;
            foreach (var attr in symbol.GetAttributes())
            {
                var fullname = attr.AttributeClass.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat);

                if (fullname == attributeName ||
                    fullname == attributeName + "Attribute")
                {
                    return (TAttributeData)(object)attr;
                }
            }
            return null;
        }

        public static bool ReturnsTaskLike(this IMethodSymbol method, Compilation compilation)
        {
            var returnType = method.ReturnType;

            var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

            if (taskType == null)
                return false;

            // Task
            if (SymbolEqualityComparer.Default.Equals(returnType, taskType))
                return true;

            // Task<T>
            if (returnType is INamedTypeSymbol named &&
                named.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, taskOfTType))
                return true;

            return false;
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
    }
}
