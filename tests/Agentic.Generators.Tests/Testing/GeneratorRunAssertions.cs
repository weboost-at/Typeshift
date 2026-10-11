using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Tests.Testing;

public sealed class GeneratorRunAssertions(GeneratorRun run)
{
    // The subject plus the generated code, held to TreatWarningsAsErrors with documentation on
    public GeneratorRun CompilesCleanly()
    {
        run.CompilationDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .Should()
            .BeEmpty();

        return run;
    }

    public Diagnostic Reports(string id, DiagnosticSeverity severity, string offendingSyntax)
    {
        var reported = run.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToList();

        reported.Should().ContainSingle();
        reported[0].Severity.Should().Be(severity);
        SourceAt(reported[0].Location).Should().Be(offendingSyntax);
        return reported[0];
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

    // Models keep locations as path and span, so they come back as file locations without a tree
    private string SourceAt(Location location)
    {
        var path = location.GetLineSpan().Path;
        var tree = run.Output.SyntaxTrees.Single(candidate => candidate.FilePath == path);

        return tree.GetText().ToString(location.SourceSpan);
    }
}

public static class GeneratorRunAssertionExtensions
{
    extension(GeneratorRun run)
    {
        public GeneratorRunAssertions Assert => new(run);
    }
}
