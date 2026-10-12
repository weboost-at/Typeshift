using System.Text.Json.Nodes;
using Typeshift.Agents;
using Typeshift.Persistence;
using Typeshift.Samples.Docs.Agents;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Docs.Scenarios;

internal static class AgentSettings
{
    public static async Task RunAsync()
    {
        await using var services = Host.Create();
        var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();

        // begin-snippet: configure-settings
        var customer = Guid.NewGuid();

        await support.ConfigureAsync(customer, settings => settings with
        {
            Model = "reasoning-model",
            Instructions = "Answer in German.",
            SearchTickets = settings.SearchTickets with { Limit = 20 }
        });

        var options = await support.ResolveChatOptionsAsync(customer);
        // end-snippet

        Expect.That(options.ModelId == "reasoning-model", "stored settings choose the model");

        // begin-snippet: load-and-save-settings
        var settings = await support.LoadSettingsAsync(customer);

        await support.SaveSettingsAsync(customer, settings with { Instructions = null });
        // end-snippet

        var stored = await services.GetRequiredService<ITypeshiftStore<Guid>>()
            .LoadAsync<SupportAgent.Settings>(customer, CancellationToken.None);

        var example = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Examples", "support-agent-settings.json")));

        Expect.That(JsonNode.DeepEquals(JsonNode.Parse(stored!.Serialize()), example), "the documented JSON is stored");
    }
}
