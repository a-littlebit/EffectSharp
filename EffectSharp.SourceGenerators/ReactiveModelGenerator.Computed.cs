using System;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using EffectSharp.SourceGenerators.Model;
using EffectSharp.SourceGenerators.Utils;

namespace EffectSharp.SourceGenerators
{
    public sealed partial class ReactiveModelGenerator
    {
        private static ImmutableArray<ComputedSpec> BuildComputed(INamedTypeSymbol modelSymbol, ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var builder = ImmutableArray.CreateBuilder<ComputedSpec>();
            foreach (var method in modelSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var attr = SymbolHelper.GetAttribute(method, "Computed");
                if (attr == null)
                    continue;

                if (method.Parameters.Length > 0)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ComputedMethodTooManyParameters, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                var propertyName = method.Name.StartsWith("Compute", StringComparison.Ordinal)
                    ? method.Name.Substring("Compute".Length)
                    : "Computed" + method.Name;

                var setter = SymbolHelper.GetNamedArgument<string?>(attr, "Setter", null, out _);

                var fieldName = "_" + NameHelper.ToCamel(propertyName);
                builder.Add(new ComputedSpec(
                    fieldName,
                    propertyName,
                    method.Name,
                    method.ReturnType.ToDisplayString(FullName),
                    setter));
            }
            return builder.ToImmutable();
        }

        private static void EmitComputed(ModelSpec spec, IndentedTextWriter iw)
        {
            if (spec.Computeds.Length == 0)
                return;

            foreach (var c in spec.Computeds)
            {
                iw.WriteLine("private Computed<" + c.ValueTypeName + "> " + c.FieldName + ";");
                if (c.HasSetter)
                {
                    iw.WriteLine("public " + c.ValueTypeName + " " + c.PropertyName + " { get => " + c.FieldName + ".Value; set => " + c.FieldName + ".Value = value; }");
                }
                else
                {
                    iw.WriteLine("public " + c.ValueTypeName + " " + c.PropertyName + " => " + c.FieldName + ".Value;");
                }
                iw.WriteLine();
            }
        }

        private static void EmitComputedInitializers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var c in spec.Computeds)
            {
                iw.Write("this." + c.FieldName + " = Reactive.Computed<" + c.ValueTypeName + ">(() => this." + c.MethodName + "()");
                if (c.HasSetter)
                {
                    iw.WriteLine(",");
                    iw.Indent++;
                    iw.Write("setter: " + c.Setter);
                    iw.Indent--;
                }
                iw.WriteLine(");");

                iw.WriteLine("this." + c.FieldName + ".PropertyChanging += (s, e) =>");
                iw.Indent++;
                iw.WriteLine("this.PropertyChanging?.Invoke(this, new System.ComponentModel.PropertyChangingEventArgs(nameof(this." + c.PropertyName + ")));");
                iw.Indent--;
                iw.WriteLine("this." + c.FieldName + ".PropertyChanged += (s, e) =>");
                iw.Indent++;
                iw.WriteLine("this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this." + c.PropertyName + ")));");
                iw.Indent--;
                iw.WriteLine();
            }
        }

        private static void EmitComputedDisposers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var c in spec.Computeds)
            {
                iw.WriteLine("this." + c.FieldName + "?.Dispose();");
                iw.WriteLine("this." + c.FieldName + " = null;");
            }
            if (spec.Computeds.Length > 0)
                iw.WriteLine();
        }
    }
}
