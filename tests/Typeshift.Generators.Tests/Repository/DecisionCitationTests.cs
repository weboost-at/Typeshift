using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

public partial class DecisionCitationTests
{
    // Every decision a comment names, wherever it stands in the comment: code, project and build files, workflows
    // and hooks. The Markdown pages cite in prose, which the docs lint reads
    private static readonly IReadOnlyList<(string File, int Line, string Id)> Cited =
    [
        .. RepositoryFiles.Find("*", "src", "samples", "tests", "build", "demos", "tools", ".github", ".claude/hooks")
            .Concat(["Directory.Build.props", "Directory.Packages.props", ".editorconfig"])
            .SelectMany(Citations)
    ];

    [Fact]
    public void Every_Decision_A_Comment_Cites_Is_In_Force()
    {
        // Arrange
        var decisions = RepositoryFiles.Read("docs/contributing/decisions.md");
        var withdrawn = Ids(WithdrawnRow(), Section(decisions, "Withdrawn"));
        var inForce = Ids(Heading(), decisions).Except(withdrawn).ToHashSet();

        // Act
        var stale = Cited
            .Where(cited => !inForce.Contains(cited.Id))
            .Select(cited => $"{cited.File}:{cited.Line}: D{cited.Id}: not in force, cite the decision that is")
            .ToList();

        // Assert
        stale.Should().BeEmpty(Listing.Of(stale));
    }

    [Fact]
    public void The_Citations_Of_Every_Comment_Syntax_Are_Read()
    {
        // Act
        var read = Cited.Select(cited => Path.GetExtension(cited.File)).Distinct();

        // Assert
        read.Should().Contain([".cs", ".props", ".yml"], "a syntax the guard cannot read passes whatever it cites");
    }

    [Fact]
    public void Every_Decision_Has_Its_Anchor_And_Its_Index_Row()
    {
        // Arrange
        var decisions = RepositoryFiles.Read("docs/contributing/decisions.md");
        var withdrawn = Ids(WithdrawnRow(), Section(decisions, "Withdrawn")).ToHashSet();
        var index = IndexRow().Matches(Section(decisions, "Index")).Select(row => row.Value).ToList();

        var entries = Entries(decisions)
            .Select(entry => (
                entry.Id,
                entry.Anchor,
                Row: $"| D{entry.Id} | [{entry.Title}](#d{entry.Id}) | {entry.Section} | "
                    + $"{(withdrawn.Contains(entry.Id) ? "Withdrawn" : "In force")} |"))
            .ToList();

        // Act
        var wrong = entries
            .Where(entry => entry.Anchor != $"d{entry.Id}")
            .Select(entry => $"D{entry.Id}: end the heading with the anchor #d{entry.Id}, which its index row links")
            .Concat(entries
                .Where(entry => !index.Contains(entry.Row))
                .Select(entry => $"D{entry.Id}: the index needs the row {entry.Row}"))
            .Concat(index
                .Except(entries.Select(entry => entry.Row))
                .Select(row => $"{row}: the index row of no heading, or of one that reads otherwise"))
            .ToList();

        // Assert
        wrong.Should().BeEmpty(Listing.Of(wrong));
    }

    // Each entry's heading, with the section it stands in
    private static IEnumerable<(string Id, string Title, string Anchor, string Section)> Entries(string decisions)
    {
        var section = "";

        foreach (var line in decisions.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
                section = line[3..];

            if (Entry().Match(line) is not { Success: true } entry)
                continue;

            yield return (entry.Groups["id"].Value, entry.Groups["title"].Value, entry.Groups["anchor"].Value, section);
        }
    }

    // The lines under an H2 of the page, up to the next H2
    private static string Section(string page, string heading)
    {
        var start = page.IndexOf($"\n## {heading}\n", StringComparison.Ordinal);
        var end = start < 0 ? -1 : page.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return start < 0 ? "" : page[start..(end < 0 ? page.Length : end)];
    }

    private static IEnumerable<(string File, int Line, string Id)> Citations(string file)
    {
        var text = RepositoryFiles.Read(file);

        return Comment()
            .Matches(text)
            .SelectMany(comment => Id()
                .Matches(comment.Value)
                .Select(id => (file, LineAt(text, comment.Index + id.Index), id.Groups["id"].Value)));
    }

    private static int LineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<string> Ids(Regex pattern, string decisions) =>
        pattern.Matches(decisions).Select(match => match.Groups["id"].Value);

    [GeneratedRegex(@"^### D(?<id>\d+)\. ", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^### D(?<id>\d+)\. (?<title>.+?)(?: \{#(?<anchor>[\w-]+)\})?$")]
    private static partial Regex Entry();

    [GeneratedRegex(@"^\| D\d+ \|.*\|$", RegexOptions.Multiline)]
    private static partial Regex IndexRow();

    // A withdrawn decision is a row of the withdrawn table, whether or not its heading is gone yet
    [GeneratedRegex(@"^\| D(?<id>\d+) \|", RegexOptions.Multiline)]
    private static partial Regex WithdrawnRow();

    // To the end of the line after // or a # that stands alone, and all between <!-- and -->, over several lines
    [GeneratedRegex(@"//[^\n]*|(?<!\S)#\s[^\n]*|<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\bD(?<id>\d+)\b")]
    private static partial Regex Id();
}
