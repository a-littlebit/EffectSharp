using System.Linq;
using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class NameHelper
    {
        public static string RemoveLeadingUnderscore(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name[0] == '_' ? name.Substring(1) : name;
        }

        public static string ToPascal(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 1) return char.ToUpperInvariant(name[0]).ToString();
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        public static string ToCamel(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (char.IsLower(name[0])) return name;
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 0 || i == chars.Length - 1 || (char.IsUpper(chars[i]) && char.IsUpper(chars[i + 1])))
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }
                else break;
            }
            return new string(chars);
        }

        public static string GetReactiveHintFileName(INamedTypeSymbol symbol)
        {
            var ns = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty;
            var types = symbol.GetContainingTypes().Reverse().Select(t => t.Name).Concat(new[] { symbol.Name });
            var qualified = string.Join(".", new[] { ns }.Concat(types).Where(s => !string.IsNullOrEmpty(s)));

            qualified = qualified.Replace('`', '_')
                                 .Replace('<', '_')
                                 .Replace('>', '_')
                                 .Replace(',', '_')
                                 .Replace(' ', '_');

            if (string.IsNullOrEmpty(qualified))
                qualified = symbol.Name.Replace('`', '_');

            if (symbol.TypeParameters.Length > 0)
            {
                qualified += "." + symbol.TypeParameters.Length;
            }

            return qualified + ".ReactiveModel.g.cs";
        }
    }
}
