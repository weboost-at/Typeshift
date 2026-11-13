using System.Text.RegularExpressions;

namespace Typeshift.Tests.Testing.Conventions;

// Every violation reads "<file>:<line>: <what>: <what to do>", so an editor jumps to it and nobody looks the rule up
public static partial class ConventionViolations
{
    private const string NamingHint = "name it Given_<state>_<outcome>, declared public void, Task or ValueTask";

    // What a test never writes, each with what it writes instead
    public static IReadOnlyDictionary<string, (Regex Pattern, string Hint)> BannedCalls { get; } =
        new Dictionary<string, (Regex, string)>
        {
            ["Task.Delay"] = (
                new(@"\bTask\.Delay\("),
                "order the work with a TaskCompletionSource gate and move time with Time.Advance"),
            ["Thread.Sleep"] = (
                new(@"\bThread\.Sleep\("),
                "order the work with a TaskCompletionSource gate"),
            ["#region"] = (
                new(@"^\s*#(end)?region\b"),
                "order the members instead of folding them"),
            ["new Faker"] = (
                new(@"\bnew\s+(Bogus\.)?Faker\b|\bFaker(<[^>]+>)?\s+\w+\s*=\s*new\("),
                "take the value from Data, the catalog seeded per test"),
            ["disabled test"] = (
                new(@"\[(Fact|Theory)\(.*\b(Skip|Explicit)\s*=|\bAssert\.Skip(When|Unless)?\("),
                "a test runs or is deleted, and a known bug stays a red test"),
            ["raw cancellation token"] = (
                new(@"TestContext\.Current\.CancellationToken"),
                "use Ct from the test base"),
            ["numeric wait timeout"] = (
                new(@"\b(WaitAsync|Timeout)\([^;]*(TimeSpan\.From\w+\(|\d+\.(Seconds|Minutes)\(\))"),
                "await the gate with Ct, which ends a hang after 20 seconds"),
            ["wall clock"] = (
                new(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b"),
                "read Time, the FakeTimeProvider of the test base"),
            ["real time"] = (
                new(@"\b(Stop[w]atch|Environment\.TickCount|TimeProvider\.System)\b"),
                "read Time, the FakeTimeProvider of the test base"),
            ["chained assertion"] = (
                new(@"\.(And|Which|Subject)\b(?!\s*\()"),
                "one assertion per statement, a recurring check becomes an .Assert helper")
        };

    // Every test whose name is neither Given_<state>_<outcome> nor the outcome alone
    public static IEnumerable<string> Names() =>
        TestSources.TestMethods
            .Where(method => !IsWellNamed(method))
            .Select(method => $"{method.File}:{method.Line}: {method.Name}: {NamingHint}");

    // Every test declaration line past 120 columns
    public static IEnumerable<string> LongDeclarations() =>
        TestSources.Files.SelectMany(file => Numbered(TestSources.Lines(file))
            .Where(line => line.Text.Length > 120 && TestSources.TestDeclaration().IsMatch(line.Text))
            .Select(line => $"{file}:{line.Number}: {TestSources.NameOf(line.Text)}: shorten the name"));

    // Every test class that does not live in <Project>/<Namespace folder>/<Class>.cs
    public static IEnumerable<string> Misplaced() =>
        TestSources.TestMethods
            .Where(method => method.File != ExpectedFile(method))
            .Select(method => $"{method.File}: {method.Namespace}.{method.Class}: move it to {ExpectedFile(method)}")
            .Distinct();

    public static IEnumerable<string> Banned(string call) =>
        TestSources.Files.SelectMany(file => Numbered(TestSources.Lines(file))
            .Where(line => BannedCalls[call].Pattern.IsMatch(line.Text))
            .Select(line => $"{file}:{line.Number}: {call}: {BannedCalls[call].Hint}"));

    // Every comment in a test body other than Arrange, Act, Assert or Act & Assert: on a line of its own, behind a
    // statement or between /* and */
    public static IEnumerable<string> Comments() =>
        TestSources.Files.SelectMany(file => TestBodies(TestSources.Lines(file))
            .SelectMany(body => body.Lines
                .Where(line => IsForeignComment(line.Text))
                .Select(line => $"{file}:{line.Number}: {body.Name}: say it in the test's name or its data")));

    // Every test whose body holds no assertion
    public static IEnumerable<string> WithoutAssertion() =>
        TestSources.Files.SelectMany(file => TestBodies(TestSources.Lines(file))
            .Where(body => !body.Lines.Any(line => Assertion().IsMatch(line.Text)))
            .Select(body => $"{file}:{body.Line}: {body.Name}: assert with .Should(), .Assert or Snapshot.Verify"));

    // Everything but a test class declared outside Testing/ and the areas' Support/ folders
    public static IEnumerable<string> SupportOutsideSupport() =>
        TestSources.Files
            .Where(file => !file.Contains("/Support/"))
            .SelectMany(file => Numbered(TestSources.Lines(file))
                .Select(line => (line.Number, Type: TopLevelType().Match(line.Text).Groups["name"].Value))
                .Where(line => line.Type.Length > 0 && !line.Type.EndsWith("Tests"))
                .Select(line => $"{file}:{line.Number}: {line.Type}: move it to Testing/ or a Support/ folder"));

    private static IEnumerable<(int Number, string Text)> Numbered(string[] lines) =>
        lines.Select((line, index) => (index + 1, line));

    private static bool IsWellNamed(TestMethod method)
    {
        var subject = method.Class.Replace("Tests", "");

        return WellFormedName().IsMatch(method.Name)
            && !method.Name.StartsWith("When_")
            && !method.Name.StartsWith($"{subject}_");
    }

    private static string ExpectedFile(TestMethod method)
    {
        var project = TestSources.ProjectOf(method.File);
        var folder = method.Namespace == project
            ? ""
            : method.Namespace[(project.Length + 1)..].Replace('.', '/') + "/";

        return $"{project}/{folder}{method.Class}.cs";
    }

    // A pragma keeps its reason beside it, and what a string holds is no comment
    private static bool IsForeignComment(string line)
    {
        var code = Literal().Replace(line, "\"\"").Trim();

        return Comment().IsMatch(code)
            && !code.StartsWith("#pragma")
            && code is not ("// Arrange" or "// Act" or "// Assert" or "// Act & Assert");
    }

    // A [Fact] or [Theory] method from its declaration to the brace that closes it, found by counting them
    private static IEnumerable<(string Name, int Line, List<(int Number, string Text)> Lines)> TestBodies(
        string[] lines)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            if (!TestSources.TestAttribute().IsMatch(lines[index])) continue;

            var declaration = Array.FindIndex(lines, index, line => TestSources.Member().IsMatch(line));
            if (declaration < 0) yield break;

            var body = new List<(int, string)>();
            var depth = 0;
            var opened = false;

            for (index = declaration + 1; index < lines.Length; index++)
            {
                depth += lines[index].Count(character => character == '{');
                depth -= lines[index].Count(character => character == '}');
                opened |= depth > 0;
                body.Add((index + 1, lines[index]));

                if (opened && depth == 0) break;
            }

            yield return (TestSources.NameOf(lines[declaration]), declaration + 1, body);
        }
    }

    [GeneratedRegex(@"^(Given_)?[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$")]
    private static partial Regex WellFormedName();

    // Top-level types start in the first column, nested ones are indented
    [GeneratedRegex(
        @"^(public |internal |file )?((sealed|static|abstract|partial) )*"
        + @"(class|record|struct|interface|enum) (?<name>\w+)")]
    private static partial Regex TopLevelType();

    // A character or a string, whose text is no code
    [GeneratedRegex(@"'(?:[^'\\]|\\.)+'|""(?:[^""\\]|\\.)*""")]
    private static partial Regex Literal();

    [GeneratedRegex(@"//|/\*")]
    private static partial Regex Comment();

    // FluentAssertions, an assertions class behind .Assert, or a snapshot
    [GeneratedRegex(@"\.Should\(\)|\.Assert\.|\bSnapshot\.Verify\(")]
    private static partial Regex Assertion();
}
