using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Diagnostics;

public partial class DiagnosticCatalogTests
{
    // The release tracking files list every descriptor; the RS2000 analyzer fails the build on a missing row
    private static readonly IReadOnlyList<string> Ids =
    [
        .. new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" }
            .SelectMany(file => Row().Matches(RepositoryFiles.Read($"src/Typeshift.Generators/{file}")))
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
    public void Every_Diagnostic_Has_A_Test_Named_After_It()
    {
        // Arrange
        var tests = typeof(DiagnosticTests).GetMethods().Select(method => method.Name).ToList();

        // Act
        var untested = Ids.Where(id => !tests.Any(test => test.Contains($"_{id}_", StringComparison.Ordinal)));

        // Assert
        untested.Should().BeEmpty();
    }

    [GeneratedRegex(@"^(?<id>TSH\d{3}) \|", RegexOptions.Multiline)]
    private static partial Regex Row();
}
