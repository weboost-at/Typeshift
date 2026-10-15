using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Quickstart;

internal static class Quickstart
{
    public static async Task RunAsync()
    {
        // begin-snippet: quickstart-registration
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore>()
            .WithChatClientProvider<KeyedChatClientProvider>();

        services.AddKeyedSingleton<IChatClient>("fast-model", new CannedChatClient());

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        // end-snippet

        // begin-snippet: quickstart-settings
        await using var scope = provider.CreateAsyncScope();
        var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();
        var settingsId = 42;

        await research.ConfigureAsync(settingsId, settings => settings with
        {
            Temperature = 0.4f,
            FindCompanies = settings.FindCompanies with { Limit = 2 }
        });
        // end-snippet

        // begin-snippet: quickstart-run
        var answer = await research.RunAsync("Vienna", settingsId, conversationId: 1);

        Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
        // end-snippet

        // begin-snippet: quickstart-run-settings
        var run = new ResearchAgent.Settings
        {
            FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
        };

        await foreach (var update in research.RunStreamingAsync("Vienna", settingsId, settings: run))
            Console.Write(update.Text);
        // end-snippet

        Console.WriteLine();

        var stored = await research.LoadSettingsAsync(settingsId);

        if (stored.Data.Temperature != 0.4f) throw new InvalidOperationException("Expected the configured settings.");
    }
}
