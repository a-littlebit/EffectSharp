using System;

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
    }
}
