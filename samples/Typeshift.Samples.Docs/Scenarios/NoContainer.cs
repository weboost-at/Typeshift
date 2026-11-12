using Microsoft.Extensions.AI;
using Typeshift.Samples.Quickstart;

namespace Typeshift.Samples.Scenarios;

// D75: an app without a container of its own
internal static class NoContainer
{
    public static async Task RunAsync(IChatClient client)
    {
        // begin-snippet: typeshift-app
        await using var app = TypeshiftApp.Create(client);

        var research = app.Agent<ResearchAgent>();
        var answer = await research.RunAsync("Vienna");
        // end-snippet

        if (string.IsNullOrEmpty(answer.Text))
            throw new InvalidOperationException("The app's agent answered nothing.");

        Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
    }
}
