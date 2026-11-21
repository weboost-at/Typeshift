using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D26: the messages of a run carry the agent that wrote them
internal sealed class AuthorNameChatClient(IChatClient inner, string name) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        // The checkpoints name what they store, but the gate's answer to a call it could not run, a handoff next to
        // one that waits, is stored only after run middleware read the response
        foreach (var message in response.Messages)
        {
            message.AuthorName ??= name;
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var answer = base.GetStreamingResponseAsync(messages, options, cancellationToken);

        await foreach (var update in answer.ConfigureAwait(false))
        {
            update.AuthorName ??= name;
            yield return update;
        }
    }
}
