using System.Linq;
using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// D3: attribute-free; the syntax check passes only classes whose base list names Toolset, so an edit re-binds just
// those, and the semantic check confirms the direct base
internal static class Discovery
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken ct) =>
        node is ClassDeclarationSyntax { BaseList: { } bases } declaration
        && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && bases.Types.Any(type => RightmostName(type.Type) == "Toolset");

    public static ToolsetModel? Classify(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        // D23: generic toolsets and toolsets deriving from another are TSH017, which the analyzer reports
        if (context.SemanticModel.GetDeclaredSymbol(declaration, ct) is not
            {
                IsAbstract: false,
                IsGenericType: false,
                BaseType: var baseType
            } type
            || !baseType.Is(KnownTypes.Toolset)
            || !IsFirstDeclarationWithBaseList(type, declaration, ct)) return null;

        var documented = declaration.SyntaxTree.Options.DocumentationMode != DocumentationMode.None;
        var constructors = ConstructorChains.GetBlocks(type, context.SemanticModel.Compilation, ct);

        return ToolsetParser.Parse(type, constructors, documented, Reporter.None, ct);
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
