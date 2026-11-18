using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Stores.EfCore;

namespace Typeshift.Samples.Scenarios;

// The registrations the guide shows beside the scenarios' own, each on services of its own, built as an app builds
// them and resolved. A real provider's client is built with a key that is no key: it calls nobody until a run does
internal static class ShownRegistrations
{
    private const string Key = "sk-not-a-key";

    public static async Task RunAsync(IChatClient client)
    {
        await StoresAsync();
        await ChatClientsAsync(client);
        await TenantClientsAsync();

        await using (var mcp = Build(Registration.ShowMcpRegistration))
        {
            await using var scope = mcp.CreateAsyncScope();
            Expect(scope.ServiceProvider.GetRequiredService<IMcpToolProvider>() is McpToolProvider, "the MCP provider");
        }

        await using (var rule = Build(Registration.ShowApprovalRule))
        {
            await using var scope = rule.CreateAsyncScope();
            Expect(scope.ServiceProvider.GetServices<IToolApprovalRule>().Single() is ShellGuard, "the approval rule");
        }

        await using (var live = Build(Registration.ShowLiveUpdateOptions))
        {
            var options = live.GetRequiredService<LiveUpdateOptions>();
            Expect(options.PublishInterval == TimeSpan.FromMilliseconds(250), "the live updates' interval");
        }

        await using (var telemetry = Build(Registration.ShowOpenTelemetry))
        {
            telemetry.GetRequiredService<TracerProvider>();
            Expect(new ActivitySource("Typeshift").HasListeners(), "OpenTelemetry to listen to Typeshift's spans");
        }

        var summary = Models.ShowSummaryProvider();
        Expect(summary is { SummaryProvider: "openai", SummaryModel: "gpt-5-mini" }, "the summary model's settings");
    }

    // The database store, then the one in memory that a test host swaps in for it
    private static async Task StoresAsync()
    {
        await using (var app = Build(Registration.ShowEfCoreRegistration))
        {
            Expect(app.GetRequiredService<IConversationStore<int>>() is EfTypeshiftStore, "the store");
            Expect(app.GetService<IDbContextFactory<TypeshiftDb>>() is not null, "the database");
        }

        await using var test = Build(services =>
        {
            Registration.ShowEfCoreRegistration(services);
            Registration.ShowTestHost(services);
        });

        Expect(test.GetRequiredService<IConversationStore<int>>() is InMemoryTypeshiftStore<int>, "the swapped store");
    }

    private static async Task ChatClientsAsync(IChatClient client)
    {
        await using (var keyed = Build(services => Models.ShowKeyedChatClients(services, client, client)))
        {
            Expect(keyed.GetKeyedService<IChatClient>("openai") is not null, "a client under the key openai");
            Expect(keyed.GetKeyedService<IChatClient>("anthropic") is not null, "a client under the key anthropic");
        }

        await using (var one = Build(services => Models.ShowChatClient(services, client)))
        {
            Expect(one.GetService<IChatClient>() is not null, "one client for every agent");
        }

        await using (var openAi = Build(services => Models.ShowOpenAiClient(services, Key)))
        {
            Expect(Model(openAi, "openai") == "gpt-5-mini", "OpenAI's client under the key openai");
        }

        await using (var anthropic = Build(services => Models.ShowAnthropicClient(services, Key)))
        {
            Expect(Model(anthropic, "anthropic") == "claude-sonnet-5-5", "Anthropic's client under the key anthropic");
        }

        await using (var local = Build(Models.ShowLocalClient))
        {
            var metadata = local.GetRequiredKeyedService<IChatClient>("local").GetService<ChatClientMetadata>();
            Expect(metadata?.ProviderUri == new Uri("http://localhost:1234/v1"), "the local server's endpoint");
        }

        var providers = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:openai:ApiKey"] = Key,
                ["Providers:local:Endpoint"] = "http://localhost:1234/v1",
                ["Providers:local:ApiKey"] = "none"
            })
            .Build();

        await using (var configured = Build(services => Models.ShowProvidersFromConfiguration(services, providers)))
        {
            Expect(Model(configured, "openai") == "default", "a client for each provider of the configuration");
            Expect(Model(configured, "local") == "default", "a client for each provider of the configuration");
        }

        var unkeyed = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Providers:openai:Endpoint"] = "https://x" })
            .Build();

        Expect(
            await ThrowsAsync<InvalidOperationException>(() =>
            {
                Models.ShowProvidersFromConfiguration(new ServiceCollection(), unkeyed);
                return Task.CompletedTask;
            }),
            "a provider without a key to fail at startup");

        await using (var wrapped = Build(services =>
        {
            services.AddLogging();
            Models.ShowWrappedClient(services, Key);
        }))
        {
            var logging = wrapped.GetRequiredKeyedService<IChatClient>("openai").GetService<LoggingChatClient>();
            Expect(logging is not null, "the client wrapped in logging");
        }

        await using (var guarded = Build(services => OutputGuardrail.ShowRegistration(services, client)))
        {
            var guardrail = guarded.GetRequiredKeyedService<IChatClient>("openai").GetService<RedactedCardNumbers>();
            Expect(guardrail is not null, "the client wrapped in the output guardrail");
        }

        await using (var retrying = Build(services => Models.ShowRetries(services, Key)))
        {
            Expect(Model(retrying, "openai") == "gpt-5-mini", "OpenAI's client with retries");
        }

        using var completions = Models.ShowOpenAiChatClient(Key);
        Expect(completions.GetService<ChatClientMetadata>()?.DefaultModelId == "gpt-5-mini", "a Completions client");

        using var responses = Models.ShowOpenAiResponsesClient(Key);
        Expect(responses.GetService<ChatClientMetadata>()?.DefaultModelId == "gpt-5-mini", "a Responses client");

        using var beta = Models.ShowAnthropicBetaClient(Key);
        Expect(beta.GetService<ChatClientMetadata>()?.DefaultModelId == "claude-sonnet-5-5", "Anthropic's beta client");
    }

    // A client per run, with the customer's key, for the one provider the app serves that way
    private static async Task TenantClientsAsync()
    {
        await using var tenants = Build(services =>
        {
            services.AddScoped<ICurrentCustomer, SampleCustomer>();
            services.AddSingleton<ITenantKeys, SampleTenantKeys>();
            Models.ShowTenantClients(services);
        });

        await using var scope = tenants.CreateAsyncScope();
        var clients = scope.ServiceProvider.GetRequiredService<IChatClientProvider<string>>();

        var openAi = await clients.GetChatClientAsync("openai", "gpt-5-mini", CancellationToken.None);
        Expect(openAi.GetService<ChatClientMetadata>()?.DefaultModelId == "gpt-5-mini", "the customer's client");

        Expect(
            await ThrowsAsync<InvalidOperationException>(async () =>
                await clients.GetChatClientAsync("anthropic", "claude-sonnet-5-5", CancellationToken.None)),
            "no client for a provider the app does not serve per customer");
    }

    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    // The model of the client registered under the provider's key
    private static string? Model(IServiceProvider services, string provider) =>
        services.GetRequiredKeyedService<IChatClient>(provider).GetService<ChatClientMetadata>()?.DefaultModelId;

    private static async Task<bool> ThrowsAsync<TException>(Func<Task> act)
        where TException : Exception
    {
        try
        {
            await act();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
