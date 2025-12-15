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

        public string PropertyName =>
            NameHelper.ToPascalCase(
                NameHelper.RemoveLeadingUnderscore(FieldSymbol.Name));

        public ReactiveFieldContext(IFieldSymbol field, ReactiveModelContext modelContext)
        {
            FieldSymbol = field;
            ReactiveFieldAttribute = field.GetAttributeData("ReactiveField");
            IsAtomic = false;
            UnderlyingType = field.Type;

            var compilation = modelContext.GeneratorContext.Compilation;
            var iAtomicType = compilation.GetTypeByMetadataName("EffectSharp.IAtomic`1")!;
            for (var type = field.Type; type != null; type = type.BaseType)
            {
                if (!(type is INamedTypeSymbol namedType))
                    continue;

                if (namedType.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(
                        namedType.OriginalDefinition,
                        iAtomicType))
                {
                    IsAtomic = true;
                    UnderlyingType = namedType.TypeArguments[0];
                    break;
                }

                foreach (var iface in namedType.Interfaces)
                {
                    if (iface.IsGenericType &&
                        SymbolEqualityComparer.Default.Equals(
                            iface.OriginalDefinition,
                            iAtomicType))
                    {
                        IsAtomic = true;
                        UnderlyingType = iface.TypeArguments[0];
                        break;
                    }
                }
            }
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
