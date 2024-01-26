using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PrimaryConstructor
{
    public class MemberSymbolInfo
    {
        public string Type { get; set; }
        public string ParameterName { get; set; }
        public string Name { get; set; }
        public IEnumerable<AttributeData> Attributes { get; set; }
    }

    [Generator]
    internal class PrimaryConstructorGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var classSymbols = GetClassSymbols(context, receiver);
            var classNames = new Dictionary<string, int>();
            foreach (var classSymbol in classSymbols)
            {
                classNames.TryGetValue(classSymbol.Name, out var i);
                var name = i == 0 ? classSymbol.Name : $"{classSymbol.Name}{i + 1}";
                classNames[classSymbol.Name] = i + 1;
                
                var source = CSharpSyntaxTree
                    .ParseText(SourceText.From(CreatePrimaryConstructor(classSymbol), Encoding.UTF8))
                    .GetRoot()
                    .NormalizeWhitespace()
                    .SyntaxTree
                    .GetText();
                
                context.AddSource($"{name}.PrimaryConstructor.g.cs", source);
            }
        }

        private static bool HasFieldInitializer(IFieldSymbol symbol)
        {
            var field = symbol.DeclaringSyntaxReferences.ElementAtOrDefault(0)?.GetSyntax() as VariableDeclaratorSyntax;
            return field?.Initializer != null;
        }

        private static bool HasPropertyInitializer(IPropertySymbol symbol)
        {
            var property = symbol.DeclaringSyntaxReferences.ElementAtOrDefault(0)?.GetSyntax() as PropertyDeclarationSyntax;
            return property?.Initializer != null;
        }

        private static bool HasAttribute(ISymbol symbol, string name) => symbol
            .GetAttributes()
            .Any(x => x.AttributeClass?.Name == name);

        private static readonly SymbolDisplayFormat TypeFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );
        private static readonly SymbolDisplayFormat PropertyTypeFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );
        private static string CreatePrimaryConstructor(INamedTypeSymbol classSymbol)
        {
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            var baseClassConstructorArgs = classSymbol.BaseType != null && HasAttribute(classSymbol.BaseType, nameof(PrimaryConstructorAttribute))
                ? GetMembers(classSymbol.BaseType, true)
                : null;
            var baseConstructorInheritance = baseClassConstructorArgs?.Count > 0
                ? $" : base({string.Join(", ", baseClassConstructorArgs.Select(it => it.ParameterName))})"
                : "";

            var memberList = GetMembers(classSymbol, false);
            var arguments = (baseClassConstructorArgs == null ? memberList : memberList.Concat(baseClassConstructorArgs))
                .Select(it => $"{it.Type} {it.ParameterName}");
            var nestingStack = GetNestingAncestors(classSymbol);
            var nestingCount = nestingStack.Count;
            var source = new StringBuilder($@"namespace {namespaceName}
{{");
            while (nestingStack.Any())
            {
                var currentNestingAncestor = nestingStack.Pop();

                source.Append($@"
    partial class {currentNestingAncestor.Name}{GetGenericsNamePart(currentNestingAncestor)}
    {{");
            }
            
            source.Append($@"
    partial class {classSymbol.Name}{GetGenericsNamePart(classSymbol)}
    {{
        public {classSymbol.Name}({string.Join(", ", arguments)}){baseConstructorInheritance}
        {{");

            foreach (var item in memberList)
            {
                source.Append($@"
            this.{item.Name} = {item.ParameterName};");
            }

            source.Append(@"
        }
    }");

            for (int i = 0; i < nestingCount; i++)
            {
                source.Append(@"
    }");
            }
            
            source.Append(@"
}
");

            return source.ToString();
        }

        private static bool IsAutoProperty(IPropertySymbol propertySymbol)
        {
            // Get fields declared in the same type as the property
            var fields = propertySymbol.ContainingType.GetMembers().OfType<IFieldSymbol>();

            // Check if one field is associated to
            return fields.Any(field => !field.CanBeReferencedByName && SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, propertySymbol));
        }

        private static List<MemberSymbolInfo> GetMembers(INamedTypeSymbol classSymbol, bool recursive)
        {
            var fieldList = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(x => x.CanBeReferencedByName && !x.IsStatic &&
                            (x.IsReadOnly && !HasFieldInitializer(x) || HasAttribute(x, nameof(IncludePrimaryConstructorAttribute))) && 
                            !HasAttribute(x, nameof(IgnorePrimaryConstructorAttribute)))
                .Select(it => new MemberSymbolInfo
                {
                    Type = it.Type.ToDisplayString(PropertyTypeFormat),
                    ParameterName = ToCamelCase(it.Name),
                    Name = it.Name,
                    Attributes = it.GetAttributes()
                })
                .ToList();

            var props = classSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(x => x.CanBeReferencedByName && !x.IsStatic &&
                            (x.IsReadOnly && !HasPropertyInitializer(x) && IsAutoProperty(x) || 
                                HasAttribute(x, nameof(IncludePrimaryConstructorAttribute))) && 
                            !HasAttribute(x, nameof(IgnorePrimaryConstructorAttribute)))
                .Select(it => new MemberSymbolInfo
                {
                    Type = it.Type.ToDisplayString(PropertyTypeFormat),
                    ParameterName = ToCamelCase(it.Name),
                    Name = it.Name,
                    Attributes = it.GetAttributes()
                });
            fieldList.AddRange(props);

            //context.Compilation.GetSemanticModel();

            if (recursive && classSymbol.BaseType != null && HasAttribute(classSymbol.BaseType, nameof(PrimaryConstructorAttribute)))
            {
                fieldList.AddRange(GetMembers(classSymbol.BaseType, true));
            }

            return fieldList;
        }

        private static string ToCamelCase(string name)
        {
            name = name.TrimStart('_');
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }
        
        private static IEnumerable<INamedTypeSymbol> GetClassSymbols(GeneratorExecutionContext context, SyntaxReceiver receiver)
        {
            var compilation = context.Compilation;

            return from clazz in receiver.CandidateClasses
                let model = compilation.GetSemanticModel(clazz.SyntaxTree)
                select model.GetDeclaredSymbol(clazz)! into classSymbol
                where HasAttribute(classSymbol, nameof(PrimaryConstructorAttribute))
                select classSymbol;
        }

        private static Stack<INamedTypeSymbol> GetNestingAncestors(INamedTypeSymbol classSymbol)
        {
            var stack = new Stack<INamedTypeSymbol>();
            var current = classSymbol.ContainingType;
            while (current is not null)
            {
                stack.Push(current);
                current = current.ContainingType;
            }

            return stack;
        }

        private static string GetGenericsNamePart(INamedTypeSymbol classSymbol) =>
            classSymbol.TypeArguments.Any()
                ? $"<{string.Join(", ", classSymbol.TypeArguments.Select(s => s.ToDisplayString(TypeFormat)))}>"
                : "";
    }
}
