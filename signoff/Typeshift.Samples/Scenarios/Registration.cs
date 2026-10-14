using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;

namespace Typeshift.Samples.Scenarios;

internal static class Registration
{
    public static IServiceCollection Register(IServiceCollection services, IChatClient fast, IChatClient reasoning)
    {
        // begin-snippet: registration
        services.AddTypeshift()
            .WithStore<EfTypeshiftStore>()
            .WithChatClientProvider<KeyedChatClientProvider>()
            .WithRealtimeTracking()
            .WithRunMiddleware<BlockedTopics>()
            .WithToolMiddleware<AuditToolCalls>()
            .WithMcpToolProvider<McpToolProvider>();

        services.AddKeyedSingleton("fast-model", fast);
        services.AddKeyedSingleton("reasoning-model", reasoning);
        // end-snippet

        // begin-snippet: ef-core-registration
        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
        // end-snippet

        // begin-snippet: realtime-options
        services.AddTypeshift()
            .WithRealtimeTracking(new RealtimeTrackingOptions
            {
                PublishInterval = TimeSpan.FromMilliseconds(250),
                MaxPendingUpdates = 1024
            });
        // end-snippet

        // begin-snippet: built-in-registration
        services.AddSingleton<HttpClient>();

        services.AddTypeshift()
            .WithFileSystemRoot<CustomerWorkspace>()
            .WithWebSearch<SearchApi>()
            .WithWebFetch<HttpWebFetch>()
            .WithApprovalRule<ShellGuard>();
        // end-snippet

        // begin-snippet: open-telemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource("Typeshift"))
            .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
        // end-snippet

        return services;
    }
}
