using System.Text.RegularExpressions;
using Typeshift.Generators.Tests.Repository.Support;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// The code style rules of AGENTS.md a pattern can check, for the library, the generator, the samples and the tests;
// the patterns are spelled so that they do not match themselves
public partial class SourceConventionTests
{
    private static readonly string[] Code = ["src", "samples", "tests"];

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

    // Task.Delay waits on the real clock unless a TimeProvider is its second argument
    [GeneratedRegex(
        @"\bDateTime(Offset)?\.(UtcNow|Now|Today)\b|\bStop[w]atch\b|\bEnvironment\.TickCount"
        + @"|\bTask\.Delay\([^,]*\)\s*;")]
    private static partial Regex Clock();

    [GeneratedRegex(@"[!=]= null\b|\bnull [!=]=")]
    private static partial Regex NullComparison();

    // After the parenthesis or on a line of its own; the generated interceptor's this global:: matches neither
    [GeneratedRegex(@"(\(|^\s*)this [\w.<>?\[\]]+ \w+[,)]")]
    private static partial Regex ThisParameter();

    [GeneratedRegex(@"(//|/\*).*\b(TO[D]O|FIX[M]E|HA[C]K)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Todo();
}
