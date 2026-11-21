using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D32: inside the tool loop, below the checkpoints, which store each step before compaction sees it: before each
// model call of a run with a maximum of input tokens, the history the model sees shrinks to the target
internal sealed class CompactingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run, RunOptions.MaxInputTokens: { } max } resolved)
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        var compaction = run.Compaction ??= new Compaction(run.LastCall);
        var call = await compaction.PrepareAsync(messages, resolved, max, cancellationToken).ConfigureAwait(false);

        var response = await base.GetResponseAsync(call, options, cancellationToken).ConfigureAwait(false);
        compaction.Calibrate(response.Usage);
        return response;
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        options is ResolvedChatOptions { AgentRun: { } run, RunOptions.MaxInputTokens: { } max } resolved
            ? StreamAsync(messages, resolved, run, max, cancellationToken)
            : base.GetStreamingResponseAsync(messages, options, cancellationToken);

    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions resolved,
        AgentRun run,
        int max,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var compaction = run.Compaction ??= new Compaction(run.LastCall);
        var call = await compaction.PrepareAsync(messages, resolved, max, cancellationToken).ConfigureAwait(false);
        var answer = base.GetStreamingResponseAsync(call, resolved, cancellationToken);
        UsageDetails? usage = null;

        await foreach (var update in answer.ConfigureAwait(false))
        {
            foreach (var content in update.Contents.OfType<UsageContent>())
            {
                (usage ??= new UsageDetails()).Add(content.Details);
            }

            yield return update;
        }

        compaction.Calibrate(usage);
    }
}
