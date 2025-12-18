using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class NameHelper
    {
        public static string RemoveLeadingUnderscore(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name[0] == '_')
                return name.Substring(1);

            return name;
        }

        public static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name.Length == 1)
                return char.ToUpperInvariant(name[0]).ToString();

            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (char.IsLower(name[0]))
                return name;

            char[] chars = name.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 0 || i == chars.Length - 1 || (char.IsUpper(chars[i]) && char.IsUpper(chars[i + 1])))
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }
                else
                {
                    break;
                }
            }

            return new string(chars);
        }

        public static string GetReactiveHintFileName(INamedTypeSymbol symbol)
        {
            // Build a readable unique hint name: Namespace.Type.NestedType
            string ns = symbol.ContainingNamespace?.IsGlobalNamespace == false
                ? symbol.ContainingNamespace.ToDisplayString()
                : string.Empty;

            var types = Enumerable.Repeat(symbol, 1)
                .Concat(GetContainingTypes(symbol))
                .Reverse() // outermost → innermost
                .Select(s => s.Name);

            string qualified = string.Join(".", new[] { ns }
                .Concat(types).Where(s => !string.IsNullOrWhiteSpace(s)));

            // Sanitize a few potentially problematic characters, keep '.' for readability
            qualified = qualified
                .Replace('`', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(',', '_')
                .Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(qualified))
                qualified = symbol.Name.Replace('`', '_');

            return qualified + ".ReactiveModel.g.cs";
        }

        private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol symbol)
        {
            var current = symbol.ContainingType;
            while (current != null)
            {
                yield return current;
                current = current.ContainingType;
            }
        }
    }
}
