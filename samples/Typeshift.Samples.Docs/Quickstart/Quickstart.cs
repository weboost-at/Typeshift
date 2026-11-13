using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Quickstart;

internal static class Quickstart
{
    public static async Task RunAsync(IChatClient client)
    {
        // begin-snippet: quickstart-runtime
        await using var runtime = TypeshiftRuntime.Create(client);
        await using var research = runtime.Agent<ResearchAgent>();

        var first = await research.RunAsync("Vienna");

        Console.WriteLine($"{research.Agent.Name}: {first.Text}");
        // end-snippet

        // begin-snippet: quickstart-settings
        var settingsId = 42;

        await research.WithSettingsId(settingsId).ConfigureAsync(settings => settings with
        {
            Temperature = 0.4f,
            FindCompanies = settings.FindCompanies with { Limit = 2 }
        });
        // end-snippet

        // begin-snippet: quickstart-run
        var answer = await research.WithSettingsId(settingsId).WithConversationId(1).RunAsync("Vienna");

        Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
        // end-snippet

        // begin-snippet: quickstart-run-settings
        var run = new ResearchAgent.Settings
        {
            FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
        };

        await foreach (var update in research.WithSettingsId(settingsId).RunStreamingAsync("Vienna", settings: run))
            Console.Write(update.Text);
        // end-snippet

        Console.WriteLine();

        // Getting started prints these three answers, one for each layer that sets the limit
        var limited = await research.WithSettingsId(settingsId).RunAsync("Vienna", settings: run);

        if (first.Text != "Found Danube Robotics, Prater Motion and Belvedere Optics.")
            throw new InvalidOperationException($"Expected the agent's limit of 3: {first.Text}");

        if (answer.Text != "Found Danube Robotics and Prater Motion.")
            throw new InvalidOperationException($"Expected the stored limit of 2: {answer.Text}");

        if (limited.Text != "Found Danube Robotics.")
            throw new InvalidOperationException($"Expected the run's limit of 1: {limited.Text}");

        // begin-snippet: own-settings
        await research.ConfigureAsync(settings => settings with { Temperature = 0.3f });

        var everyRun = await research.RunAsync("Graz");
        // end-snippet

        var resolved = await research.ResolveOptionsAsync();
        if (resolved.Temperature != 0.3f || everyRun.Text.Length == 0)
            throw new InvalidOperationException("Expected the agent's own settings to apply.");

        var stored = await research.WithSettingsId(settingsId).LoadSettingsAsync();

        if (stored.Data.Temperature != 0.4f) throw new InvalidOperationException("Expected the configured settings.");
    }
}
