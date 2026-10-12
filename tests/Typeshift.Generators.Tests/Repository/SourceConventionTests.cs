using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// The code style rules of AGENTS.md a pattern can check, for the library, the generator and the samples
public partial class SourceConventionTests
{
    private static readonly string[] Code = ["src", "samples"];

    [Fact]
    public void The_Code_Never_Reads_The_Clock()
    {
        // Act
        var violations = Violations(Clock());

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void The_Code_Compares_With_Null_Through_Patterns()
    {
        // Act
        var violations = Violations(NullComparison());

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void Extension_Members_Are_Declared_In_Extension_Blocks()
    {
        // Act
        var violations = Violations(ThisParameter());

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void The_Code_Holds_No_Todo()
    {
        // Act
        var violations = Violations(Todo());

        // Assert
        violations.Should().BeEmpty();
    }

    private static IEnumerable<string> Violations(Regex rule) =>
        RepositoryFiles.Find("*.cs", Code)
            .SelectMany(file => File.ReadLines(Path.Combine(RepositoryFiles.Root, file))
                .Select((line, index) => (Line: $"{file}:{index + 1}", Text: line)))
            .Where(line => rule.IsMatch(line.Text))
            .Select(line => line.Line);

    [GeneratedRegex(@"\bDateTime(Offset)?\.(Utc)?Now\b|\bStopwatch\b|\bEnvironment\.TickCount")]
    private static partial Regex Clock();

    [GeneratedRegex(@"[!=]= null\b")]
    private static partial Regex NullComparison();

    [GeneratedRegex(@"\(this [\w.<>]+ \w+")]
    private static partial Regex ThisParameter();

    [GeneratedRegex(@"\b(TODO|FIXME|HACK)\b")]
    private static partial Regex Todo();
}
