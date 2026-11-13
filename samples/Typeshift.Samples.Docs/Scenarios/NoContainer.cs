using Microsoft.Extensions.AI;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// D75: an app without a container of its own
internal static class NoContainer
{
    public static async Task RunAsync(IChatClient client)
    {
        // begin-snippet: typeshift-runtime
        await using var runtime = TypeshiftRuntime.Create(client);
        await using var research = runtime.Agent<ResearchAgent>();

        var answer = await research.RunAsync("Vienna");

        Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
        // end-snippet

        if (string.IsNullOrEmpty(answer.Text))
            throw new InvalidOperationException("The runtime's agent answered nothing.");
    }
}
