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
            Provider = "anthropic",
            Model = "claude-sonnet-5-5",
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
            stored = await support.LoadSettingsAsync(settingsId);
        }
        // end-snippet

        var saved = await support.LoadSettingsAsync(settingsId);

        if (saved.Data.Instructions is not null || saved.Data.SearchTickets.Limit != 20 || saved.Revision != 2)
            throw new InvalidOperationException("Expected the configured settings, saved twice.");

        var lost = saved.Data with { Instructions = "Lost." };

        await ExpectConflictAsync(
            () => support.SaveSettingsAsync(settingsId, lost, stored.Revision),
            "Expected a stale revision to conflict.");

        await ExpectConflictAsync(
            () => support.SaveSettingsAsync(settingsId, lost, revision: 0),
            "Expected a second first save to conflict.");

        var after = await support.LoadSettingsAsync(settingsId);

        if (after.Revision != saved.Revision || after.Data.Instructions is not null)
            throw new InvalidOperationException("Expected a conflict to leave the settings as they were.");

        static async Task ExpectConflictAsync(Func<Task> save, string message)
        {
            try
            {
                await save();
            }
            catch (StoreConflictException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
