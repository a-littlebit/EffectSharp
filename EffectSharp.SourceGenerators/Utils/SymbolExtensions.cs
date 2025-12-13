using Microsoft.CodeAnalysis;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolExtensions
    {
        public static bool HasAttribute(this ISymbol symbol, string attributeName)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var name = attr.AttributeClass.Name;

                if (name == attributeName ||
                    name == attributeName + "Attribute")
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
            foreach (var attr in symbol.GetAttributes())
            {
                var name = attr.AttributeClass.Name;
                if (name == attributeName ||
                    name == attributeName + "Attribute")
                {
                    return (TAttributeData)(object)attr;
                }
            }
            return null;
        }
    }
}
