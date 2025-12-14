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

        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (char.IsLower(name[0]))
                return name;

            char[] chars = name.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 0 || (i + 1 < chars.Length && char.IsUpper(chars[i + 1])))
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }
                else
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                    break;
                }
            }

            return new string(chars);
        }

    }
}
