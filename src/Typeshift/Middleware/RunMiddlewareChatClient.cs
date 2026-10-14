using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D30: below the conversation, around each pass of a run: the run middleware the run's services resolve, the first
// registered outermost. A response middleware sets without calling next answers in place of the model, and the
// conversation stores it
internal sealed class RunMiddlewareChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run } resolved
            || run.Services.GetServices<IRunMiddleware>().ToList() is not { Count: > 0 } middleware)
            return await base.GetResponseAsync(messages, options, cancellationToken);

        Begin(run, messages);
        var called = false;

        await InvokeAsync(middleware, 0, run, async () =>
        {
            called = true;
            Keep(run, resolved);
            run.Response = await InnerClient.GetResponseAsync(run.Messages, resolved, cancellationToken);
        }, cancellationToken);

        return await AnsweredAsync(run, resolved, called);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run } resolved
            || run.Services.GetServices<IRunMiddleware>().ToList() is not { Count: > 0 } middleware)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        Begin(run, messages);

        // The middleware awaits next while the updates stream on to the caller
        var updates = Channel.CreateUnbounded<ChatResponseUpdate>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var called = false;

        var chain = RunChainAsync(middleware, run, updates.Writer, async () =>
        {
            called = true;
            Keep(run, resolved);
            var arrived = new List<ChatResponseUpdate>();

            await foreach (var update in InnerClient.GetStreamingResponseAsync(run.Messages, resolved, stop.Token))
            {
                arrived.Add(update);
                await updates.Writer.WriteAsync(update, stop.Token);
            }

            run.Response = arrived.ToChatResponse();
        }, stop.Token);

        try
        {
            await foreach (var update in updates.Reader.ReadAllAsync(CancellationToken.None))
            {
                yield return update;
            }

            await chain;

            if (!called)
            {
                foreach (var update in (await AnsweredAsync(run, resolved, called)).ToChatResponseUpdates())
                {
                    yield return update;
                }
            }
        }
        finally
        {
            // A caller that left early stops the run, and no longer hears how it ended
            if (!chain.IsCompleted)
            {
                await stop.CancelAsync();
                await chain.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private static void Begin(AgentRun run, IEnumerable<ChatMessage> messages)
    {
        run.Messages = [.. messages];
        run.Response = null;
    }

    // What middleware changed in the messages holds for this pass only: the conversation keeps what the host sent
    private static void Keep(AgentRun run, ResolvedChatOptions options) =>
        options.ConversationRun?.MarkTransient(run.Messages);

    private static async Task RunChainAsync(
        List<IRunMiddleware> middleware,
        AgentRun run,
        ChannelWriter<ChatResponseUpdate> updates,
        Func<Task> model,
        CancellationToken ct)
    {
        try
        {
            await InvokeAsync(middleware, 0, run, model, ct);
        }
        finally
        {
            updates.TryComplete();
        }
    }

    private static Task InvokeAsync(
        List<IRunMiddleware> middleware,
        int index,
        AgentRun run,
        Func<Task> model,
        CancellationToken ct) =>
        index == middleware.Count
            ? model()
            : middleware[index].InvokeAsync(run, () => InvokeAsync(middleware, index + 1, run, model, ct), ct);

    private static async Task<ChatResponse> AnsweredAsync(AgentRun run, ResolvedChatOptions options, bool called)
    {
        var response = run.Response
            ?? throw new InvalidOperationException(
                "A run middleware returned without calling next or setting the run's response.");

        if (called) return response;

        foreach (var message in response.Messages.Where(message => message.Role != ChatRole.User))
        {
            message.AuthorName ??= run.Agent.Name;
        }

        if (options.ConversationRun is { } conversation) await conversation.AppendAsync([.. response.Messages]);
        return response;
    }
}
