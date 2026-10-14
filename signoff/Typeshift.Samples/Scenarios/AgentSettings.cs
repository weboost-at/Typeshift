using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class AgentSettings
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var settingsId = 42;

        // begin-snippet: configure-settings
        await support.ConfigureAsync(settingsId, settings => settings with
        {
            Model = "reasoning-model",
            Instructions = "Answer in German.",
            SearchTickets = settings.SearchTickets with { Limit = 20 }
        });
        // end-snippet

        // begin-snippet: load-and-save-settings
        var stored = await support.LoadSettingsAsync(settingsId);

        try
        {
            await support.SaveSettingsAsync(settingsId, stored.Data with { Instructions = null }, stored.Revision);
        }
        catch (StoreConflictException)
        {
            // Someone saved in between: reload, show the form again
        }
        // end-snippet
    }
}
