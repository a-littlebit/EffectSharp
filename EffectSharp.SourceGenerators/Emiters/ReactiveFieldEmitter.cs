using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal sealed class ReactiveFieldEmitter : IReactiveModelEmitter
    {
        public void Emit(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
            context.ReactiveFields = context.ModelSymbol.GetMembers()
                              .OfType<IFieldSymbol>()
                              .Select(f => new ReactiveFieldContext(f, context))
                              .Where(f => f.ReactiveFieldAttribute != null)
                              .ToList();

            EmitDependencyFields(context, iw);
            iw.WriteLine();
            EmitProperties(context, iw);
            iw.WriteLine();
            EmitTrackDeep(context, iw);
        }

        private static void EmitDependencyFields(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
            foreach (var field in context.ReactiveFields)
            {
                var fieldName = field.FieldSymbol.Name;
                iw.WriteLine(
                    "private readonly Dependency " +
                    fieldName +
                    "_dependency = new Dependency();"
                );
            }
        }

        private static void EmitProperties(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
            foreach (var fieldContext in context.ReactiveFields)
            {
                var field = fieldContext.FieldSymbol;
                var fieldName = field.Name;
                var propertyName = fieldContext.PropertyName;
                var fieldType = fieldContext.UnderlyingType.ToDisplayString();
                var readExpression = fieldContext.GetReadExpression();
                var equalsMethod = fieldContext.EqualsMethod;

                iw.WriteLine("public " + fieldType + " " + propertyName);
                iw.WriteLine("{");
                iw.Indent++;

                iw.WriteLine("get");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine(fieldName + "_dependency.Track();");
                iw.WriteLine("return " + readExpression + ";");
                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();

                iw.WriteLine("set");
                iw.WriteLine("{");
                iw.Indent++;

                if (equalsMethod == "global::System.Collections.Generic.EqualityComparer<T>.Default")
                {
                    iw.WriteLine($"if (System.Collections.Generic.EqualityComparer<{fieldType}>.Default.Equals({readExpression}, value))");
                    iw.Indent++;
                    iw.WriteLine("return;");
                    iw.Indent--;
                }
                else if (!string.IsNullOrWhiteSpace(equalsMethod))
                {
                    iw.WriteLine($"if ({equalsMethod}({readExpression}, value))");
                    iw.Indent++;
                    iw.WriteLine("return;");
                    iw.Indent--;
                }

                iw.WriteLine();
                iw.WriteLine(
                    "PropertyChanging?.Invoke(" +
                    "this, new System.ComponentModel.PropertyChangingEventArgs(nameof(" +
                    propertyName + ")));");

                iw.WriteLine();
                iw.WriteLine(fieldContext.GetWriteExpression("value") + ";");
                iw.WriteLine();
                iw.WriteLine(fieldName + "_dependency.Trigger();");
                iw.WriteLine();

                iw.WriteLine("if (PropertyChanged != null)");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine(
                    "TaskManager.QueueNotification(" +
                    "this, nameof(" + propertyName + "), " +
                    "e => PropertyChanged?.Invoke(this, e));");
                iw.Indent--;
                iw.WriteLine("}");

                iw.Indent--;
                iw.WriteLine("}");
                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();
            }
        }

        private static void EmitTrackDeep(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
            iw.WriteLine("public void TrackDeep()");
            iw.WriteLine("{");
            iw.Indent++;

            foreach (var fieldContext in context.ReactiveFields)
            {
                var field = fieldContext.FieldSymbol;
                var readExpression = fieldContext.GetReadExpression();
                iw.WriteLine(field.Name + "_dependency.Track();");
                if (fieldContext.MayBeIReactive(context.Compilation))
                {
                    iw.WriteLine(
                        "if (" + readExpression + " is IReactive r_" + field.Name + ")");
                    iw.WriteLine("{");
                    iw.Indent++;
                    iw.WriteLine("r_" + field.Name + ".TrackDeep();");
                    iw.Indent--;
                    iw.WriteLine("}");
                }
                else if (fieldContext.MustBeIReactive(context.Compilation))
                {
                    iw.WriteLine($"{readExpression}.TrackDeep();");
                }
                iw.WriteLine();
            }

            iw.Indent--;
            iw.WriteLine("}");
        }
    }
}
