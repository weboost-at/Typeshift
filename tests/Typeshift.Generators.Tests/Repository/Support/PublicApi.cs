using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Typeshift;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Tests.Repository.Support;

// The library's public API as its consumers see it: the built assembly with its XML documentation file
public static class PublicApi
{
    // Every name in full, so a line reads the same wherever its type moves
    private static readonly SymbolDisplayFormat Name = Format(SymbolDisplayDelegateStyle.NameOnly);

    private static readonly SymbolDisplayFormat Inheritance = Format(SymbolDisplayDelegateStyle.NameAndSignature)
        .AddMemberOptions(
            SymbolDisplayMemberOptions.IncludeAccessibility
            | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeConstantValue)
        .AddParameterOptions(
            SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue)
        .AddKindOptions(SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword);

    private static readonly SymbolDisplayFormat Declaration =
        Inheritance.AddGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeConstraints);

    private static readonly IAssemblySymbol Library = Load();

    // Every visible type and member; a property stands for its accessors and a delegate for its members
    public static IReadOnlyList<ISymbol> Members { get; } =
    [
        .. Types(Library.GlobalNamespace)
            .SelectMany(type => type.GetMembers().Where(member => member is not INamedTypeSymbol).Prepend(type))
            .Where(IsVisible)
            .Where(member => member is not IMethodSymbol { AssociatedSymbol: not null })
            .Where(member => member.ContainingType?.TypeKind is not TypeKind.Delegate)
            .Where(member => !member.IsImplicitlyDeclared && !member.Name.StartsWith('<'))
    ];

    public static IReadOnlyList<ISymbol> DocumentedMembers { get; } =
        [.. Members.Where(member => !string.IsNullOrEmpty(member.GetDocumentationCommentXml()))];

    // D7: what generated code in a consumer binds to: the hidden namespace, what a Settings record implements and
    // the declaration an agent overrides
    public static IReadOnlyList<ISymbol> GeneratedContract { get; } =
    [
        .. Members.Where(member =>
            member.ContainingNamespace.ToDisplayString() == "Typeshift.Generated"
            || (member as INamedTypeSymbol ?? member.ContainingType).MetadataName == "IStorable`1"
            || member is IPropertySymbol { Name: "Declaration", ContainingType.Name: "Agent" })
    ];

    public static XElement Docs(ISymbol member) => XElement.Parse(member.GetDocumentationCommentXml()!);

    // One declaration a line, in the order of the members' full names
    public static string Render(IEnumerable<ISymbol> members) =>
        string.Join(
            '\n',
            members
                .OrderBy(member => member.ToDisplayString(Name), StringComparer.Ordinal)
                .Select(Declare));

    private static IAssemblySymbol Load()
    {
        var path = typeof(Toolset).Assembly.Location;
        var library = MetadataReference.CreateFromFile(
            path,
            documentation: new XmlDocumentation(Path.ChangeExtension(path, ".xml")));

        var framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(reference => Path.GetFileName(reference) != Path.GetFileName(path))
            .Select(reference => MetadataReference.CreateFromFile(reference));

        var compilation = CSharpCompilation.Create("PublicApi", references: [.. framework, library]);

        return (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(library)!;
    }

    private static SymbolDisplayFormat Format(SymbolDisplayDelegateStyle delegates) =>
        new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
                | SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeExplicitInterface
                | SymbolDisplayMemberOptions.IncludeParameters,
            delegateStyle: delegates,
            extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
            parameterOptions: SymbolDisplayParameterOptions.IncludeModifiers
                | SymbolDisplayParameterOptions.IncludeType,
            propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    // What no display format states: a type's modifiers and what it derives from, ahead of its constraints, and
    // whether an interface's member is static or abstract
    private static string Declare(ISymbol member) =>
        member switch
        {
            INamedTypeSymbol type => Declare(type),
            { ContainingType.TypeKind: TypeKind.Interface } =>
                $"{(member.IsStatic ? "static " : "")}{(member.IsAbstract ? "abstract " : "")}"
                + member.ToDisplayString(Declaration),
            _ => member.ToDisplayString(Declaration)
        };

    private static string Declare(INamedTypeSymbol type)
    {
        var name = type.ToDisplayString(Inheritance);
        var parents = type.Interfaces.Prepend(type.BaseType)
            .Where(parent => parent is { SpecialType: SpecialType.None } && IsVisible(parent))
            .Select(parent => parent!.ToDisplayString(Name))
            .ToList();

        var modifier = type switch
        {
            { IsStatic: true } => "static ",
            { TypeKind: TypeKind.Class, IsAbstract: true } => "abstract ",
            { TypeKind: TypeKind.Class, IsSealed: true } => "sealed ",
            _ => ""
        };

        return $"{SyntaxFacts.GetText(type.DeclaredAccessibility)} {modifier}{name}"
            + (parents.Count > 0 ? $" : {string.Join(", ", parents)}" : "")
            + type.ToDisplayString(Declaration)[name.Length..];
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container) =>
        container.GetMembers().SelectMany(member => member switch
        {
            INamespaceSymbol nameSpace => Types(nameSpace),
            INamedTypeSymbol type when IsVisible(type) => Types(type).Prepend(type),
            _ => []
        });

    private static bool IsVisible(ISymbol member) =>
        member.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected
            or Accessibility.ProtectedOrInternal;

    // Reads the <member> entries of an XML documentation file, as the IDE does for a referenced assembly
    private sealed class XmlDocumentation(string path) : DocumentationProvider
    {
        private readonly Dictionary<string, string> _members = XDocument.Load(path)
            .Descendants("member")
            .ToDictionary(member => (string)member.Attribute("name")!, member => member.ToString());

        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        protected override string? GetDocumentationForSymbol(
            string documentationMemberID,
            CultureInfo preferredCulture,
            CancellationToken cancellationToken = default) =>
            _members.GetValueOrDefault(documentationMemberID);
    }
}
