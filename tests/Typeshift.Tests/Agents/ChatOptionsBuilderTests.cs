using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Agents;

// A Microsoft.Extensions.AI release that adds a chat option fails here until the builder sets it or the list below
// says why it does not
public class ChatOptionsBuilderTests : UnitTestBase
{
    // Each chat option and the builder call that sets it
    private static readonly Dictionary<string, string> Set = new()
    {
        [nameof(ChatOptions.ModelId)] = nameof(ChatOptionsBuilder.WithModel),
        [nameof(ChatOptions.Temperature)] = nameof(ChatOptionsBuilder.WithTemperature),
        [nameof(ChatOptions.MaxOutputTokens)] = nameof(ChatOptionsBuilder.WithMaxOutputTokens),
        [nameof(ChatOptions.TopP)] = nameof(ChatOptionsBuilder.WithTopP),
        [nameof(ChatOptions.TopK)] = nameof(ChatOptionsBuilder.WithTopK),
        [nameof(ChatOptions.FrequencyPenalty)] = nameof(ChatOptionsBuilder.WithFrequencyPenalty),
        [nameof(ChatOptions.PresencePenalty)] = nameof(ChatOptionsBuilder.WithPresencePenalty),
        [nameof(ChatOptions.Seed)] = nameof(ChatOptionsBuilder.WithSeed),
        [nameof(ChatOptions.Reasoning)] = nameof(ChatOptionsBuilder.WithReasoning),
        [nameof(ChatOptions.ResponseFormat)] = nameof(ChatOptionsBuilder.WithResponseFormat),
        [nameof(ChatOptions.StopSequences)] = nameof(ChatOptionsBuilder.WithStopSequences),
        [nameof(ChatOptions.ToolMode)] = nameof(ChatOptionsBuilder.WithToolMode),
        [nameof(ChatOptions.AllowMultipleToolCalls)] = nameof(ChatOptionsBuilder.WithAllowMultipleToolCalls),
        [nameof(ChatOptions.Tools)] = nameof(ChatOptionsBuilder.WithTools),
        [nameof(ChatOptions.AdditionalProperties)] = nameof(ChatOptionsBuilder.WithAdditionalProperty)
    };

    // The chat options no builder call sets, each for a reason the guide states
    private static readonly string[] LeftOut =
    [
        // The agent's WithInstructions or GetInstructions sets them
        nameof(ChatOptions.Instructions),

        // The run's own: the conversation is the host's, and a continuation belongs to one call
        nameof(ChatOptions.ConversationId),
        nameof(ChatOptions.ContinuationToken),

        // A run takes the response a call returns, and never polls for one a provider finishes in the background
        nameof(ChatOptions.AllowBackgroundResponses),

        // A lambda that runs, which no chain holds (D5): ConfigureOptions on the client, or a run middleware, sets it
        nameof(ChatOptions.RawRepresentationFactory)
    ];

    [Fact]
    public void Given_The_Chat_Options_Each_Is_Set_By_The_Builder_Or_Left_Out_For_A_Reason()
    {
        // Act
        var options = typeof(ChatOptions).GetProperties().Where(property => property.CanWrite).Select(p => p.Name);

        // Assert
        options.Except(Set.Keys).Except(LeftOut).Should().BeEmpty();
    }

    [Fact]
    public void Given_The_Chat_Options_The_Builder_Sets_Each_Has_Its_Call()
    {
        // Act
        var calls = typeof(ChatOptionsBuilder).GetMethods().Select(method => method.Name);

        // Assert
        Set.Values.Except(calls).Should().BeEmpty();
    }
}
