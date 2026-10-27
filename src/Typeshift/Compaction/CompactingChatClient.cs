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
            return await base.GetResponseAsync(messages, options, cancellationToken);

        var compaction = run.Compaction ??= Start(run, resolved);
        var call = await compaction.PrepareAsync(messages, resolved, max, cancellationToken);

        var response = await base.GetResponseAsync(call, options, cancellationToken);
        compaction.Calibrate(call, resolved, response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run, RunOptions.MaxInputTokens: { } max } resolved)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var compaction = run.Compaction ??= Start(run, resolved);
        var call = await compaction.PrepareAsync(messages, resolved, max, cancellationToken);
        UsageDetails? usage = null;

        await foreach (var update in base.GetStreamingResponseAsync(call, options, cancellationToken))
        {
            foreach (var content in update.Contents.OfType<UsageContent>())
            {
                (usage ??= new UsageDetails()).Add(content.Details);
            }

            yield return update;
        }

        compaction.Calibrate(call, resolved, usage);
    }

    // A run's first estimate is calibrated by the conversation's last call the provider reported on
    private static Compaction Start(AgentRun run, ResolvedChatOptions options)
    {
        var compaction = new Compaction();
        if (run.LastCall is { } last) compaction.Calibrate(last.Call, options, last.Usage);
        return compaction;
    }
}
