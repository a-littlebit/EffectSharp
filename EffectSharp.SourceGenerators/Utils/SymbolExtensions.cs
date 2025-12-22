using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class SymbolExtensions
    {
        public static void RequireTypes(KnownTypeRegistry registry)
        {
            registry.RequireRange(new[]
            {
                "System.Threading.Tasks.Task",
                "System.Threading.Tasks.Task`1",
            });
        }

        public static bool IsAssignableTo(
            this ITypeSymbol type,
            INamedTypeSymbol? targetType)
        {
            if (type == null || targetType == null)
                return false;

            // Walk the inheritance chain
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, targetType))
                {
                    return true;
                }
                if (targetType.TypeKind == TypeKind.Interface && current is INamedTypeSymbol named)
                {
                    // Check implemented interfaces
                    foreach (var iface in named.AllInterfaces)
                    {
                        if (SymbolEqualityComparer.Default.Equals(iface, targetType))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool TryGetGenericArgument(
            this ITypeSymbol type,
            INamedTypeSymbol genericDefinition,
            int argumentIndex,
            out INamedTypeSymbol? typeArgument)
        {
            typeArgument = null;

            if (type == null || genericDefinition == null)
                return false;

            // Walk the inheritance chain
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current is INamedTypeSymbol named)
                {
                    // Check self constructed from generic interface (rare, for interface types)
                    if (named.IsGenericType &&
                        SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, genericDefinition) &&
                        named.TypeArguments.Length > argumentIndex)
                    {
                        typeArgument = named.TypeArguments[argumentIndex] as INamedTypeSymbol;
                        return typeArgument != null;
                    }

                    // Check implemented interfaces
                    if (genericDefinition.TypeKind == TypeKind.Interface)
                    {
                        foreach (var iface in named.AllInterfaces)
                        {
                            if (iface.IsGenericType &&
                                SymbolEqualityComparer.Default.Equals(iface.ConstructedFrom, genericDefinition) &&
                                iface.TypeArguments.Length > argumentIndex)
                            {
                                typeArgument = iface.TypeArguments[argumentIndex] as INamedTypeSymbol;
                                return typeArgument != null;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool HasAttribute(
            this ISymbol symbol,
            string attributeName,
            string containingNamespace = "EffectSharp.SourceGenerators")
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null)
                    continue;
                var className = attrClass.Name;
                var classNamespace = attrClass.ContainingNamespace.ToDisplayString();

                if ((className == attributeName ||
                    className == attributeName + "Attribute")
                    && classNamespace == containingNamespace)
                {
                    return true;
                }
            }

            return false;
        }

        public static AttributeData? GetAttributeData(
            this ISymbol symbol,
            string attributeName,
            string containingNamespace = "EffectSharp.SourceGenerators")
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null)
                    continue;

                var className = attrClass.Name;
                var classNamespace = attrClass.ContainingNamespace.ToDisplayString();

                if ((className == attributeName ||
                    className == attributeName + "Attribute")
                    && classNamespace == containingNamespace)
                {
                    return attr;
                }
            }
            return null;
        }

        public static T GetNamedArgument<T>(
            this AttributeData attributeData,
            string argumentName,
            T defaultValue = default!)
        {
            var namedArg = attributeData.NamedArguments
                .FirstOrDefault(kv => kv.Key == argumentName);
            if (namedArg.Value.Value is T value)
            {
                return value;
            }
            return defaultValue;
        }

        public static List<T> GetNamedArgumentList<T>(
            this AttributeData attributeData,
            string argumentName)
        {
            var result = new List<T>();
            var namedArg = attributeData.NamedArguments
                .FirstOrDefault(kv => kv.Key == argumentName);
            if (namedArg.Value.Values != null)
            {
                foreach (var typedConstant in namedArg.Value.Values)
                {
                    if (typedConstant.Value is T value)
                    {
                        result.Add(value);
                    }
                }
            }
            return result;
        }

        public static IEnumerable<INamedTypeSymbol> GetContainingTypes(this INamedTypeSymbol symbol)
        {
            var current = symbol.ContainingType;
            while (current != null)
            {
                yield return current;
                current = current.ContainingType;
            }
        }

        public static string ComposeTypeHeader(this INamedTypeSymbol type, string? baseList = null)
        {
            var accessibility = type.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Protected => "protected",
                Accessibility.Private => "private",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => "public"
            };

            var modifiers = new List<string> { accessibility, "partial" };
            if (type.IsStatic) modifiers.Add("static");
            if (type.IsSealed && !type.IsRecord) modifiers.Add("sealed");
            if (type.IsAbstract && !type.IsRecord) modifiers.Add("abstract");

            var kind = type.IsRecord ? "record" : (type.TypeKind == TypeKind.Struct ? "struct" : "class");
            var typeParams = type.TypeParameters.Length > 0
                ? "<" + string.Join(", ", type.TypeParameters.Select(tp => tp.Name)) + ">"
                : string.Empty;

            var header = string.Join(" ", modifiers) + " " + kind + " " + type.Name + typeParams;
            if (!string.IsNullOrEmpty(baseList))
                header += ": " + baseList;
            return header;
        }

        public static void WriteTypeParameterConstraints(this INamedTypeSymbol type, IndentedTextWriter iw)
        {
            foreach (var tp in type.TypeParameters)
            {
                var constraints = new List<string>();
                if (tp.HasReferenceTypeConstraint) constraints.Add("class");
                if (tp.HasValueTypeConstraint) constraints.Add("struct");
                if (tp.HasNotNullConstraint) constraints.Add("notnull");
                if (tp.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
                constraints.AddRange(tp.ConstraintTypes.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                if (tp.HasConstructorConstraint) constraints.Add("new()");
                if (constraints.Count > 0)
                {
                    iw.WriteLine($"where {tp.Name} : {string.Join(", ", constraints)}");
                }
            }
        }

        public static TMember? GetMemberInHierarchy<TMember>(this INamedTypeSymbol model, string memberName)
            where TMember : class, ISymbol
        {
            INamedTypeSymbol? current = model;
            while (current != null)
            {
                var member = current.GetMembers().OfType<TMember>().Where(m => m.Name == memberName).FirstOrDefault();
                if (member != null)
                    return member;
                current = current.BaseType;
            }
            return null;
        }

        public static bool ReturnsTaskLike(this IMethodSymbol method, KnownTypes knownTypes, out INamedTypeSymbol? resultType)
        {
            var returnType = method.ReturnType;

            var taskType = knownTypes.Get("System.Threading.Tasks.Task");
            var taskOfTType = knownTypes.Get("System.Threading.Tasks.Task`1");

            resultType = null;

            if (taskType == null || taskOfTType == null)
                return false;

            // Task<T>
            if (returnType is INamedTypeSymbol named &&
                named.TryGetGenericArgument(taskOfTType, 0, out resultType))
            {
                return true;
            }

            // Task
            if (returnType.IsAssignableTo(taskType))
                return true;

            return false;
        }
    }
}
