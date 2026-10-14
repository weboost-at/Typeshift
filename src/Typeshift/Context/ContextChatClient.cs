using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D31: below the run middleware. The agent's context providers add instructions after the agent's, messages ahead
// of the input, which the conversation never stores, and tools for this run only
internal sealed class ContextChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is ResolvedChatOptions { AgentRun.Agent.ContextProviders.Count: > 0 } resolved)
            (messages, options) = await AddContextAsync(messages, resolved, cancellationToken);

        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is ResolvedChatOptions { AgentRun.Agent.ContextProviders.Count: > 0 } resolved)
            (messages, options) = await AddContextAsync(messages, resolved, cancellationToken);

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static async Task<(List<ChatMessage>, ChatOptions)> AddContextAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions resolved,
        CancellationToken ct)
    {
        var run = resolved.AgentRun!;
        var call = messages.ToList();
        var options = (ResolvedChatOptions)resolved.Clone();
        var at = Math.Min(run.InputStart, call.Count);

        foreach (var create in run.Agent.ContextProviders)
        {
            var context = await create(run.Services).GetContextAsync(run, ct);

            if (context.Instructions is { } instructions)
            {
                options.Instructions = options.Instructions is { } own ? $"{own}\n\n{instructions}" : instructions;
            }

            call.InsertRange(at, context.Messages);
            at += context.Messages.Count;

            options.Tools = [.. options.Tools ?? [], .. context.Tools];
            resolved.ConversationRun?.MarkTransient(context.Messages);
        }

        return (call, options);
    }
}
