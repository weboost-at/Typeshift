using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Knowledge;
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
            .WithRunMiddleware<BlockedTopics>()
            .WithToolMiddleware<AuditToolCalls>();

        services.AddKeyedChatClient("openai", fast);
        services.AddKeyedChatClient("anthropic", reasoning);
        // end-snippet

        // The default client, for the quick start's agent, which names no provider and is a sub-agent here
        services.AddChatClient(fast);

        services.AddTypeshift()
            .WithMcpToolProvider<CannedMcpToolProvider>()
            .WithProviderConnector<CannedConnector>();
        ShowApprovalRule(services);
        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite($"Data Source={database}"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITicketClient, InMemoryTicketClient>();
        services.AddSingleton<IArticleIndex, InMemoryArticleIndex>();
        services.AddScoped<ICurrentCustomer, SampleCustomer>();
        services.AddSingleton<ICustomerProviders, SampleCustomerProviders>();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<RefundDesk>();
        services.AddLogging();

        return services;
    }

    public static void ShowEfCoreRegistration(IServiceCollection services)
    {
        // begin-snippet: ef-core-registration
        services.AddTypeshift()
            .WithStore<EfTypeshiftStore>();

        services.AddDbContextFactory<TypeshiftDb>(options => options.UseSqlite("Data Source=typeshift.db"));
        // end-snippet
    }

    public static void ShowTestHost(IServiceCollection services)
    {
        // begin-snippet: store-replace
        services.AddTypeshift()
            .WithStore<InMemoryTypeshiftStore<int>>(replace: true);
        // end-snippet
    }

    public static void ShowLiveUpdateOptions(IServiceCollection services)
    {
        // begin-snippet: live-update-options
        services.AddTypeshift()
            .WithOptions(options =>
            {
                options.LiveUpdates.PublishInterval = TimeSpan.FromMilliseconds(250);
                options.LiveUpdates.MaxPendingUpdates = 1024;
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
