using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Stores.EfCore;

namespace Typeshift.Samples.Scenarios;

internal static class StoreVerification
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        var contexts = provider.GetRequiredService<IDbContextFactory<TypeshiftDb>>();

        // begin-snippet: store-check
        var store = new EfTypeshiftStore(contexts);

        await store.VerifyAsync(9001);
        // end-snippet
    }
}
