using System.Linq;
using System.Threading;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Agentic.Generators.Parsing;

// Attribute-free discovery: a cheap syntax check, then the base type decides
internal static class Discovery
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken ct) =>
        node is ClassDeclarationSyntax { BaseList: not null } declaration
        && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword);

    public static ClassModel? Classify(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, ct);

        // D6: a generic class's tools depend on type arguments only the closing site knows
        if (symbol is not { IsAbstract: false, IsGenericType: false } type) return null;
        if (!IsFirstDeclarationWithBaseList(type, declaration, ct)) return null;

        var documented = declaration.SyntaxTree.Options.DocumentationMode != DocumentationMode.None;

        var compilation = context.SemanticModel.Compilation;

        if (type.DerivesFrom(KnownTypes.Toolset))
            return new ClassModel(ToolsetParser.Parse(type, compilation, documented, ct), null);

        return type.DerivesFrom(KnownTypes.AgentDefinition)
            ? new ClassModel(null, AgentParser.Parse(type, compilation, ct))
            : null;
    }

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
