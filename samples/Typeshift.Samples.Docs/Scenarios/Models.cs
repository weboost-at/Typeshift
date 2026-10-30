using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

internal static class Models
{
    public static void ShowChatClientProvider(IServiceCollection services, IChatClient openAi, IChatClient anthropic)
    {
        // begin-snippet: chat-client-provider-registration
        services.AddTypeshift()
            .WithChatClientProvider<KeyedChatClientProvider>();

        services.AddKeyedSingleton("openai", openAi);
        services.AddKeyedSingleton("anthropic", anthropic);
        // end-snippet
    }

    public static void ShowOpenAiClient(IServiceCollection services, string apiKey)
    {
        // begin-snippet: openai-client
        var openAi = new OpenAIClient(apiKey)
            .GetChatClient("gpt-5-mini")
            .AsIChatClient();

        services.AddKeyedSingleton("openai", openAi);
        // end-snippet
    }

    public static void ShowLocalClient(IServiceCollection services)
    {
        // begin-snippet: local-chat-client
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("http://localhost:1234/v1")
        };

        var local = new OpenAIClient(new ApiKeyCredential("none"), options)
            .GetChatClient("qwen3-8b")
            .AsIChatClient();

        services.AddKeyedSingleton("local", local);
        // end-snippet
    }

    public static void ShowProvidersFromConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        // begin-snippet: providers-from-configuration
        foreach (var provider in configuration.GetSection("Providers").GetChildren())
        {
            var options = new OpenAIClientOptions();
            if (provider["Endpoint"] is { } endpoint) options.Endpoint = new Uri(endpoint);

            var key = provider["ApiKey"]
                ?? throw new InvalidOperationException($"Providers:{provider.Key}:ApiKey is not configured.");

            var client = new OpenAIClient(new ApiKeyCredential(key), options)
                .GetChatClient("default")
                .AsIChatClient();

            services.AddKeyedSingleton(provider.Key, client);
        }
        // end-snippet
    }

    public static void ShowTenantClients(IServiceCollection services)
    {
        // begin-snippet: tenant-chat-client-provider-registration
        services.AddTypeshift()
            .WithChatClientProvider<TenantChatClientProvider>();
        // end-snippet
    }

    public static void ShowWrappedClient(IServiceCollection services, string apiKey)
    {
        // begin-snippet: wrapped-chat-client
        var openAi = new OpenAIClient(apiKey)
            .GetChatClient("gpt-5-mini")
            .AsIChatClient();

        services.AddKeyedChatClient("openai", openAi)
            .UseLogging();
        // end-snippet
    }

    public static void ShowRetries(IServiceCollection services, string apiKey)
    {
        // begin-snippet: client-retries
        var options = new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 5)
        };

        var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), options)
            .GetChatClient("gpt-5-mini")
            .AsIChatClient();

        services.AddKeyedSingleton("openai", openAi);
        // end-snippet
    }

    public static CodingAgent.Settings ShowSummaryProvider()
    {
        // begin-snippet: summary-provider
        var settings = new CodingAgent.Settings
        {
            SummaryProvider = "openai",
            SummaryModel = "gpt-5-mini"
        };
        // end-snippet

        return settings;
    }
}
