using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Quickstart;

// An app with one agent and no database keeps the agent's settings in a folder, across restarts
internal static class FileSettings
{
    public static async Task RunAsync(string folder)
    {
        await using (var first = Build(folder))
        {
            await using var scope = first.CreateAsyncScope();
            var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();

            await research.ConfigureAsync(settings => settings with { Temperature = 0.3f });
        }

        await using var restarted = Build(folder);
        await using var again = restarted.CreateAsyncScope();
        var options = await again.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>().ResolveOptionsAsync();

        if (options.Temperature != 0.3f)
            throw new InvalidOperationException("Expected the settings the folder kept to apply after a restart.");
    }

    private static ServiceProvider Build(string folder)
    {
        // begin-snippet: file-agent-settings
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithFileStore(store => store.Folder = folder)
            .WithChatClientProvider<KeyedChatClientProvider>();
        // end-snippet

        services.AddKeyedSingleton<IChatClient>("openai", new CannedChatClient());

        return services.BuildServiceProvider(validateScopes: true);
    }
}
