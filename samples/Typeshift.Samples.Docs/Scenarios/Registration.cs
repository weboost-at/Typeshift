using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;

namespace Typeshift.Samples.Scenarios;

internal static class Registration
{
    public static IServiceCollection Register(
        IServiceCollection services,
        IChatClient fast,
        IChatClient reasoning,
        string database)
    {
        // begin-snippet: registration
        services.AddTypeshift()
            .WithStore<EfTypeshiftStore>()
            .WithChatClientProvider<KeyedChatClientProvider>();

        services.AddKeyedSingleton("fast-model", fast);
        services.AddKeyedSingleton("reasoning-model", reasoning);
        // end-snippet

        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite($"Data Source={database}"));

        return services;
    }

    public static void ShowEfCoreRegistration(IServiceCollection services)
    {
        // begin-snippet: ef-core-registration
        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
        // end-snippet
    }
}
