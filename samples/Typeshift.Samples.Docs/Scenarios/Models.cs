using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
