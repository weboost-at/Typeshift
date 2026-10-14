using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift;

// D46, D50: outermost, around the tool loop. It resumes the conversation, marks where a run begins and ends, keeps a
// steered run going until its input was answered, and publishes the updates of streaming runs (D47). The provider
// never receives the conversation id
internal sealed class ConversationChatClient(IChatClient inner, string agent, IServiceProvider services)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (GetConversation(options) is not { } conversation)
            return await base.GetResponseAsync(messages, options, cancellationToken);

        var (run, call, runOptions) = await BeginAsync(conversation, messages, options, cancellationToken);

        try
        {
            var last = await base.GetResponseAsync(call, runOptions, cancellationToken);
            var response = last;

            // D51: the run does not end while steering input waits for the model
            while (run.IsSteered)
            {
                call = [.. call, .. last.Messages];
                last = await base.GetResponseAsync(call, runOptions, cancellationToken);
                response = Merge(response, last);
            }

            await run.AppendAsync([], endsRun: true);
            return response;
        }
        catch (Exception exception)
        {
            await EndFailedAsync(run, exception);
            throw;
        }
        finally
        {
            Runs.End(run);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (GetConversation(options) is not { } conversation)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var (run, call, runOptions) = await BeginAsync(conversation, messages, options, cancellationToken);
        var publisher = services.GetService<RealtimeTrackingOptions>() is { } realtime
            ? new LivePublisher(Runs, conversation, realtime, Time)
            : null;

        var ended = false;

        try
        {
            do
            {
                var updates = new List<ChatResponseUpdate>();

                await foreach (var update in base.GetStreamingResponseAsync(call, runOptions, cancellationToken))
                {
                    updates.Add(update);
                    publisher?.Add(update);
                    yield return update;
                }

                call = [.. call, .. updates.ToChatResponse().Messages];
            }
            while (run.IsSteered);

            ended = true;
            await run.AppendAsync([], endsRun: true);
        }
        finally
        {
            publisher?.Flush();

            // A stream that failed, was cancelled or was left early still ends its run
            if (!ended) await EndFailedAsync(run, null);
            Runs.End(run);
        }
    }

    private LiveRuns Runs => services.GetRequiredService<LiveRuns>();

    private TimeProvider Time => services.GetService<TimeProvider>() ?? TimeProvider.System;

    private IStoreAccess Store => services.GetRequiredService<IStoreAccess>();

    // The id a typed run passes, or the raw path's ChatOptions.ConversationId in the build's id type
    private object? GetConversation(ChatOptions? options) =>
        options switch
        {
            ResolvedChatOptions { Conversation: { } conversation } => conversation,
            { ConversationId: { } text } => Store.Parse(text),
            _ => null
        };

    private async Task<(ConversationRun Run, List<ChatMessage> Call, ResolvedChatOptions Options)> BeginAsync(
        object conversation,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken ct)
    {
        var input = messages.ToList();
        var entries = await Store.LoadConversationAsync(conversation, ct);

        // D50: a run without input continues the conversation's interrupted run
        if (input.Count == 0 && (entries.Count == 0 || entries[^1].EndsRun))
        {
            throw new InvalidOperationException(
                $"Conversation {conversation} has no interrupted run to resume: run it with input instead.");
        }

        var runOptions = ResolvedChatOptions.From(options ?? new ChatOptions());
        var logger = (services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
            .CreateLogger<ConversationChatClient>();

        var run = new ConversationRun(conversation, agent, runOptions.ModelId ?? "", Store, Time, logger);

        runOptions.ConversationId = null;
        runOptions.ConversationRun = run;

        Runs.Begin(run);

        var history = ConversationHistory.From(entries);
        run.MarkStored(history);

        if (input.Count > 0) await run.AppendAsync(input);

        return (run, [.. history, .. input], runOptions);
    }

    // D46: a failed or cancelled run ends with what went wrong, which a later run leaves out
    private static Task EndFailedAsync(ConversationRun run, Exception? exception)
    {
        var error = new ErrorContent(exception?.Message ?? "The run stopped before it answered.");
        return run.AppendAsync([new ChatMessage(ChatRole.Assistant, [error])], endsRun: true);
    }

    private static ChatResponse Merge(ChatResponse first, ChatResponse next)
    {
        var merged = new ChatResponse([.. first.Messages, .. next.Messages])
        {
            ModelId = next.ModelId ?? first.ModelId,
            FinishReason = next.FinishReason,
            Usage = first.Usage
        };

        if (next.Usage is { } usage) (merged.Usage ??= new UsageDetails()).Add(usage);
        return merged;
    }
}
