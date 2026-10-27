using System.ClientModel;
using Microsoft.Extensions.AI;
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
