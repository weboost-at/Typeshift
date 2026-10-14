using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D50: inside the tool loop, before each model call: the steps since the last call are stored (tool results, then
// steering input, which goes into the call), and each response is stored the moment it exists
internal sealed class CheckpointChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { ConversationRun: { } run })
            return await base.GetResponseAsync(messages, options, cancellationToken);

        var call = await run.PrepareCallAsync(messages);
        var response = await base.GetResponseAsync(call, options, cancellationToken);

        await run.AppendResponseAsync(response, options, streamed: false);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { ConversationRun: { } run })
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var call = await run.PrepareCallAsync(messages);
        var updates = new List<ChatResponseUpdate>();

        // A stream that fails, is cancelled or is left early is stored with the updates that arrived
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(call, options, cancellationToken))
            {
                updates.Add(update);
                yield return update;
            }
        }
        finally
        {
            if (updates.Count > 0) await run.AppendResponseAsync(updates.ToChatResponse(), options, streamed: true);
        }
    }
}
