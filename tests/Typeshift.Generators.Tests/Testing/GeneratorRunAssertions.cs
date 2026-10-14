using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Testing;

public sealed class GeneratorRunAssertions(GeneratorRun run)
{
    // The subject plus the generated code, held to TreatWarningsAsErrors with documentation on, and a generator
    // that neither warns nor fails
    public GeneratorRun CompilesCleanly()
    {
        run.Reported
            .Concat(run.Result.Diagnostics)
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .Should()
            .BeEmpty();

        return run;
    }

    public Diagnostic Reports(string id, DiagnosticSeverity severity, string offendingSyntax)
    {
        var reported = run.Reported.Where(diagnostic => diagnostic.Id == id).ToList();

        reported.Should().ContainSingle();
        reported[0].Severity.Should().Be(severity);
        SourceAt(reported[0].Location).Should().Be(offendingSyntax);
        return reported[0];
    }

    // One of several reports of the id, for a mistake that several checks see
    public Diagnostic ReportsAmong(string id, DiagnosticSeverity severity, string offendingSyntax)
    {
        var reported = run.Reported
            .Where(diagnostic => diagnostic.Id == id && SourceAt(diagnostic.Location) == offendingSyntax)
            .ToList();

        reported.Should().ContainSingle();
        reported[0].Severity.Should().Be(severity);
        return reported[0];
    }

    // A diagnostic about the compilation as a whole, at no location
    public Diagnostic ReportsOnTheCompilation(string id, DiagnosticSeverity severity)
    {
        var reported = run.Reported.Where(diagnostic => diagnostic.Id == id).ToList();

        reported.Should().ContainSingle();
        reported[0].Severity.Should().Be(severity);
        reported[0].Location.Should().Be(Location.None);
        return reported[0];
    }

    public GeneratorRun DoesNotReport(string id)
    {
        run.Reported.Select(diagnostic => diagnostic.Id).Should().NotContain(id);
        return run;
    }

    public GeneratorRun Suppresses(string id)
    {
        run.Diagnostics
            .Where(diagnostic => diagnostic.Id == id)
            .Should()
            .OnlyContain(diagnostic => diagnostic.IsSuppressed);

        run.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == id);
        return run;
    }

    // No generated file holds a compiler error or warning: the analyzer reports every shape the generator cannot
    // handle. Hidden ones (an alias the project never uses) are the IDE's business
    public GeneratorRun GeneratesValidCode()
    {
        run.Reported
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Where(diagnostic => diagnostic.Location.SourceTree is { } tree && Generated(tree))
            .Select(diagnostic => diagnostic.ToString())
            .Should()
            .BeEmpty();

        return run;
    }

    public GeneratorRun Cached(params string[] steps)
    {
        foreach (var step in steps)
        {
            Reasons(run.Result.Results[0].TrackedSteps[step])
                .Should()
                .BeSubsetOf([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged], step);
        }

        return run;
    }

    public GeneratorRun OutputsCached()
    {
        var outputs = run.Result.Results[0].TrackedOutputSteps.Values.SelectMany(steps => Reasons(steps));
        outputs.Should().OnlyContain(reason => reason == IncrementalStepRunReason.Cached);
        return run;
    }

    public GeneratorRun Modified(string step)
    {
        Reasons(run.Result.Results[0].TrackedSteps[step]).Should().Contain(IncrementalStepRunReason.Modified);
        return run;
    }

    public GeneratorRun OutputsModified()
    {
        var outputs = run.Result.Results[0].TrackedOutputSteps.Values.SelectMany(steps => Reasons(steps));
        outputs.Should().Contain(IncrementalStepRunReason.Modified);
        return run;
    }

    private static IEnumerable<IncrementalStepRunReason> Reasons(IEnumerable<IncrementalGeneratorRunStep> steps) =>
        steps.SelectMany(step => step.Outputs).Select(output => output.Reason);

    private bool Generated(SyntaxTree tree) => run.Result.GeneratedTrees.Contains(tree);

    private static string SourceAt(Location location) =>
        location.SourceTree!.GetText().ToString(location.SourceSpan);
}

public static class GeneratorRunAssertionExtensions
{
    extension(GeneratorRun run)
    {
        public GeneratorRunAssertions Assert => new(run);
    }
}
