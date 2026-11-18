using Typeshift.Generators.Tests.Incremental.Support;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Tests.Incremental;

// D3: after an unrelated edit the outputs stay cached, but the transforms that feed them bind every toolset and agent
// again. This budget, in bytes allocated for each pair of them, is what an edit costs. Bytes repeat to the byte
// whatever the machine's load, where a time would not
public class IncrementalCostTests : GeneratorTestBase
{
    private const int Pairs = 25;

    // 263 KB when set
    private const int PairBudget = 330_000;

    [Fact]
    public void Given_An_Unrelated_Edit_The_Generator_Runs_Again_Within_Its_Budget_For_Each_Toolset_And_Agent()
    {
        // Arrange
        var compilation = Compile(
            [.. Enumerable.Range(0, Pairs).Select(index => ScaleSources.Pair(index, Pairs)), ScaleSources.Unrelated]);

        var driver = CreateDriver().RunGenerators(compilation, Ct);

        for (var pass = 0; pass < 2; pass++)
        {
            driver = driver.RunGenerators(Edited(compilation, pass), Ct);
        }

        var edited = Edited(compilation, pass: 2);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        driver = driver.RunGenerators(edited, Ct);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        driver.GetRunResult().GeneratedTrees.Should().HaveCount(4 * Pairs + 1);
        (allocated / Pairs).Should().BeLessThan(PairBudget);
    }

    private static Compilation Edited(Compilation compilation, int pass) =>
        Edit(compilation, Pairs, ScaleSources.Unrelated.Replace("Value + 1", $"Value + {pass + 2}"));
}
