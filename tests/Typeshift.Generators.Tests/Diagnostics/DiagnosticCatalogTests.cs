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

    [GeneratedRegex(@"^(?<id>TSH\d{3}) \|", RegexOptions.Multiline)]
    private static partial Regex Row();

    // A diagnostic's own row links its ID to its section; a retired one has no section to link to
    [GeneratedRegex(@"^\| (?<id>TSH\d{3}) \|", RegexOptions.Multiline)]
    private static partial Regex RetiredRow();
}
