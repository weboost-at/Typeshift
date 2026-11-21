using System.Runtime.CompilerServices;
using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware.Support;

// A guardrail on what a model says, around the provider's client: every text it answers reads as one fixed text
public sealed class RedactingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public const string Redacted = "[redacted]";

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        Redact(response.Messages.SelectMany(message => message.Contents));
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            Redact(update.Contents);
            yield return update;
        }
    }

    private static void Redact(IEnumerable<AIContent> contents)
    {
        foreach (var text in contents.OfType<TextContent>())
        {
            text.Text = Redacted;
        }
    }
}
