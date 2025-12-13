using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal sealed class ReactiveFieldEmitter : IReactiveModelEmitter
    {
        public void Emit(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
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
                iw.WriteLine(
                    "private readonly Dependency " +
                    field.Name +
                    "_dependency = new Dependency();"
                );
            }
        }

        private static void EmitProperties(
            ReactiveModelContext context,
            IndentedTextWriter iw)
        {
            foreach (var field in context.ReactiveFields)
            {
                var fieldName = field.Name;
                var propertyName =
                    NameHelper.ToPascalCase(
                        NameHelper.RemoveLeadingUnderscore(fieldName));
                var fieldType = field.Type.ToDisplayString();

                iw.WriteLine("public " + fieldType + " " + propertyName);
                iw.WriteLine("{");
                iw.Indent++;

                iw.WriteLine("get");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine(fieldName + "_dependency.Track();");
                iw.WriteLine("return " + fieldName + ";");
                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();

                iw.WriteLine("set");
                iw.WriteLine("{");
                iw.Indent++;

                iw.WriteLine(
                    "if (System.Collections.Generic.EqualityComparer<" +
                    fieldType +
                    ">.Default.Equals(" +
                    fieldName +
                    ", value))");
                iw.Indent++;
                iw.WriteLine("return;");
                iw.Indent--;

                iw.WriteLine();
                iw.WriteLine(
                    "PropertyChanging?.Invoke(" +
                    "this, new System.ComponentModel.PropertyChangingEventArgs(nameof(" +
                    propertyName + ")));");

                iw.WriteLine();
                iw.WriteLine(fieldName + " = value;");
                iw.WriteLine();
                iw.WriteLine(fieldName + "_dependency.Trigger();");
                iw.WriteLine();

                iw.WriteLine("if (PropertyChanged != null)");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine(
                    "TaskManager.EnqueueNotification(" +
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

            foreach (var field in context.ReactiveFields)
            {
                iw.WriteLine(field.Name + "_dependency.Track();");
                if (field.Type.IsValueType)
                {
                    iw.WriteLine();
                    continue;
                }
                iw.WriteLine(
                    "if (" + field.Name + " is IReactive r_" + field.Name + ")");
                iw.WriteLine("{");
                iw.Indent++;
                iw.WriteLine("r_" + field.Name + ".TrackDeep();");
                iw.Indent--;
                iw.WriteLine("}");
                iw.WriteLine();
            }

            iw.Indent--;
            iw.WriteLine("}");
        }
    }
}
