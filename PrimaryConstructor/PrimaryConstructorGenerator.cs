using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PrimaryConstructor
{
    [Generator]
    public class PrimaryConstructorGenerator : ISourceGenerator
    {
        private const string primaryConstructorAttributeText = @"using System;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class PrimaryConstructorAttribute : Attribute
{
}
";
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            InjectPrimaryConstructorAttributes(context);

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) 
                return;

            var classSymbols = GetClassSymbols(context, receiver);
            foreach (var classSymbol in classSymbols)
            {
                context.AddSource($"{classSymbol.Name}.PrimaryConstructor.g.cs",
                    SourceText.From(CreatePrimaryConstructor(classSymbol), Encoding.UTF8));
            }
        }

        private static bool HasInitializer(IFieldSymbol symbol)
        {
            var field = symbol.DeclaringSyntaxReferences.ElementAtOrDefault(0)?.GetSyntax() as VariableDeclaratorSyntax;
            return field?.Initializer != null;
        }

        private static string CreatePrimaryConstructor(INamedTypeSymbol classSymbol)
        {
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            var fieldList = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(x => x.CanBeReferencedByName && x.IsReadOnly && !x.IsStatic && !HasInitializer(x))
                .Select(it => new { Type = it.Type.ToDisplayString(), ParameterName = ToCamelCase(it.Name), it.Name })
                .ToList();
            var arguments = fieldList.Select(it => $"{it.Type} {it.ParameterName}");
            var source = new StringBuilder($@"namespace {namespaceName}
{{
    partial class {classSymbol.Name}
    {{
        public {classSymbol.Name}({string.Join(", ", arguments)})
        {{");

            foreach (var item in fieldList)
            {
                source.Append($@"
            this.{item.Name} = {item.ParameterName};");
            }
            source.Append(@"
        }
    }
}
");
            return source.ToString();
        }

        private static string ToCamelCase(string name)
        {
            name = name.TrimStart('_');
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }

        private static List<INamedTypeSymbol> GetClassSymbols(GeneratorExecutionContext context, SyntaxReceiver receiver)
        {
            var options = ((CSharpCompilation) context.Compilation).SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(primaryConstructorAttributeText, Encoding.UTF8), options));

            var attributeSymbol = compilation.GetTypeByMetadataName("PrimaryConstructorAttribute")!;

            var classSymbols = new List<INamedTypeSymbol>();
            foreach (var clazz in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(clazz.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(clazz)!;
                if (classSymbol!.GetAttributes().Any(ad => ad.AttributeClass!.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                {
                    classSymbols.Add(classSymbol);
                }
            }

            return classSymbols;
        }

        private static void InjectPrimaryConstructorAttributes(GeneratorExecutionContext context)
        {
            context.AddSource("PrimaryConstructorAttribute.g.cs", SourceText.From(primaryConstructorAttributeText, Encoding.UTF8));
        }
    }
}
