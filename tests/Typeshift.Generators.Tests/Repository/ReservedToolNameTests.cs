using System.Reflection;
using Typeshift.Generators.Parsing;
using FluentAssertions;

namespace Typeshift.Generators.Tests.Repository;

// The generator cannot reference the runtime, so each holds the names of the tools a run offers itself. The analyzer
// reports a tool of such a name with the generator's literals, and a run refuses it with the runtime's
public class ReservedToolNameTests
{
    [Theory]
    [InlineData("Typeshift.AskUserFunction", "ToolName", ToolNames.AskUser)]
    [InlineData("Typeshift.ExitPlanModeFunction", "ToolName", ToolNames.ExitPlanMode)]
    [InlineData("Typeshift.ConversationChatClient", "HandoffPrefix", ToolNames.HandoffPrefix)]
    public void Given_A_Name_The_Generator_Reserves_The_Runtime_Holds_The_Same_Literal(
        string type,
        string constant,
        string reserved)
    {
        // Act
        var field = typeof(Agent).Assembly.GetType(type, throwOnError: true)!.GetField(
            constant,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        // Assert
        field!.GetRawConstantValue().Should().Be(reserved);
    }
}
