using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    internal class ReactiveFieldContext
    {
        public IFieldSymbol FieldSymbol { get; }

        public AttributeData ReactiveFieldAttribute { get; }

        public bool IsAtomic { get; }

        public ITypeSymbol UnderlyingType { get; }

        public string EqualsMethod { get; }

        public string PropertyName =>
            NameHelper.ToPascalCase(
                NameHelper.RemoveLeadingUnderscore(FieldSymbol.Name));

        public bool MayBeIReactive(Compilation compilation)
        {
            if (UnderlyingType.IsValueType) return false;
            
            var iReactiveType = compilation.GetTypeByMetadataName("EffectSharp.IReactive");
            if (UnderlyingType.IsAssignableTo(iReactiveType))
                return false; // must be

            return !UnderlyingType.IsSealed;
        }

        public bool MustBeIReactive(Compilation compilation)
        {
            var iReactiveType = compilation.GetTypeByMetadataName("EffectSharp.IReactive");
            return UnderlyingType.IsAssignableTo(iReactiveType);
        }

        public ReactiveFieldContext(IFieldSymbol field, ReactiveModelContext modelContext)
        {
            FieldSymbol = field;
            ReactiveFieldAttribute = field.GetAttributeData("ReactiveField");
            IsAtomic = false;
            UnderlyingType = field.Type;

            var compilation = modelContext.Compilation;
            var iAtomicType = compilation.GetTypeByMetadataName("EffectSharp.IAtomic`1");
            if (iAtomicType != null && field.Type.TryGetGenericArgument(iAtomicType, 0, out var atomicArg))
            {
                IsAtomic = true;
                UnderlyingType = atomicArg ?? field.Type;
            }

            EqualsMethod = ReactiveFieldAttribute?.GetNamedArgument(
                "EqualsMethod",
                "global::System.Collections.Generic.EqualityComparer<T>.Default");
        }

        public string GetReadExpression()
        {
            if (IsAtomic)
            {
                return $"{FieldSymbol.Name}.Value";
            }
            else
            {
                return FieldSymbol.Name;
            }
        }

        public string GetWriteExpression(string valueExpression)
        {
            if (IsAtomic)
            {
                return $"{FieldSymbol.Name}.Value = {valueExpression}";
            }
            else
            {
                return $"{FieldSymbol.Name} = {valueExpression}";
            }
        }
    }
}
