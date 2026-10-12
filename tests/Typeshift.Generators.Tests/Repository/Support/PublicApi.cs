using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Typeshift.Agents;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Tests.Repository.Support;

// The library's public API as its consumers see it: the built assembly with its XML documentation file
public static class PublicApi
{
    public static IReadOnlyList<ISymbol> DocumentedMembers { get; } = FindDocumentedMembers();

    public static XElement Docs(ISymbol member) => XElement.Parse(member.GetDocumentationCommentXml()!);

    private static List<ISymbol> FindDocumentedMembers()
    {
        var path = typeof(AgentDefinition).Assembly.Location;
        var library = MetadataReference.CreateFromFile(
            path,
            documentation: new XmlDocumentation(Path.ChangeExtension(path, ".xml")));

        var framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(reference => Path.GetFileName(reference) != Path.GetFileName(path))
            .Select(reference => MetadataReference.CreateFromFile(reference));

        var compilation = CSharpCompilation.Create("PublicApi", references: [.. framework, library]);
        var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(library)!;

        return
        [
            .. Types(assembly.GlobalNamespace)
                .SelectMany(type => type.GetMembers().Prepend(type))
                .Where(IsVisible)
                .Where(member => !string.IsNullOrEmpty(member.GetDocumentationCommentXml()))
        ];
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
