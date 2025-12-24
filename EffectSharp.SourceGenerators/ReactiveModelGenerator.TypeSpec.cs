using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using EffectSharp.SourceGenerators.Model;
using EffectSharp.SourceGenerators.Utils;

namespace EffectSharp.SourceGenerators
{
    public sealed partial class ReactiveModelGenerator
    {
        private static TypeSpec CreateTypeSpec(INamedTypeSymbol modelSymbol, Compilation compilation)
        {
            var iReactive = compilation.GetTypeByMetadataName("EffectSharp.IReactive");
            var nsSymbol = modelSymbol.ContainingNamespace;
            var ns = nsSymbol != null && !nsSymbol.IsGlobalNamespace ? nsSymbol.ToDisplayString() : string.Empty;
            var containing = modelSymbol.GetContainingTypes().Select(ComposeContaining).Reverse().ToImmutableArray();

            const string baseList = "IReactive, System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged";
            var header = ComposeTypeHeader(modelSymbol, baseList);
            var constraints = WriteTypeParameterConstraints(modelSymbol);

            var emitChanging = !SymbolHelper.HasMemberInHierarchy<IEventSymbol>(modelSymbol, "PropertyChanging");
            var emitChanged = !SymbolHelper.HasMemberInHierarchy<IEventSymbol>(modelSymbol, "PropertyChanged");

            var hintName = NameHelper.GetReactiveHintFileName(modelSymbol);

            return new TypeSpec(ns, containing, header, constraints, emitChanging, emitChanged, hintName);
        }

        private static string ComposeTypeHeader(INamedTypeSymbol type, string baseList)
        {
            var access = type.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "public"
            };

            var modifiers = new List<string> { access, "partial" };
            if (type.IsStatic) modifiers.Add("static");
            if (type.IsSealed && !type.IsRecord) modifiers.Add("sealed");
            if (type.IsAbstract && !type.IsRecord) modifiers.Add("abstract");

            var typeKeyword = type.IsRecord ? "record" : (type.TypeKind == TypeKind.Struct ? "struct" : "class");
            var typeParams = type.TypeParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", type.TypeParameters.Select(tp => tp.Name)) + ">";

            var header = string.Join(" ", modifiers) + " " + typeKeyword + " " + type.Name + typeParams;
            if (!string.IsNullOrEmpty(baseList))
                header += " : " + baseList;
            return header;
        }

        private static ImmutableArray<string> WriteTypeParameterConstraints(INamedTypeSymbol type)
        {
            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var tp in type.TypeParameters)
            {
                var constraints = new List<string>();
                if (tp.HasReferenceTypeConstraint) constraints.Add("class");
                if (tp.HasValueTypeConstraint) constraints.Add("struct");
                if (tp.HasNotNullConstraint) constraints.Add("notnull");
                if (tp.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
                constraints.AddRange(tp.ConstraintTypes.Select(t => t.ToDisplayString(FullName)));
                if (tp.HasConstructorConstraint) constraints.Add("new()");
                if (constraints.Count > 0)
                {
                    builder.Add(string.Format("where {0} : {1}", tp.Name, string.Join(", ", constraints)));
                }
            }
            return builder.ToImmutable();
        }

        private static ContainingTypeSpec ComposeContaining(INamedTypeSymbol type)
        {
            var header = ComposeTypeHeader(type, string.Empty);
            var constraints = WriteTypeParameterConstraints(type);
            return new ContainingTypeSpec(header, constraints);
        }
    }
}
