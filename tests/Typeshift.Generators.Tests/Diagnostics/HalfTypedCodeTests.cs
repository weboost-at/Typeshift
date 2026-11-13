using Typeshift.Generators.Tests.Diagnostics.Support;

namespace Typeshift.Generators.Tests.Diagnostics;

public class HalfTypedCodeTests : GeneratorTestBase
{
    public static TheoryData<string, string> Entries { get; } = [.. HalfTypedSources.Entries];

    [Theory]
    [MemberData(nameof(Entries))]
    public void Given_A_Declaration_Being_Typed_The_Generator_And_The_Analyzer_Do_Not_Throw(string into, string body)
    {
        // Arrange
        var source = HalfTypedSources.Typed(into, body);

        // Act
        var run = Run(source);

        // Assert
        run.Assert.RunsToTheEnd();
    }
}
