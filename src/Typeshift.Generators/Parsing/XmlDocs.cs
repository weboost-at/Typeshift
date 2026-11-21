using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Typeshift.Generators.Parsing;

// A method's <summary> and <param> docs; the compiler parses them only with GenerateDocumentationFile on
internal sealed class XmlDocs
{
    private static readonly XmlDocs None = new(null, new Dictionary<string, string>());

    private static readonly string[] Blocks = ["para", "list", "item", "term", "description", "code", "br"];

    private XmlDocs(string? summary, IReadOnlyDictionary<string, string> parameters)
    {
        Summary = summary;
        Parameters = parameters;
    }

    public string? Summary { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    // D3: every edit parses every toolset and agent, and the compiler's XML of a comment costs a string and its parse
    // each time, so a comment of the compilation is read where it is written. Both readings give the same text
    public static XmlDocs Read(ISymbol symbol, CancellationToken ct) =>
        ReadWritten(symbol, ct) ?? ReadPlain(symbol, ct) ?? ReadCompiled(symbol, ct);

    // The compiler's XML: the docs of a referenced assembly, and every comment whose reading in place could differ
    public static XmlDocs ReadCompiled(ISymbol symbol, CancellationToken ct)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: ct);
        return xml is null || string.IsNullOrWhiteSpace(xml) ? None : Parse(xml);
    }

    // D68: with GenerateDocumentationFile off the compiler keeps doc comments as plain comments and writes no XML of
    // them, though they describe tools and agents all the same; their text, without the delimiters, is that XML, and
    // a cref reads as it is written. A partial class has the comments of each of its declarations, as the compiler's
    // XML does, so an agent reads the same beside its generated part. Null where the compiler parses the comments
    public static XmlDocs? ReadPlain(ISymbol symbol, CancellationToken ct)
    {
        var declarations = symbol.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(ct)).ToList();

        if (declarations.Count == 0 || declarations[0].SyntaxTree.Options.DocumentationMode != DocumentationMode.None)
            return null;

        var lines = declarations
            .SelectMany(declaration => declaration.GetLeadingTrivia())
            .Where(IsDocumentation)
            .SelectMany(GetLines)
            .ToList();

        return lines.Count == 0 ? None : Parse($"<doc>{string.Join("\n", lines)}</doc>");
    }

    // A /// line without its slashes, or the lines of a /** */ comment without the stars that start them
    private static IEnumerable<string> GetLines(SyntaxTrivia trivia)
    {
        var text = trivia.ToString();

        return text.StartsWith("/**", StringComparison.Ordinal)
            ? text.Substring(3, text.Length - 5).Split('\n').Select(line => line.Trim().TrimStart('*'))
            : text.Split('\n').Select(line => line.Trim()).Where(IsLine).Select(line => line.Substring(3));
    }

    // As the compiler reads them, //// starts a plain comment
    private static bool IsLine(string line) =>
        line.StartsWith("///", StringComparison.Ordinal) && !line.StartsWith("////", StringComparison.Ordinal);

    private static XmlDocs Parse(string xml)
    {
        try
        {
            // Kept, or the space between two elements, as in <paramref name="a"/> <paramref name="b"/>, is lost
            var root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);

            // An element without a text, such as the <summary></summary> an editor writes for ///, describes nothing
            return new XmlDocs(
                root.Element("summary") is { } summary && GetText(summary) is { Length: > 0 } text ? text : null,
                root.Elements("param")
                    .Where(param => param.Attribute("name") is not null)
                    .GroupBy(param => param.Attribute("name")!.Value)
                    .Select(group => (group.Key, Text: GetText(group.First())))
                    .Where(param => param.Text.Length > 0)
                    .ToDictionary(param => param.Key, param => param.Text));
        }
        catch (XmlException)
        {
            return None;
        }
    }

    // The comment of a declaration in this compilation, read as ReadCompiled reads the compiler's XML of it. Null
    // where the compiler writes what the comment alone does not say: the docs of a positional record's property come
    // from the record, a partial declaration's from several, a comment it warns about may be written otherwise, and
    // so is a cref that names no definition
    public static XmlDocs? ReadWritten(ISymbol symbol, CancellationToken ct)
    {
        if (symbol.DeclaringSyntaxReferences.Length != 1
            || symbol.ContainingAssembly is not ISourceAssemblySymbol { Compilation: var compilation })
            return null;

        var declaration = symbol.DeclaringSyntaxReferences[0].GetSyntax(ct);

        if (declaration is not (MethodDeclarationSyntax or PropertyDeclarationSyntax or BaseTypeDeclarationSyntax)
            || declaration.SyntaxTree.Options.DocumentationMode != DocumentationMode.Diagnose)
            return null;

        var comments = declaration.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .ToList();

        if (comments.Count == 0) return None;
        if (comments.Count > 1 || comments[0].ContainsDiagnostics) return null;

        var reader = new Comment(compilation, declaration.SyntaxTree, ct);
        var content = comments[0].Content;

        var summary = Elements(content, "summary").Select(reader.GetText).FirstOrDefault();

        var parameters = Elements(content, "param")
            .Select(param => (Name: reader.GetAttribute(param, "name"), Param: param))
            .Where(param => param.Name is not null)
            .GroupBy(param => param.Name!)
            .Select(group => (group.Key, Text: reader.GetText(group.First().Param)))
            .Where(param => param.Text.Length > 0)
            .ToDictionary(param => param.Key, param => param.Text);

        return reader.Unresolved ? null : new XmlDocs(summary is { Length: > 0 } ? summary : null, parameters);
    }

    // With DocumentationMode.None the compiler keeps /// comments as plain comments and returns no XML
    public static string ReadUnparsed(ISymbol symbol, CancellationToken ct) =>
        string.Concat(symbol.DeclaringSyntaxReferences
            .SelectMany(reference => reference.GetSyntax(ct).GetLeadingTrivia())
            .Where(IsDocumentation)
            .Select(trivia => trivia.ToString()));

    // As the compiler reads them, a /** */ comment is one too, but neither /**/ nor /*** */
    private static bool IsDocumentation(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            && trivia.ToString().StartsWith("///", StringComparison.Ordinal))
        || (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            && trivia.ToString() is var comment
            && comment.StartsWith("/**", StringComparison.Ordinal)
            && !comment.StartsWith("/***", StringComparison.Ordinal)
            && comment != "/**/");

    private static string GetText(XElement element) => Collapse(string.Concat(element.Nodes().Select(GetText)));

    // <see cref="..."/>, <see langword="..."/> and <paramref name="..."/> read as the name they point at, a link
    // without a text as its address; the text of a block stands apart from what is next to it
    private static string GetText(XNode node) =>
        node switch
        {
            XText text => text.Value,
            XElement element when element.Attribute("cref") is { } cref => GetShortName(cref.Value),
            XElement element when element.Attribute("langword") is { } langword => langword.Value,
            XElement element when element.Attribute("name") is { } name => name.Value,
            XElement { IsEmpty: true } element when element.Attribute("href") is { } href => href.Value,
            XElement element when Blocks.Contains(element.Name.LocalName) => $" {GetText(element)} ",
            XElement element => GetText(element),
            _ => ""
        };

    private static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // T:Ns.Type, T:Ns.List`1, M:Ns.Type.Method``1(System.String) and the like read as their last segment, and so do
    // List{T} and Type.Method(string) as a comment the compiler does not parse writes them
    private static string GetShortName(string cref)
    {
        var name = cref.Split('(', '`', '{')[0];
        return name.Substring(name.LastIndexOfAny([':', '.']) + 1);
    }

    // The elements of the name at the top of the comment, as XElement.Elements finds them in the compiler's XML
    private static IEnumerable<XmlNodeSyntax> Elements(SyntaxList<XmlNodeSyntax> content, string name) =>
        content.Where(node => GetName(node) is { Prefix: null } tag && tag.LocalName.ValueText == name);

    private static XmlNameSyntax? GetName(XmlNodeSyntax node) =>
        node switch
        {
            XmlElementSyntax element => element.StartTag.Name,
            XmlEmptyElementSyntax empty => empty.Name,
            _ => null
        };

    private static SyntaxList<XmlAttributeSyntax> GetAttributes(XmlNodeSyntax node) =>
        node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax empty => empty.Attributes,
            _ => default
        };

    private static string GetValue(SyntaxTokenList tokens) => string.Concat(tokens.Select(token => token.ValueText));

    // The syntax of one comment read by the rules of the XML reading above; a cref binds in the comment's own tree
    private sealed class Comment(Compilation compilation, SyntaxTree tree, CancellationToken ct)
    {
        private SemanticModel? model;

        // A cref that binds to no symbol with an id, which the compiler writes in a form of its own
        public bool Unresolved { get; private set; }

        public string GetText(XmlNodeSyntax element) => Collapse(GetContent(element));

        public string? GetAttribute(XmlNodeSyntax element, string name) =>
            GetAttributes(element).FirstOrDefault(attribute => attribute.Name is { Prefix: null } named
                && named.LocalName.ValueText == name) switch
            {
                XmlCrefAttributeSyntax cref => GetId(cref.Cref),
                XmlNameAttributeSyntax named => named.Identifier.Identifier.Text,
                XmlTextAttributeSyntax text => GetValue(text.TextTokens),
                _ => null
            };

        private string GetContent(XmlNodeSyntax element) =>
            element is XmlElementSyntax { Content: var content }
                ? string.Concat(content.Select(GetNodeText))
                : "";

        private string GetNodeText(XmlNodeSyntax node) =>
            node switch
            {
                XmlTextSyntax text => GetValue(text.TextTokens),
                XmlCDataSectionSyntax data => GetValue(data.TextTokens),
                XmlElementSyntax or XmlEmptyElementSyntax => GetElementText(node),
                _ => ""
            };

        private string GetElementText(XmlNodeSyntax element) =>
            GetAttribute(element, "cref") is { } cref ? GetShortName(cref)
            : GetAttribute(element, "langword") is { } langword ? langword
            : GetAttribute(element, "name") is { } name ? name
            : element is XmlEmptyElementSyntax && GetAttribute(element, "href") is { } href ? href
            : Blocks.Contains(GetName(element)!.LocalName.ValueText) ? $" {GetText(element)} "
            : GetText(element);

        // List{T} binds to List<T> of a T of the cref's own, for which the compiler writes the definition's id; a
        // type argument of any other kind, as an alias of List<int> gives, it writes in a form of its own
        private string? GetId(CrefSyntax cref)
        {
            model ??= compilation.GetSemanticModel(tree);

            var symbol = model.GetSymbolInfo(cref, ct).Symbol;

            var id = symbol is INamespaceSymbol
                || (symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol
                    && IsDefinition(symbol))
                    ? symbol.OriginalDefinition.GetDocumentationCommentId()
                    : null;

            Unresolved |= id is null;
            return id ?? "";
        }

        private static bool IsDefinition(ISymbol symbol) =>
            symbol switch
            {
                INamedTypeSymbol type => type.TypeArguments.All(argument => argument is ITypeParameterSymbol)
                    && (type.ContainingType is null || IsDefinition(type.ContainingType)),
                IMethodSymbol method => method.TypeArguments.All(argument => argument is ITypeParameterSymbol)
                    && IsDefinition(method.ContainingType),
                _ => symbol.ContainingType is null || IsDefinition(symbol.ContainingType)
            };
    }
}
