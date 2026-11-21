using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Scenarios;

// Anthropic's clients, in a file of their own so the regions of Models.cs, which the README shows, keep their lines
internal static partial class Models
{
    public static void ShowAnthropicClient(IServiceCollection services, string apiKey)
    {
        // begin-snippet: anthropic-client
        var anthropic = new AnthropicClient { ApiKey = apiKey }
            .AsIChatClient("claude-sonnet-5-5");

        services.AddKeyedChatClient("anthropic", anthropic);
        // end-snippet
    }

    public static IChatClient ShowAnthropicBetaClient(string apiKey)
    {
        // begin-snippet: anthropic-beta-client
        var client = new AnthropicClient { ApiKey = apiKey }
            .Beta
            .AsIChatClient("claude-sonnet-5-5");
        // end-snippet

        return client;
    }
}
