using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;

namespace Typeshift.Samples.Scenarios;

internal static class AgentSettings
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

        // begin-snippet: configure-settings
        await support.WithSettingsId(42).ConfigureAsync(settings => settings with
        {
            Provider = "anthropic",
            Model = "claude-sonnet-5-5",
            Instructions = "Answer in German.",
            SearchTickets = settings.SearchTickets with { Limit = 20 }
        });
        // end-snippet

        // begin-snippet: load-and-save-settings
        var host = support.WithSettingsId(42);
        var stored = await host.LoadSettingsAsync();

        try
        {
            await host.SaveSettingsAsync(stored.Data with { Instructions = null }, stored.Version);
        }
        catch (StoreConflictException)
        {
            stored = await host.LoadSettingsAsync();
        }
        // end-snippet

        var saved = await host.LoadSettingsAsync();

        if (saved.Data.Instructions is not null || saved.Data.SearchTickets.Limit != 20 || saved.Version != 2)
            throw new InvalidOperationException("Expected the configured settings, saved twice.");

        var lost = saved.Data with { Instructions = "Lost." };

        await ExpectConflictAsync(
            () => host.SaveSettingsAsync(lost, stored.Version),
            "Expected a stale version to conflict.");

        await ExpectConflictAsync(
            () => host.SaveSettingsAsync(lost, loadedVersion: 0),
            "Expected a second first save to conflict.");

        var after = await host.LoadSettingsAsync();

        if (after.Version != saved.Version || after.Data.Instructions is not null)
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
