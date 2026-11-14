using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Typeshift.Generators.Analysis;
using Typeshift.Generators.Diagnostics;
using Typeshift.Generators.Tests.Diagnostics.Support;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Diagnostics;

public partial class DiagnosticCatalogTests
{
    // What the analyzer may report; the RS2000 analyzer keeps the release tracking files in step with it
    private static readonly ImmutableArray<DiagnosticDescriptor> Catalog = new TypeshiftAnalyzer().SupportedDiagnostics;

    private static readonly IReadOnlyList<string> Ids = [.. Catalog.Select(descriptor => descriptor.Id)];

    // Each descriptor by the name of its field, which is how the analyzer's source names it
    private static readonly IReadOnlyDictionary<DiagnosticDescriptor, string> Fields = typeof(Descriptors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(field => (DiagnosticDescriptor)field.GetValue(null)!, field => field.Name);

    // D49: the IDs a tag once shipped and no diagnostic has any more, which the docs keep in a table of their own
    private static readonly IReadOnlyList<string> Retired =
    [
        .. RetiredRow()
            .Matches(RepositoryFiles.Read("docs/guide/diagnostics.md"))
            .Select(match => match.Groups["id"].Value)
    ];

    [Fact]
    public void Every_Diagnostic_Has_A_Row_And_A_Section_In_The_Docs()
    {
        // Arrange
        var docs = RepositoryFiles.Read("docs/guide/diagnostics.md");

        // Act
        var undocumented = Ids.Where(id =>
            !docs.Contains($"| [{id}](#{id.ToLowerInvariant()}) |", StringComparison.Ordinal)
            || !docs.Contains($"\n## {id}\n", StringComparison.Ordinal));

        // Assert
        undocumented.Should().BeEmpty();
    }

    [Fact]
    public void Every_Diagnostic_Links_Its_Docs_Section_And_Matches_Its_Row()
    {
        // Arrange
        var docs = RepositoryFiles.Read("docs/guide/diagnostics.md");

        // Act
        var drifted = Catalog
            .Where(descriptor =>
                !descriptor.HelpLinkUri.EndsWith($"/guide/diagnostics#{Anchor(descriptor)}", StringComparison.Ordinal)
                || !docs.Contains(RowOf(descriptor), StringComparison.Ordinal))
            .Select(descriptor => descriptor.Id);

        // Assert
        drifted.Should().BeEmpty("the editor's help link opens the section, and the table states what is reported");
    }

    [Fact]
    public void Every_Diagnostic_Has_A_Test_Named_After_It()
    {
        // Arrange
        var tests = typeof(DiagnosticTests).GetMethods().Select(method => method.Name).ToList();

        // Act
        var untested = Ids.Where(id =>
            !tests.Any(test => test.Contains($"_{id}_Is_Reported", StringComparison.Ordinal)));

        // Assert
        untested.Should().BeEmpty();
    }

    [Fact]
    public void Every_Number_Up_To_The_Highest_Is_A_Diagnostic_Or_A_Retired_Row_In_The_Docs()
    {
        // Arrange
        var highest = Ids.Concat(Retired).Max(id => int.Parse(id[3..]));

        // Act
        var unaccounted = Enumerable.Range(1, highest)
            .Select(number => $"TSH{number:000}")
            .Where(id => !Ids.Contains(id) && !Retired.Contains(id));

        // Assert
        unaccounted.Should().BeEmpty("a retired ID is listed as such, so it is never handed out again");
    }

    [Fact]
    public void A_Retired_Number_Is_Never_A_Diagnostic_Again()
    {
        // Act
        var reused = Retired.Intersect(Ids);

        // Assert
        reused.Should().BeEmpty("a suppression written for the retired diagnostic would silence the new one");
    }

    [Fact]
    public void Every_Diagnostic_Is_Reported_By_An_Action_Of_The_Analyzer()
    {
        // Act
        var unreported = Catalog.Where(descriptor => !AtTheEnd(descriptor) && !WhileTyping(descriptor)).Select(Id);

        // Assert
        unreported.Should().BeEmpty("the tests below read from the analyzer's source which action reports what");
    }

    [Fact]
    public void Every_Diagnostic_Only_The_End_Of_The_Compilation_Reports_Is_Tagged_As_Such()
    {
        // Act
        var untagged = Catalog
            .Where(descriptor => AtTheEnd(descriptor) && !WhileTyping(descriptor) && !Tagged(descriptor))
            .Select(Id);

        // Assert
        untagged.Should().BeEmpty("an editor runs that action on a build, and the tag says so (RS1037)");
    }

    [Fact]
    public void No_Diagnostic_Reported_While_Typing_Is_Tagged_As_Reported_At_The_End()
    {
        // Act
        var tagged = Catalog.Where(descriptor => WhileTyping(descriptor) && Tagged(descriptor)).Select(Id);

        // Assert
        tagged.Should().BeEmpty("an editor would hold it back until the next build");
    }

    [Fact]
    public void Every_Diagnostic_Row_Says_When_It_Is_Reported()
    {
        // Arrange
        var docs = RepositoryFiles.Read("docs/guide/diagnostics.md");

        // Act
        var drifted = Catalog
            .Where(descriptor => !docs.Contains($"{RowOf(descriptor)} {When(descriptor)} |", StringComparison.Ordinal))
            .Select(Id);

        // Assert
        drifted.Should().BeEmpty("the fourth column reads 'While you type', 'On build' or 'Both'");
    }

    private static string Id(DiagnosticDescriptor descriptor) => descriptor.Id;

    private static bool AtTheEnd(DiagnosticDescriptor descriptor) =>
        AnalyzerActions.AtTheEnd.Contains(Fields[descriptor]);

    private static bool WhileTyping(DiagnosticDescriptor descriptor) =>
        AnalyzerActions.WhileTyping.Contains(Fields[descriptor]);

    private static bool Tagged(DiagnosticDescriptor descriptor) =>
        descriptor.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd);

    private static string When(DiagnosticDescriptor descriptor) =>
        !AtTheEnd(descriptor) ? "While you type"
        : WhileTyping(descriptor) ? "Both"
        : "On build";

    private static string Anchor(DiagnosticDescriptor descriptor) => descriptor.Id.ToLowerInvariant();

    private static string RowOf(DiagnosticDescriptor descriptor) =>
        $"| [{descriptor.Id}](#{Anchor(descriptor)}) | {descriptor.DefaultSeverity} | {descriptor.Title} |";

    // A diagnostic's own row links its ID to its section; a retired one has no section to link to
    [GeneratedRegex(@"^\| (?<id>TSH\d{3}) \|", RegexOptions.Multiline)]
    private static partial Regex RetiredRow();
}
