using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EffectSharp.SourceGenerators.Tests
{
    internal static class GeneratorTestHelper
    {
        public static (Compilation Compilation, GeneratorDriverRunResult RunResult, GeneratorDriver Driver) RunGenerator(params string[] sources)
        {
            var compilation = CreateCompilation(sources);
            var generator = new ReactiveModelGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);
            var result = driver.GetRunResult();
            return (compilation, result, driver);
        }

        public static Compilation CreateCompilation(params string[] sources)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions)).ToList();

            var references = GetFrameworkReferences();

            return CSharpCompilation.Create(
                assemblyName: "Tests_" + Guid.NewGuid().ToString("N"),
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static IEnumerable<MetadataReference> GetFrameworkReferences()
        {
            // Collect trusted platform assemblies to avoid hunting individual DLLs
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
            var paths = tpa?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            string[] needed =
            [
                "System.Private.CoreLib.dll",
                "System.Runtime.dll",
                "System.Console.dll",
                "System.Collections.dll",
                "System.Linq.dll",
                "System.ComponentModel.Primitives.dll",
                "System.ComponentModel.TypeConverter.dll",
                "System.ObjectModel.dll",
                "System.Threading.dll",
                "System.Threading.Tasks.dll",
                "System.Runtime.Extensions.dll"
            ];

            var set = new HashSet<string>(needed, StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                var file = Path.GetFileName(p);
                if (set.Contains(file))
                {
                    yield return MetadataReference.CreateFromFile(p);
                    set.Remove(file);
                }
            }

            // Always include the assembly containing object as a fallback
            yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            // And the C# assembly for attributes usage if needed
            yield return MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        }

        public static IEnumerable<Diagnostic> AllDiags(GeneratorDriverRunResult result)
            => result.Diagnostics.Concat(result.Results.SelectMany(r => r.Diagnostics));

        public static string EffectSharpAttributeStubs => @"using System;
namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ReactiveModelAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ReactiveFieldAttribute : Attribute
    {
        public string EqualsMethod { get; set; } = ""global::System.Collections.Generic.EqualityComparer<T>.Default"";
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ComputedAttribute : Attribute
    {
        public string Setter { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ComputedListAttribute : Attribute
    {
        public string KeySelector { get; set; }
        public string EqualityComparer { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FunctionCommandAttribute : Attribute
    {
        public string CanExecute { get; set; }
        public bool AllowConcurrentExecution { get; set; } = true;
        public string ExecutionScheduler { get; set; } = """";
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class WatchAttribute : Attribute
    {
        public string[] Values { get; set; }
        public bool Immediate { get; set; } = false;
        public bool Deep { get; set; } = false;
        public bool Once { get; set; } = false;
        public string Scheduler { get; set; } = ""null"";
        public bool SuppressEquality { get; set; } = true;
        public string EqualityComparer { get; set; } = ""null"";
    }
}
";

        public static string MinimalEffectSharpRuntimeStubs => @"namespace EffectSharp
{
    public interface IReactive { void TrackDeep(); }
    public class Dependency { public void Track() { } public void Trigger() { } }
    public static class TaskManager { public static void QueueNotification(object model, string name, System.Action<System.ComponentModel.PropertyChangedEventArgs> a) { } }
    public interface IAtomic<T> { T Value { get; set; } }
    public class AtomicInt : IAtomic<int> { public int Value { get; set; } }
    public class AtomicFactory<T> { public static IAtomic<T> Create() => default; }
    public class Computed<T> { public T Value { get; set; } public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged; public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging; public void Dispose() { } }
    public class Effect { public void Dispose() { } }
    public static class Reactive { public static Computed<T> Computed<T>(System.Func<T> g, System.Action<T> s = null) => new Computed<T>(); public static Effect Watch<T>(System.Func<T> g, System.Action<T,T> c, object o = null) => new Effect(); }
    public interface IFunctionCommand<TParam> { }
    public interface IFunctionCommand<TParam, TResult> { }
    public interface IAsyncFunctionCommand<TParam> { }
    public interface IAsyncFunctionCommand<TParam, TResult> { }
    public static class FunctionCommand {
        public static IFunctionCommand<TParam> Create<TParam>(System.Action<TParam> e, System.Func<bool> c = null, bool allowConcurrentExecution = true) => default;
        public static IFunctionCommand<TParam, TResult> Create<TParam, TResult>(System.Func<TParam, TResult> e, System.Func<bool> c = null, bool allowConcurrentExecution = true) => default;
        public static IAsyncFunctionCommand<TParam> CreateFromTask<TParam>(System.Func<TParam, System.Threading.CancellationToken, System.Threading.Tasks.Task> e, System.Func<bool> c = null, bool allowConcurrentExecution = true, System.Threading.Tasks.TaskScheduler s = null) => default;
        public static IAsyncFunctionCommand<TParam, TResult> CreateFromTask<TParam, TResult>(System.Func<TParam, System.Threading.CancellationToken, System.Threading.Tasks.Task<TResult>> e, System.Func<bool> c = null, bool allowConcurrentExecution = true, System.Threading.Tasks.TaskScheduler s = null) => default;
    }
}
";
    }
}
