using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Utils
{
    /// <summary>
    /// Provides an equality comparer for INamedTypeSymbol instances that compares type attributes and members within
    /// specified attribute namespaces.
    /// </summary>
    internal sealed class TypeMemberAttributesComparer
        : IEqualityComparer<INamedTypeSymbol>
    {
        private readonly ImmutableHashSet<string> _attributeNamespaces;

        public TypeMemberAttributesComparer(IEnumerable<string> attributeNamespaces)
        {
            _attributeNamespaces = attributeNamespaces.ToImmutableHashSet();
        }

        public bool Equals(INamedTypeSymbol x, INamedTypeSymbol y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;

            if (!SymbolEqualityComparer.Default.Equals(x, y))
                return false;

            if (!AttributesEqual(x.GetAttributes(), y.GetAttributes()))
                return false;

            return MembersEqual(x, y);
        }

        public int GetHashCode(INamedTypeSymbol obj)
        {
            var hash = new HashBuilder(SymbolEqualityComparer.Default.GetHashCode(obj));

            foreach (var attr in FilterAttributes(obj.GetAttributes()))
            {
                hash.Add(GetAttributeHash(attr));
            }

            foreach (var member in obj.GetMembers())
            {
                hash.Add(GetMemberHash(member));
            }

            return hash.ToHashCode();
        }

        private bool MembersEqual(INamedTypeSymbol x, INamedTypeSymbol y)
        {
            var xMembers = x.GetMembers();
            var yMembers = y.GetMembers();

            if (xMembers.Length != yMembers.Length)
                return false;

            foreach (var xMember in xMembers)
            {
                var yMember = yMembers.FirstOrDefault(m =>
                    SymbolEqualityComparer.Default.Equals(m, xMember));

                if (yMember is null)
                    return false;

                if (!AttributesEqual(
                        xMember.GetAttributes(),
                        yMember.GetAttributes()))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AttributesEqual(
            ImmutableArray<AttributeData> x,
            ImmutableArray<AttributeData> y)
        {
            var fx = FilterAttributes(x);
            var fy = FilterAttributes(y);

            if (fx.Length != fy.Length)
                return false;

            for (int i = 0; i < fx.Length; i++)
            {
                if (!AttributeEqual(fx[i], fy[i]))
                    return false;
            }

            return true;
        }

        private ImmutableArray<AttributeData> FilterAttributes(
            ImmutableArray<AttributeData> attrs)
        {
            return attrs
                .Where(a =>
                    a.AttributeClass?.ContainingNamespace is { } ns &&
                    _attributeNamespaces.Contains(ns.ToDisplayString()))
                .OrderBy(a => a.AttributeClass!.ToDisplayString())
                .ToImmutableArray();
        }

        private static bool AttributeEqual(AttributeData x, AttributeData y)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    x.AttributeClass, y.AttributeClass))
                return false;

            if (!TypedConstantsEqual(x.ConstructorArguments, y.ConstructorArguments))
                return false;

            return NamedArgumentsEqual(x.NamedArguments, y.NamedArguments);
        }

        private static bool TypedConstantsEqual(
            ImmutableArray<TypedConstant> x,
            ImmutableArray<TypedConstant> y)
        {
            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; i++)
            {
                if (!TypedConstantEqual(x[i], y[i]))
                    return false;
            }

            return true;
        }

        private static bool TypedConstantEqual(
            TypedConstant x,
            TypedConstant y)
        {
            if (x.Kind != y.Kind)
                return false;

            if (x.Kind == TypedConstantKind.Array)
            {
                var xa = x.Values;
                var ya = y.Values;

                if (xa.Length != ya.Length)
                    return false;

                for (int i = 0; i < xa.Length; i++)
                {
                    if (!TypedConstantEqual(xa[i], ya[i]))
                        return false;
                }

                return true;
            }

            return Equals(x.Value, y.Value);
        }

        private static bool NamedArgumentsEqual(
            ImmutableArray<KeyValuePair<string, TypedConstant>> x,
            ImmutableArray<KeyValuePair<string, TypedConstant>> y)
        {
            if (x.Length != y.Length)
                return false;

            foreach (var pair in x)
            {
                var other = y.FirstOrDefault(p => p.Key == pair.Key);
                if (other.Key is null)
                    return false;

                if (!TypedConstantEqual(pair.Value, other.Value))
                    return false;
            }

            return true;
        }

        private int GetMemberHash(ISymbol member)
        {
            var hash = new HashBuilder(SymbolEqualityComparer.Default.GetHashCode(member));

            foreach (var attr in FilterAttributes(member.GetAttributes()))
            {
                hash.Add(GetAttributeHash(attr));
            }

            return hash.ToHashCode();
        }

        private static int GetAttributeHash(AttributeData attr)
        {
            var hash = new HashBuilder(SymbolEqualityComparer.Default.GetHashCode(attr.AttributeClass!));

            foreach (var arg in attr.ConstructorArguments)
            {
                hash.Add(GetTypedConstantHash(arg));
            }

            foreach (var named in attr.NamedArguments)
            {
                hash.Add(named.Key);
                hash.Add(GetTypedConstantHash(named.Value));
            }

            return hash.ToHashCode();
        }

        private static int GetTypedConstantHash(TypedConstant constant)
        {
            if (constant.Kind == TypedConstantKind.Array)
            {
                var hash = new HashBuilder(0);
                foreach (var v in constant.Values)
                    hash.Add(GetTypedConstantHash(v));
                return hash.ToHashCode();
            }

            return constant.Value?.GetHashCode() ?? 0;
        }
    }

    internal struct HashBuilder
    {
        private int _hash;

        public HashBuilder(int seed)
        {
            _hash = seed;
        }

        public void Add(int value)
        {
            unchecked
            {
                _hash = (_hash * 16777619) ^ value;
            }
        }

        public void Add(object value)
        {
            Add(value?.GetHashCode() ?? 0);
        }

        public int ToHashCode() => _hash;
    }
}
