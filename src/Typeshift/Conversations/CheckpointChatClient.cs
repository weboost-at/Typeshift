using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D50: inside the tool loop, before each model call: the steps since the last call are stored (tool results, then
// steering input, which goes into the call), and each response is stored the moment it exists. It counts the run's
// model calls too, as the one layer that sees each
internal sealed class CheckpointChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Count(options);

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
        Count(options);

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

        // A stream that fails, is cancelled or is left early is stored with the updates that arrived. The tool loop
        // swaps the calls that need approval out of an update's contents, so the contents are copied as they arrive
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(call, options, cancellationToken))
            {
                var arrived = update.Clone();
                arrived.Contents = [.. update.Contents];
                updates.Add(arrived);

                yield return update;
            }
        }
        finally
        {
            if (updates.Count > 0) await run.AppendResponseAsync(updates.ToChatResponse(), options, streamed: true);
        }
    }

    private static void Count(ChatOptions? options)
    {
        if (options is ResolvedChatOptions { AgentRun: { } run }) run.ModelCalls++;
    }
}
