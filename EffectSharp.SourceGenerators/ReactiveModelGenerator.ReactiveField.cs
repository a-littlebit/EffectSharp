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
        private static ImmutableArray<ReactiveFieldSpec> BuildReactiveFields(INamedTypeSymbol modelSymbol, Compilation compilation)
        {
            var iAtomicT = compilation.GetTypeByMetadataName("EffectSharp.IAtomic`1");
            var iReactive = compilation.GetTypeByMetadataName("EffectSharp.IReactive");
            var builder = ImmutableArray.CreateBuilder<ReactiveFieldSpec>();
            foreach (var field in modelSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                var attr = SymbolHelper.GetAttribute(field, "ReactiveField");
                if (attr == null)
                    continue;

                var isAtomic = SymbolHelper.TryGetGenericTypeArgument(field.Type, iAtomicT, 0, out var atomicArgument);
                var underlyingType = atomicArgument ?? field.Type;
                var propertyName = NameHelper.ToPascal(NameHelper.RemoveLeadingUnderscore(field.Name));
                var fieldTypeName = underlyingType.ToDisplayString(FullName);
                var equalsMethod = SymbolHelper.GetNamedArgument(attr, "EqualsMethod", "global::System.Collections.Generic.EqualityComparer<T>.Default", out var equalsProvided);

                EqualityMode equalityMode;
                string equalityCall;
                if (equalsProvided && string.IsNullOrWhiteSpace(equalsMethod))
                {
                    equalityMode = EqualityMode.None;
                    equalityCall = string.Empty;
                }
                else if (equalsMethod == "global::System.Collections.Generic.EqualityComparer<T>.Default")
                {
                    equalityMode = EqualityMode.Default;
                    equalityCall = string.Format("System.Collections.Generic.EqualityComparer<{0}>.Default.Equals({1}, value)", fieldTypeName, GetReadExpr(field, isAtomic));
                }
                else
                {
                    equalityMode = EqualityMode.Custom;
                    equalityCall = string.Format("{0}({1}, value)", equalsMethod, GetReadExpr(field, isAtomic));
                }

                var dependencyName = field.Name + "_dependency";
                var spec = new ReactiveFieldSpec(
                    field.Name,
                    dependencyName,
                    propertyName,
                    fieldTypeName,
                    GetReadExpr(field, isAtomic),
                    GetWriteExpr(field, isAtomic, "value"),
                    equalityMode,
                    equalityCall,
                    MayBeReactive(underlyingType, iReactive),
                    MustBeReactive(underlyingType, iReactive));
                builder.Add(spec);
            }
            return builder.ToImmutable();
        }

        private static bool MayBeReactive(ITypeSymbol? type, INamedTypeSymbol? iReactive)
        {
            if (type == null || type.IsValueType)
                return false;
            if (iReactive == null)
                return false;
            if (MustBeReactive(type, iReactive))
                return false;
            return !type.IsSealed;
        }

        private static bool MustBeReactive(ITypeSymbol? type, INamedTypeSymbol? iReactive)
        {
            if (type == null || iReactive == null)
                return false;
            return SymbolHelper.ImplementsInterface(type as INamedTypeSymbol, iReactive);
        }

        private static string GetReadExpr(IFieldSymbol field, bool isAtomic)
        {
            return isAtomic ? field.Name + ".Value" : field.Name;
        }

        private static string GetWriteExpr(IFieldSymbol field, bool isAtomic, string valueExpression)
        {
            return isAtomic ? field.Name + ".Value = " + valueExpression : field.Name + " = " + valueExpression;
        }

        private static void EmitReactiveFields(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var field in spec.ReactiveFields)
            {
                iw.WriteLine("private readonly Dependency " + field.DependencyFieldName + " = new Dependency();");
                iw.WriteLine("public " + field.FieldTypeName + " " + field.PropertyName);
                iw.WriteLine("{");
                iw.Indent++;

                iw.WriteLine("get");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine(field.DependencyFieldName + ".Track();");
                iw.WriteLine("return " + field.ReadExpression + ";");
                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();

                iw.WriteLine("set");
                iw.WriteLine("{");
                iw.Indent++;

                if (field.EqualityMode == EqualityMode.Default || field.EqualityMode == EqualityMode.Custom)
                {
                    iw.WriteLine("if (" + field.EqualityExpression + ")");
                    iw.Indent++;
                    iw.WriteLine("return;");
                    iw.Indent--;
                    iw.WriteLine();
                }

                iw.WriteLine("PropertyChanging?.Invoke(this, new System.ComponentModel.PropertyChangingEventArgs(nameof(" + field.PropertyName + ")));");
                iw.WriteLine();
                iw.WriteLine(field.WriteExpression + ";");
                iw.WriteLine();
                iw.WriteLine(field.DependencyFieldName + ".Trigger();");
                iw.WriteLine();
                iw.WriteLine("if (PropertyChanged != null)");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine("TaskManager.QueueNotification(this, nameof(" + field.PropertyName + "), e => PropertyChanged?.Invoke(this, e));");
                iw.Indent--;
                iw.WriteLine("}");

                iw.Indent--;
                iw.WriteLine("}");

                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();
            }

            iw.WriteLine("public void TrackDeep()");
            iw.WriteLine("{");
            iw.Indent++;
            foreach (var field in spec.ReactiveFields)
            {
                iw.WriteLine(field.DependencyFieldName + ".Track();");
                if (field.MayBeReactive)
                {
                    iw.WriteLine("if (" + field.ReadExpression + " is IReactive r_" + field.BackingFieldName + ")");
                    iw.WriteLine("{");
                    iw.Indent++;
                    iw.WriteLine("r_" + field.BackingFieldName + ".TrackDeep();");
                    iw.Indent--;
                    iw.WriteLine("}");
                }
                else if (field.MustBeReactive)
                {
                    iw.WriteLine(field.ReadExpression + ".TrackDeep();");
                }
                iw.WriteLine();
            }
            iw.Indent--;
            iw.WriteLine("}");
            iw.WriteLine();
        }
    }
}
