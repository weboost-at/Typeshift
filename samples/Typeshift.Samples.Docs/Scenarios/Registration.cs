using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

internal static class Registration
{
    public static IServiceCollection Register(IServiceCollection services, IChatClient fast, IChatClient reasoning)
    {
        // begin-snippet: registration
        services.AddTypeshift()
            .WithChatClientProvider<KeyedChatClientProvider>();

        services.AddKeyedSingleton("fast-model", fast);
        services.AddKeyedSingleton("reasoning-model", reasoning);
        // end-snippet

        return services;
    }
}
