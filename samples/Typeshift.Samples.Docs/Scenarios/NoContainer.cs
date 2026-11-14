using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Typeshift.Samples.Quickstart;
using Typeshift.Samples.Tickets;

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

        await WithServicesAsync(client);
    }

    // The chain's services: logging, and what the tools resolve
    private static async Task WithServicesAsync(IChatClient client)
    {
        // begin-snippet: typeshift-runtime-services
        await using var runtime = TypeshiftRuntime.Create(client, typeshift =>
        {
            typeshift.Services.AddLogging(logging => logging.AddConsole());
            typeshift.Services.AddSingleton<ITicketClient>(new InMemoryTicketClient(TimeProvider.System));
        });
        // end-snippet

        var tickets = runtime.Services.GetService<ITicketClient>();
        var logging = runtime.Services.GetService<ILoggerFactory>();

        if (tickets is null || logging is null)
            throw new InvalidOperationException("Expected the runtime to hold the services its chain registered.");
    }
}
