using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolHelper
    {
        private const string DefaultAttributeNamespace = "EffectSharp.SourceGenerators";

        public static IEnumerable<INamedTypeSymbol> GetContainingTypes(this INamedTypeSymbol symbol)
        {
            var current = symbol?.ContainingType;
            while (current != null)
            {
                yield return current;
                current = current.ContainingType;
            }
        }

        public static bool ImplementsInterface(INamedTypeSymbol? type, INamedTypeSymbol? iface)
        {
            if (type == null || iface == null)
                return false;

            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface))
                || SymbolEqualityComparer.Default.Equals(type, iface);
        }

        public static bool HasMemberInHierarchy<TMember>(INamedTypeSymbol? type, string name, Func<TMember, bool>? predicate = null)
            where TMember : class, ISymbol
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var members = current.GetMembers(name).OfType<TMember>();
                if (predicate == null)
                {
                    if (members.Any())
                        return true;
                }
                else if (members.Any(predicate))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetGenericTypeArgument(ITypeSymbol? type, INamedTypeSymbol? genericDefinition, int argumentIndex, out ITypeSymbol? argument)
        {
            argument = null;
            if (type == null || genericDefinition == null)
                return false;

            if (type is INamedTypeSymbol named)
            {
                if (named.IsGenericType && SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, genericDefinition) && named.TypeArguments.Length > argumentIndex)
                {
                    argument = named.TypeArguments[argumentIndex];
                    return true;
                }

                foreach (var iface in named.AllInterfaces)
                {
                    if (iface.IsGenericType && SymbolEqualityComparer.Default.Equals(iface.ConstructedFrom, genericDefinition) && iface.TypeArguments.Length > argumentIndex)
                    {
                        argument = iface.TypeArguments[argumentIndex];
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsTaskLike(ITypeSymbol? type, Compilation compilation, out INamedTypeSymbol? result)
        {
            result = null;
            if (type == null)
                return false;

            var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            if (task == null || taskOfT == null)
                return false;

            if (type is INamedTypeSymbol named && named.IsGenericType && SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, taskOfT))
            {
                result = named.TypeArguments[0] as INamedTypeSymbol;
                return true;
            }

            return SymbolEqualityComparer.Default.Equals(type, task);
        }

        public static AttributeData? GetAttribute(ISymbol symbol, string name, string attributeNamespace = DefaultAttributeNamespace)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var cls = attr.AttributeClass;
                if (cls == null) continue;
                var attrName = cls.Name;
                var attrNs = cls.ContainingNamespace?.ToDisplayString();
                if ((attrName == name || attrName == name + "Attribute") && attrNs == attributeNamespace)
                    return attr;
            }
            return null;
        }

        public static T GetNamedArgument<T>(AttributeData attr, string name, T defaultValue, out bool provided)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != name)
                    continue;
                provided = true;
                var value = kvp.Value.Value;
                if (value is T typed)
                    return typed;
                return defaultValue;
            }
            provided = false;
            return defaultValue;
        }

        public static T GetNamedArgument<T>(AttributeData attr, string name, T defaultValue = default!)
        {
            return GetNamedArgument(attr, name, defaultValue, out _);
        }

        public static ImmutableArray<T> GetArrayArgument<T>(AttributeData attr, string name)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != name)
                    continue;
                if (kvp.Value.Values.IsDefaultOrEmpty)
                    return ImmutableArray<T>.Empty;
                var builder = ImmutableArray.CreateBuilder<T>(kvp.Value.Values.Length);
                foreach (var tc in kvp.Value.Values)
                {
                    if (tc.Value is T value)
                        builder.Add(value);
                }
                return builder.ToImmutable();
            }
            return ImmutableArray<T>.Empty;
        }
    }
}
