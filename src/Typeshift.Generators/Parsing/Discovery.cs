using System.Linq;
using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// D3: attribute-free; the syntax check passes only classes whose base list names Toolset, Agent or
// JsonSerializerContext, so an edit re-binds just those, and the semantic check confirms the direct base
internal static class Discovery
{
    public static bool IsToolset(SyntaxNode node, CancellationToken ct) => HasBase(node, "Toolset");

    public static bool IsAgent(SyntaxNode node, CancellationToken ct) => HasBase(node, "Agent");

    public static bool IsJsonContext(SyntaxNode node, CancellationToken ct) => HasBase(node, "JsonSerializerContext");

    public static ToolsetModel? ClassifyToolset(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (GetDeclared(context, KnownTypes.Toolset, ct) is not { } type) return null;

        var documented = context.Node.SyntaxTree.Options.DocumentationMode != DocumentationMode.None;
        var constructors = ConstructorChains.GetBlocks(type, context.SemanticModel.Compilation, ct);

        return ToolsetParser.Parse(type, constructors, documented, Reporter.None, ct);
    }

    public static AgentModel? ClassifyAgent(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (GetDeclared(context, KnownTypes.Agent, ct) is not { } type) return null;

        var constructors = ConstructorChains.GetBlocks(type, context.SemanticModel.Compilation, ct);
        return AgentParser.Parse(type, constructors, Reporter.None, ct);
    }

    // D45: the module registers the assembly's JSON contexts, which tools ask before the defaults
    public static string? ClassifyJsonContext(GeneratorSyntaxContext context, CancellationToken ct) =>
        GetDeclared(context, KnownTypes.JsonSerializerContext, ct) is { } type && type.IsVisibleInAssembly()
            ? type.ToTypeOf()
            : null;

    private static bool HasBase(SyntaxNode node, string baseName) =>
        node is ClassDeclarationSyntax { BaseList: { } bases } declaration
        && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && bases.Types.Any(type => RightmostName(type.Type) == baseName);

    // D23: generic classes and classes deriving from another are TSH017, which the analyzer reports
    private static INamedTypeSymbol? GetDeclared(GeneratorSyntaxContext context, string baseType, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        return context.SemanticModel.GetDeclaredSymbol(declaration, ct) is
            {
                IsAbstract: false,
                IsGenericType: false,
                BaseType: var direct
            } type
            && direct.Is(baseType)
            && IsFirstDeclarationWithBaseList(type, declaration, ct)
                ? type
                : null;
    }

    private static string? RightmostName(TypeSyntax type) =>
        type switch
        {
            SimpleNameSyntax name => name.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            _ => null
        };

    // A partial class may list its base on several declarations, the model is built once
    private static bool IsFirstDeclarationWithBaseList(
        INamedTypeSymbol type,
        ClassDeclarationSyntax declaration,
        CancellationToken ct) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(ct))
            .OfType<ClassDeclarationSyntax>()
            .First(candidate => candidate.BaseList is not null) == declaration;
}
