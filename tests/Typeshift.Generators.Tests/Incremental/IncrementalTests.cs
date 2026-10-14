using Typeshift.Generators.Tests.Agents.Support;
using Typeshift.Generators.Tests.Toolsets.Support;

namespace Typeshift.Generators.Tests.Incremental;

public class IncrementalTests : GeneratorTestBase
{
    private const string Unrelated =
        """
        namespace Demo;

        internal class Unrelated
        {
            public int Value { get; set; }
        }
        """;

    [Fact]
    public void Given_An_Edit_Outside_The_Toolsets_Every_Output_Stays_Cached()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.Tickets, Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 1, Unrelated.Replace("Value", "Other")));

        // Assert
        second.Assert.Cached(TrackingNames.Toolsets);
        second.Assert.OutputsCached();
    }

    [Fact]
    public void Given_An_Edit_Of_A_Tool_The_Toolset_Is_Generated_Again()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.Tickets, Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 0, ToolsetSources.Tickets.Replace("forget_ticket", "forget")));

        // Assert
        second.Assert.Modified(TrackingNames.Toolsets);
        second.Assert.OutputsModified();
    }

    [Fact]
    public void Given_Code_Moved_Within_A_Toolset_Its_Output_Stays_Cached()
    {
        // Arrange
        var compilation = Compile(ToolsetSources.Tickets, Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 0, "\n\n" + ToolsetSources.Tickets));

        // Assert
        second.Assert.OutputsCached();
    }

    [Fact]
    public void Given_An_Edit_Outside_The_Agents_Every_Agent_Output_Stays_Cached()
    {
        // Arrange
        var compilation = Compile(AgentSources.Tickets, AgentSources.Support, Unrelated);
        var first = Run(compilation);

        // Act
        var second = RunAgain(first, Edit(compilation, 2, Unrelated.Replace("Value", "Other")));

        // Assert
        second.Assert.Cached(TrackingNames.Agents, TrackingNames.References, TrackingNames.AgentOutputs);
        second.Assert.OutputsCached();
    }

    [Fact]
    public void Given_An_Edit_Of_A_Tools_Body_The_Agents_Output_Stays_Cached()
    {
        // Arrange
        var compilation = Compile(AgentSources.Tickets, AgentSources.Support, Unrelated);
        var first = Run(compilation);
        var edited = AgentSources.Tickets.Replace("=> query;", "=> query.Trim();");

        // Act
        var second = RunAgain(first, Edit(compilation, 0, edited));

        // Assert
        second.Assert.Cached(TrackingNames.AgentOutputs);
    }

    [Fact]
    public void Given_An_Edit_Of_An_Agent_It_Is_Generated_Again()
    {
        // Arrange
        var compilation = Compile(AgentSources.Tickets, AgentSources.Support, Unrelated);
        var first = Run(compilation);
        var edited = AgentSources.Support.Replace("t.DeleteTicket", "t.CloseTicket");

        // Act
        var second = RunAgain(first, Edit(compilation, 1, edited));

        // Assert
        second.Assert.Modified(TrackingNames.AgentOutputs);
    }
}
