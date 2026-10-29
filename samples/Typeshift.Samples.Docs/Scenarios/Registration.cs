using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;
using Typeshift.Samples.Tickets;

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
            .WithChatClientProvider<KeyedChatClientProvider>()
            .WithRealtimeTracking()
            .WithRunMiddleware<BlockedTopics>()
            .WithToolMiddleware<AuditToolCalls>();

        services.AddKeyedSingleton("openai", fast);
        services.AddKeyedSingleton("anthropic", reasoning);
        // end-snippet

        services.AddTypeshift()
            .WithMcpToolProvider<CannedMcpToolProvider>();
        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite($"Data Source={database}"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITicketClient, InMemoryTicketClient>();
        services.AddScoped<ICurrentCustomer, SampleCustomer>();
        services.AddScoped<RefundDesk>();
        services.AddLogging();

        return services;
    }

    public static void ShowEfCoreRegistration(IServiceCollection services)
    {
        // begin-snippet: ef-core-registration
        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
        // end-snippet
    }

    public static void ShowRealtimeOptions(IServiceCollection services)
    {
        // begin-snippet: realtime-options
        services.AddTypeshift()
            .WithRealtimeTracking(new RealtimeTrackingOptions
            {
                PublishInterval = TimeSpan.FromMilliseconds(250),
                MaxPendingUpdates = 1024
            });
        // end-snippet
    }

    public static void ShowApprovalRule(IServiceCollection services)
    {
        // begin-snippet: approval-rule-registration
        services.AddTypeshift()
            .WithApprovalRule<ShellGuard>();
        // end-snippet
    }

    public static void ShowOpenTelemetry(IServiceCollection services)
    {
        // begin-snippet: open-telemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource("Typeshift"))
            .WithMetrics(metrics => metrics.AddMeter("Typeshift"));
        // end-snippet
    }

    public static void ShowMcpRegistration(IServiceCollection services)
    {
        // begin-snippet: mcp-registration
        services.AddTypeshift()
            .WithMcpToolProvider<McpToolProvider>();
        // end-snippet
    }
}
