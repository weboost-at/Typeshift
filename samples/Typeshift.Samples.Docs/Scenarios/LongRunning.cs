using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Web;

namespace Typeshift.Samples.Scenarios;

// The coding agent of the guide: its run options start it in plan mode, and its explorer works in the background, the
// run waiting for the answer before it ends. Compaction starts past 150,000 input tokens, which no scenario reaches
internal static class LongRunning
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var coding = scope.ServiceProvider.GetRequiredService<AgentHost<CodingAgent>>().WithConversationId(31);

        var planned = await coding.RunAsync("Fix the failing test.");
        if (planned.Decisions is not [PlanDecision plan])
            throw new InvalidOperationException("Expected the coding agent to plan first.");

        await coding.AnswerAsync([plan.Approve(AgentMode.Default)]);

        var explored = await coding.RunAsync("Ask the explorer where the tests are.");
        var results = explored.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>();

        if (!results.Any(result => $"{result.Result}".StartsWith("explore_agent works in the background")))
            throw new InvalidOperationException("Expected the explorer's call to return at once.");

        // The answer reached the model as a message of the conversation, before the run's last model call
        var stored = await provider.GetRequiredService<IConversationStore<int>>()
            .LoadConversationAsync(31, CancellationToken.None);

        var answer = stored
            .SelectMany(entry => entry.Messages)
            .LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? "";

        if (!answer.Contains("explore_agent") || !answer.Contains(CannedChatClient.Answer))
            throw new InvalidOperationException($"Expected the explorer's answer as a message: {answer}");

        // A chat page shows what the user and the agents said, and the explorer's answer is for the coding agent
        if (ChatLine.From(stored).Any(line => line.Text == answer))
            throw new InvalidOperationException("Expected the chat's lines to leave the explorer's answer out.");
    }
}
