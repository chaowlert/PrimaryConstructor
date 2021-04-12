using System;
using System.Collections.Generic;
using System.Linq;
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
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) 
                return;

            var classSymbols = GetClassSymbols(context, receiver);
            var classNames = new Dictionary<string, int>();
            foreach (var classSymbol in classSymbols)
            {
                classNames.TryGetValue(classSymbol.Name, out var i);
                var name = i == 0 ? classSymbol.Name : $"{classSymbol.Name}{i + 1}";
                classNames[classSymbol.Name] = i + 1;
                context.AddSource($"{name}.PrimaryConstructor.g.cs",
                    SourceText.From(CreatePrimaryConstructor(classSymbol, context), Encoding.UTF8));
            }
        }

        private static bool HasInitializer(IFieldSymbol symbol)
        {
            var field = symbol.DeclaringSyntaxReferences.ElementAtOrDefault(0)?.GetSyntax() as VariableDeclaratorSyntax;
            return field?.Initializer != null;
        }
        
        private static bool HasInitializer(IPropertySymbol symbol)
        {
            var field = symbol.DeclaringSyntaxReferences.ElementAtOrDefault(0)?.GetSyntax() as VariableDeclaratorSyntax;
            return field?.Initializer != null;
        }

        private static bool HasIgnoreAttribute(ISymbol symbol) => symbol
            .GetAttributes()
            .Any(x => x.AttributeClass.Name == typeof(IgnorePrimaryConstructorAttribute).Name);

        private static readonly SymbolDisplayFormat TypeFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );
        private static readonly SymbolDisplayFormat PropertyTypeFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );
        private static string CreatePrimaryConstructor(INamedTypeSymbol classSymbol,
            GeneratorExecutionContext context)
        {
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            var baseClassConstructorArgs = GetMembers(classSymbol.BaseType, context, true);
            var baseConstructorInheritance = baseClassConstructorArgs.Count <= 0
                ? ""
                : $" : base({string.Join(", ", baseClassConstructorArgs.Select(it => it.ParameterName))})";
            
            var memberList = GetMembers(classSymbol, context, false);
            var arguments = memberList
                .Select(it => $"{it.Type} {it.ParameterName}")
                .Union(baseClassConstructorArgs.Select(it => $"{it.Type} {it.ParameterName}"));
            var fullTypeName = classSymbol.ToDisplayString(TypeFormat);
            var i = fullTypeName.IndexOf('<');
            var generic = i < 0 ? "" : fullTypeName.Substring(i);
            var source = new StringBuilder($@"namespace {namespaceName}
{{
    partial class {classSymbol.Name}{generic}
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
    }
}
");

            source.AppendLine($"/* {DateTime.Now}");
            source.AppendLine($"BaseType: {classSymbol.BaseType.ContainingNamespace}.{classSymbol.BaseType.Name}");
            if (classSymbol.BaseType != null)
            {
                foreach (var c in classSymbol.BaseType.Constructors)
                {
                    source.AppendLine("Constructor: " + c.Parameters.Length);
                    foreach (var p in c.Parameters)
                    {
                        source.AppendLine($"  {p.Name}: {p.Type}");
                    }
                }
            }

            foreach (var member in memberList)
            {
                source.AppendLine($"{member.Name} = {member.Attributes.Count()}");
                foreach (var att in member.Attributes)
                {
                    source.AppendLine($"  {att.AttributeClass.Name}");
                    source.AppendLine($"  {typeof(IgnorePrimaryConstructorAttribute).Name}");
                }
            }

            source.AppendLine("*/");
            
            return source.ToString();
        }

        private static List<MemberSymbolInfo> GetMembers(INamedTypeSymbol classSymbol,
            GeneratorExecutionContext context, bool recursive)
        {
            var fieldList = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(x => x.CanBeReferencedByName && x.IsReadOnly && !x.IsStatic && 
                            !HasInitializer(x) && !HasIgnoreAttribute(x))
                .Select(it => new MemberSymbolInfo
                {
                    Type = it.Type.ToDisplayString(PropertyTypeFormat), 
                    ParameterName = ToCamelCase(it.Name), 
                    Name = it.Name,
                    Attributes = it.GetAttributes()
                })
                .ToList();
            
            var props = classSymbol.GetMembers().OfType<IPropertySymbol>()
                .Where(x => x.CanBeReferencedByName && x.IsReadOnly && !x.IsStatic && 
                            !HasInitializer(x) && !HasIgnoreAttribute(x))
                .Select(it => new MemberSymbolInfo
                {
                    Type = it.Type.ToDisplayString(PropertyTypeFormat), 
                    ParameterName = ToCamelCase(it.Name), 
                    Name = it.Name,
                    Attributes = it.GetAttributes()
                })
                .ToList();
            fieldList.AddRange(props);

            //context.Compilation.GetSemanticModel();

            if (recursive && classSymbol.BaseType != null && 
                $"{classSymbol.BaseType.ContainingNamespace}.{classSymbol.BaseType.Name}" != "System.Object")
            {
                fieldList.AddRange(GetMembers(classSymbol.BaseType, context, true));
            }

            return fieldList;
        }

        private static string ToCamelCase(string name)
        {
            name = name.TrimStart('_');
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }

        private static List<INamedTypeSymbol> GetClassSymbols(GeneratorExecutionContext context, SyntaxReceiver receiver)
        {
            var compilation = context.Compilation;
            var classSymbols = new List<INamedTypeSymbol>();
            foreach (var clazz in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(clazz.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(clazz)!;
                if (classSymbol!.GetAttributes().Any(ad => ad.AttributeClass!.Name == "PrimaryConstructorAttribute"))
                {
                    classSymbols.Add(classSymbol);
                }
            }

            return classSymbols;
        }
    }
}
