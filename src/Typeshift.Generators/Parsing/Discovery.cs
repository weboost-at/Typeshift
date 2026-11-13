using System.Linq;
using System.Threading;
using Typeshift.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// D3: attribute-free; the syntax check passes only classes whose base list names Toolset, Agent or
// JsonSerializerContext, so an edit re-binds just those, and the semantic check confirms the direct base. A base
// named through a using alias is not found, which the analyzer reports (TSH017)
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

        var documented = context.Node.SyntaxTree.Options.DocumentationMode != DocumentationMode.None;
        var constructors = ConstructorChains.GetBlocks(type, context.SemanticModel.Compilation, ct);

        return AgentParser.Parse(type, constructors, documented, Reporter.None, ct);
    }

    // D45: the module registers the assembly's JSON contexts, which tools ask before the defaults
    public static string? ClassifyJsonContext(GeneratorSyntaxContext context, CancellationToken ct) =>
        GetDeclared(context, KnownTypes.JsonSerializerContext, ct) is { } type && CanRegister(type, ct)
            ? type.ToTypeOf()
            : null;

    // D45: the module names the context and reads its static Default. System.Text.Json's generator writes that
    // member for a partial context with a [JsonSerializable], where this generator cannot see it; any other context
    // has to declare it
    public static bool CanRegister(INamedTypeSymbol context, CancellationToken ct) =>
        context.IsVisibleInAssembly()
        && (context.GetMembers("Default").Any(IsDefault)
            || (context.GetNotPartial(ct) is null
                && context.GetAttributes()
                    .Any(attribute => attribute.AttributeClass.Is(KnownTypes.JsonSerializableAttribute))));

    // D55: the module registers the docs Weboost.SchemaAnnotations collects for a marked class, which gets a Source
    // only when it is partial and not generic
    public static string? ClassifySchemaAnnotations(GeneratorAttributeSyntaxContext context, CancellationToken ct) =>
        context.TargetNode is ClassDeclarationSyntax declaration
        && context.TargetSymbol is INamedTypeSymbol { IsGenericType: false } type
        && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        && type.IsVisibleInAssembly()
            ? type.ToTypeOf()
            : null;

    // D3: the base as a class names it through a using alias, where the syntax check does not find the class; null
    // for a class one of whose declarations names the base itself
    public static Location? GetAliasedBase(INamedTypeSymbol type, string baseName, CancellationToken ct)
    {
        var declarations = type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(ct))
            .OfType<ClassDeclarationSyntax>()
            .ToList();

        if (declarations.Any(declaration => HasBase(declaration, baseName))) return null;

        // The base class comes first in a base list, and a partial declaration may list interfaces alone
        var alias = declarations
            .Select(declaration => declaration.BaseList?.Types.FirstOrDefault()?.Type)
            .OfType<TypeSyntax>()
            .FirstOrDefault(named => type.Interfaces.All(contract => contract.Name != RightmostName(named)));

        return alias?.GetLocation() ?? type.Locations.FirstOrDefault();
    }

    private static bool IsDefault(ISymbol member) =>
        member is
        {
            IsStatic: true,
            DeclaredAccessibility: Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
        }
        && member switch
        {
            IPropertySymbol property => IsJsonContext(property.Type),
            IFieldSymbol field => IsJsonContext(field.Type),
            _ => false
        };

    private static bool IsJsonContext(ITypeSymbol type) =>
        type is INamedTypeSymbol named
        && (named.Is(KnownTypes.JsonSerializerContext) || named.DerivesFrom(KnownTypes.JsonSerializerContext));

    private static bool HasBase(SyntaxNode node, string baseName) =>
        node is ClassDeclarationSyntax { BaseList: { } bases } declaration
        && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
        && bases.Types.Any(type => RightmostName(type.Type) == baseName);

    // D23: generic classes and classes deriving from another are TSH017, which the analyzer reports
    private static INamedTypeSymbol? GetDeclared(GeneratorSyntaxContext context, string baseType, CancellationToken ct)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        var type = context.SemanticModel.GetDeclaredSymbol(declaration, ct);

        return type is { IsAbstract: false, IsGenericType: false, BaseType: var direct }
            && direct.Is(baseType)
            && IsFirstDeclarationWithBase(type, declaration, baseType.Substring(baseType.LastIndexOf('.') + 1), ct)
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

    // A partial class may name its base on several declarations, and list interfaces on others; the model is built
    // once, from the first that names the base
    private static bool IsFirstDeclarationWithBase(
        INamedTypeSymbol type,
        ClassDeclarationSyntax declaration,
        string baseName,
        CancellationToken ct) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(ct))
            .OfType<ClassDeclarationSyntax>()
            .First(candidate => HasBase(candidate, baseName)) == declaration;
}
