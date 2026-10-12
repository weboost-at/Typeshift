using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Typeshift.Generators.Parsing;

// A method's <summary> and <param> docs; the compiler parses them only with GenerateDocumentationFile on
internal sealed class XmlDocs
{
    private static readonly XmlDocs None = new(null, new Dictionary<string, string>());

    private XmlDocs(string? summary, IReadOnlyDictionary<string, string> parameters)
    {
        Summary = summary;
        Parameters = parameters;
    }

    public string? Summary { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public static XmlDocs Read(ISymbol symbol, CancellationToken ct)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(xml)) return None;

        try
        {
            var root = XElement.Parse(xml);

            return new XmlDocs(
                root.Element("summary") is { } summary ? GetText(summary) : null,
                root.Elements("param")
                    .Where(param => param.Attribute("name") is not null)
                    .GroupBy(param => param.Attribute("name")!.Value)
                    .ToDictionary(group => group.Key, group => GetText(group.First())));
        }
        catch (XmlException)
        {
            return None;
        }
    }

    // With DocumentationMode.None the compiler keeps /// comments as plain comments and returns no XML
    public static string ReadUnparsed(ISymbol symbol, CancellationToken ct) =>
        string.Concat(symbol.DeclaringSyntaxReferences
            .SelectMany(reference => reference.GetSyntax(ct).GetLeadingTrivia())
            .Where(IsDocumentation)
            .Select(trivia => trivia.ToString()));

    private static bool IsDocumentation(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            && trivia.ToString().StartsWith("///", StringComparison.Ordinal));

    private static string GetText(XElement element)
    {
        var text = string.Concat(element.Nodes().Select(GetText));
        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // <see cref="..."/>, <see langword="..."/> and <paramref name="..."/> read as the name they point at
    private static string GetText(XNode node) =>
        node switch
        {
            XText text => text.Value,
            XElement element when element.Attribute("cref") is { } cref => GetShortName(cref.Value),
            XElement element when element.Attribute("langword") is { } langword => langword.Value,
            XElement element when element.Attribute("name") is { } name => name.Value,
            XElement element => GetText(element),
            _ => ""
        };

    // T:Ns.Type, M:Ns.Type.Method(System.String) and the like read as their last segment
    private static string GetShortName(string cref)
    {
        var name = cref.Split('(')[0];
        return name.Substring(name.LastIndexOfAny([':', '.']) + 1);
    }
}
