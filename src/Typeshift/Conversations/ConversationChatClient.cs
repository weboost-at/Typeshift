using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift;

// D46, D50: outermost, around the tool loop. It begins every run (its AgentRun, mode and invoke_agent span, D36),
// and for a run with a conversation resumes it, marks where the run begins and ends, keeps a steered run going until
// its input was answered, and publishes the updates of streaming runs (D47). The provider never receives the
// conversation id
internal sealed class ConversationChatClient(IChatClient inner, Agent agent, IServiceProvider services)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.StartRun(agent, options);

        try
        {
            var response = await RunAsync(messages, options, activity, cancellationToken);
            Telemetry.Answered(activity, response.Usage);
            return response;
        }
        catch (Exception exception)
        {
            Telemetry.Failed(activity, exception);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.StartRun(agent, options);
        var usage = new UsageDetails();

        await using var updates = RunStreamingAsync(messages, options, activity, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync()) break;
            }
            catch (Exception exception)
            {
                Telemetry.Failed(activity, exception);
                throw;
            }

            foreach (var content in updates.Current.Contents.OfType<UsageContent>())
            {
                usage.Add(content.Details);
            }

            yield return updates.Current;
        }

        Telemetry.Answered(activity, usage);
    }

    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var (run, call, runOptions) = await BeginAsync(messages, options, activity, cancellationToken);

        if (run is null) return await base.GetResponseAsync(call, runOptions, cancellationToken);

        try
        {
            var last = await base.GetResponseAsync(call, runOptions, cancellationToken);
            var response = last;

            // D51: the run does not end while steering input waits for the model
            while (run.IsSteered)
            {
                call = [.. call, .. last.Messages];
                last = await base.GetResponseAsync(call, runOptions, cancellationToken);
                response = ChatResponses.Merge(response, last);
            }

            await run.EndAsync(null);
            return response;
        }
        catch (Exception exception)
        {
            await run.EndAsync(exception.Message);
            throw;
        }
        finally
        {
            Runs.End(run);
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (run, call, runOptions) = await BeginAsync(messages, options, activity, cancellationToken);

        if (run is null)
        {
            await foreach (var update in base.GetStreamingResponseAsync(call, runOptions, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var publisher = services.GetService<RealtimeTrackingOptions>() is { } realtime
            ? new LivePublisher(Runs, run.Conversation, realtime, Time)
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
            await run.EndAsync(null);
        }
        finally
        {
            publisher?.Flush();

            // A stream that failed, was cancelled or was left early still ends its run
            if (!ended) await run.EndAsync("The run stopped before it answered.");
            Runs.End(run);
        }
    }

    private LiveRuns Runs => services.GetRequiredService<LiveRuns>();

    private TimeProvider Time => services.GetService<TimeProvider>() ?? TimeProvider.System;

    private IStoreAccess Store => services.GetRequiredService<IStoreAccess>();

    // The id a typed run passes, or the raw path's ChatOptions.ConversationId in the build's id type
    private object? GetConversation(ChatOptions options) =>
        options switch
        {
            ResolvedChatOptions { Conversation: { } conversation } => conversation,
            { ConversationId: { } text } => Store.Parse(text),
            _ => null
        };

    private async Task<(ConversationRun? Run, List<ChatMessage> Call, ResolvedChatOptions Options)> BeginAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        CancellationToken ct)
    {
        var input = messages.ToList();
        var runOptions = ResolvedChatOptions.From(options ?? new ChatOptions());
        var mode = runOptions.RunOptions.Mode;

        if (GetConversation(runOptions) is not { } conversation)
        {
            runOptions.AgentRun = new AgentRun(agent, input, runOptions, services, mode) { Activity = activity };
            return (null, input, runOptions);
        }

        var entries = await Store.LoadConversationAsync(conversation, ct);

        // D50: a run without input continues the conversation's interrupted run
        if (input.Count == 0 && (entries.Count == 0 || entries[^1].EndsRun))
        {
            throw new InvalidOperationException(
                $"Conversation {conversation} has no interrupted run to resume: run it with input instead.");
        }

        // D52: a conversation keeps its mode, unless the run's settings name one
        if (!runOptions.RunSetsMode && entries.LastOrDefault(entry => entry.Mode is not null)?.Mode is { } kept)
            mode = kept;

        var history = ConversationHistory.From(entries);
        List<ChatMessage> call = [.. history, .. input];
        var agentRun = new AgentRun(agent, call, runOptions, services, mode)
        {
            InputStart = history.Count,
            Activity = activity
        };

        var logger = (services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
            .CreateLogger<ConversationChatClient>();

        var run = new ConversationRun(conversation, agentRun, runOptions.ModelId ?? "", Store, Time, logger);

        runOptions.ConversationId = null;
        runOptions.AgentRun = agentRun;
        runOptions.ConversationRun = run;

        Runs.Begin(run);
        run.MarkStored(history);

        if (input.Count > 0) await run.AppendAsync(input);

        return (run, call, runOptions);
    }
}
