using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Stores.EfCore;

namespace Typeshift.Samples.Scenarios;

internal static class StoreChecks
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        var contexts = provider.GetRequiredService<IDbContextFactory<TypeshiftDb>>();

        // begin-snippet: store-check
        var store = new EfTypeshiftStore(contexts);
        var settingsId = 9001;
        var conversationId = 9001;

        await StoreCheck.CheckSettingsAsync(store, settingsId);
        await StoreCheck.CheckConversationsAsync(store, conversationId);
        // end-snippet
    }
}
