using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class AgentSettings
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var customer = 42;

        // begin-snippet: configure-settings
        await support.ConfigureAsync(customer, settings => settings with
        {
            Model = "reasoning-model",
            Instructions = "Answer in German.",
            SearchTickets = settings.SearchTickets with { Limit = 20 }
        });
        // end-snippet

        // begin-snippet: load-and-save-settings
        var stored = await support.LoadSettingsAsync(customer);

        try
        {
            await support.SaveSettingsAsync(customer, stored.Data with { Instructions = null }, stored.Revision);
        }
        catch (StoreConflictException)
        {
            // Someone saved in between: reload, show the form again
        }
        // end-snippet
    }
}
