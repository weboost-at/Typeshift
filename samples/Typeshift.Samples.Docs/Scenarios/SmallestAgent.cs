using Microsoft.Extensions.AI;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

// An agent of instructions alone, which names no model, so the runtime's one client answers it
internal static class SmallestAgent
{
    public static async Task RunAsync(IChatClient client)
    {
        // begin-snippet: smallest-agent-run
        await using var runtime = TypeshiftRuntime.Create(client);
        await using var assistant = runtime.Agent<AssistantAgent>();

        var answer = await assistant.RunAsync("Where is my refund?");

        Console.WriteLine(answer.Text);
        // end-snippet

        if (answer.Text != CannedChatClient.Answer)
            throw new InvalidOperationException($"Expected the smallest agent to answer: {answer.Text}");
    }
}
