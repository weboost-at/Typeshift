using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift;

// D46, D50: outermost, around the tool loop. It begins every run (its AgentRun, mode and invoke_agent span, D36),
// and for a run with a conversation resumes it, marks where the run begins and ends, and publishes the updates of
// streaming runs (D47). A run goes on while steering input or a background sub-agent's answer waits (D33, D51), and
// a handoff continues it with the agent handed to (D34). The provider never receives the conversation id
internal sealed class ConversationChatClient(IChatClient inner, Agent agent, IServiceProvider services)
    : DelegatingChatClient(inner)
{
    private const string HandoffPrefix = "handoff_to_";

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

    private LiveRuns Runs => services.GetRequiredService<LiveRuns>();

    private TimeProvider Time => services.GetService<TimeProvider>() ?? TimeProvider.System;

    private IStoreAccess Store => services.GetRequiredService<IStoreAccess>();

    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        CancellationToken ct)
    {
        var (conversation, entries) = await LoadAsync(options, ct);

        if (AnsweringAgent(entries) is { } answering)
            return await answering.RunAsync(messages, SettingsId(options), conversation, null, ct);

        var (run, call, runOptions) = await BeginAsync(messages, options, conversation, entries, activity);
        run.Publisher = CreatePublisher(run);
        var handedOff = false;

        try
        {
            var calls = runOptions.AgentRun!.ModelCalls;
            var last = await base.GetResponseAsync(call, runOptions, ct);
            var response = last;

            while (true)
            {
                await run.StoreResultsAsync(last.Messages);

                var handoff = HandoffIn(last.Messages) is { } handing ? await HandOffAsync(run, handing) : null;

                if (handoff is var (result, target))
                {
                    handedOff = true;
                    var answer = await target.RunAsync([], runOptions.SettingsId, run.Conversation, null, ct);

                    return ChatResponses.Merge(ChatResponses.Merge(response, new ChatResponse(result)), answer);
                }

                if (!GoesFurther(runOptions, ref calls) || !await run.GoesOnAsync(ct)) break;

                call = [.. call, .. last.Messages];
                last = await base.GetResponseAsync(call, runOptions, ct);
                response = ChatResponses.Merge(response, last);
            }

            await run.EndAsync(null);
            return response;
        }
        catch (Exception exception) when (!handedOff)
        {
            await run.EndAsync(exception.Message);
            throw;
        }
        finally
        {
            run.Publisher?.Flush();
            Runs.End(run);
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (conversation, entries) = await LoadAsync(options, ct);

        if (AnsweringAgent(entries) is { } answering)
        {
            await foreach (var update in answering.RunStreamingAsync(messages, SettingsId(options), conversation, ct))
            {
                yield return update;
            }

            yield break;
        }

        var (run, call, runOptions) = await BeginAsync(messages, options, conversation, entries, activity);

        var publisher = CreatePublisher(run);

        var ended = false;
        var calls = runOptions.AgentRun!.ModelCalls;

        try
        {
            while (true)
            {
                var updates = new List<ChatResponseUpdate>();

                await foreach (var update in base.GetStreamingResponseAsync(call, runOptions, ct))
                {
                    updates.Add(update);
                    publisher?.Add(update);
                    yield return update;
                }

                // Rebuilt from the updates, the messages hold new text the checkpoints stored already
                var last = updates.ToChatResponse().Messages;
                await run.StoreResultsAsync(last);
                run.MarkStored(last);

                if (HandoffIn(last) is { } handoff && await HandOffAsync(run, handoff) is (var result, var target))
                {
                    ended = true;

                    yield return new ChatResponseUpdate(ChatRole.Tool, result.Contents);

                    var answer = target.RunStreamingAsync([], runOptions.SettingsId, run.Conversation, ct);

                    await foreach (var update in answer)
                    {
                        yield return update;
                    }

                    yield break;
                }

                if (!GoesFurther(runOptions, ref calls) || !await run.GoesOnAsync(ct)) break;

                call = [.. call, .. last];
            }

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

    // D47: only a conversation has watchers, and only realtime tracking feeds them
    private LivePublisher? CreatePublisher(ConversationRun run) =>
        run.Conversation is not null && services.GetService<RealtimeTrackingOptions>() is { } realtime
            ? new LivePublisher(Runs, run.Conversation, realtime, Time)
            : null;

    // The id a typed run passes, or the raw path's ChatOptions.ConversationId in the build's id type
    private object? GetConversation(ChatOptions options) =>
        options switch
        {
            ResolvedChatOptions { Conversation: { } conversation } => conversation,
            { ConversationId: { } text } => Store.Parse(text),
            _ => null
        };

    private static object? SettingsId(ChatOptions? options) => (options as ResolvedChatOptions)?.SettingsId;

    // D30, D32: a pass that called no model (run middleware answered) or the run's limit spent ends the run; the
    // input still waiting is stored with its end
    private static bool GoesFurther(ResolvedChatOptions options, ref int calls)
    {
        var made = options.AgentRun!.ModelCalls;
        var further = made > calls && made <= options.RunOptions.MaxToolIterations;

        calls = made;
        return further;
    }

    // The run's conversation from its last summary, loaded once: for the agent that answers and for the run's history
    private async Task<(object? Conversation, IReadOnlyList<ConversationEntry> Entries)> LoadAsync(
        ChatOptions? options,
        CancellationToken ct)
    {
        var conversation = options is null ? null : GetConversation(options);
        if (conversation is null) return (null, []);

        var entries = await Store.LoadConversationSinceCompactionAsync(conversation, ct);

        // D24: a summary an earlier version stored keeps no standing approvals, so the run reads them all
        if (entries is [{ IsSummary: true, StandingApprovals: null }, ..])
        {
            entries = await Store.LoadConversationAsync(conversation, ct);
        }

        return (conversation, entries);
    }

    // D34: the agent that answered the conversation last answers its next run, if this agent's handoffs reach it
    private IAgentHost? AnsweringAgent(IReadOnlyList<ConversationEntry> entries) =>
        agent.Handoffs.Count > 0 && entries.Count > 0 && entries[^1].Agent != agent.Name
            ? Reachable().GetValueOrDefault(entries[^1].Agent)
            : null;

    private Dictionary<string, IAgentHost> Reachable()
    {
        var reached = new Dictionary<string, IAgentHost>();
        var next = new Queue<Agent>([agent]);

        while (next.TryDequeue(out var from))
        {
            foreach (var host in from.Handoffs.Select(handoff => handoff(services)))
            {
                if (host.Agent.Name != agent.Name && reached.TryAdd(host.Agent.Name, host)) next.Enqueue(host.Agent);
            }
        }

        return reached;
    }

    // A call the tool loop answered, as it answers a tool it does not know, is none
    private FunctionCallContent? HandoffIn(IList<ChatMessage> messages)
    {
        if (agent.Handoffs.Count == 0) return null;

        var contents = messages.SelectMany(message => message.Contents).ToList();
        var answered = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        return contents.OfType<FunctionCallContent>()
            .LastOrDefault(call => call.Name.StartsWith(HandoffPrefix, StringComparison.Ordinal)
                && !answered.Contains(call.CallId));
    }

    // D34: the call's result is stored, and the run is the other agent's from here: it resumes the conversation,
    // so this agent neither ends the run nor holds it; input steered to it is stored for the other agent. Null for a
    // call of no agent this one hands off to
    private async Task<(ChatMessage Result, IAgentHost Target)?> HandOffAsync(
        ConversationRun run,
        FunctionCallContent call)
    {
        var target = agent.Handoffs.Select(handoff => handoff(services))
            .FirstOrDefault(host => $"{HandoffPrefix}{host.Agent.Name}" == call.Name);

        if (target is null) return null;

        var result = new ChatMessage(
            ChatRole.Tool,
            [new FunctionResultContent(call.CallId, $"Handed off to {target.Agent.Name}.")]);

        await run.AppendHandoffAsync(result, target.Agent.Name);
        await run.CloseAsync(target.Agent.Name);
        Runs.End(run);

        return (result, target);
    }

    private async Task<(ConversationRun Run, List<ChatMessage> Call, ResolvedChatOptions Options)> BeginAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        object? conversation,
        IReadOnlyList<ConversationEntry> entries,
        Activity? activity)
    {
        // D24: a request is the model's, never the host's; one in the input is dropped before anything stores it
        var input = messages.Select(WithoutRequests).OfType<ChatMessage>().ToList();
        var runOptions = ResolvedChatOptions.From(options ?? new ChatOptions());

        // Options the raw path made itself run with the agent's provider and run options, as its tool loop does
        if (options is not ResolvedChatOptions)
        {
            runOptions.Provider = agent.Provider;
            runOptions.RunOptions = agent.RunOptions;
        }

        var mode = runOptions.RunOptions.Mode;

        // D50: a run without input continues the conversation's interrupted run
        if (conversation is not null && input.Count == 0 && (entries.Count == 0 || entries[^1].EndsRun))
        {
            throw new InvalidOperationException(
                $"Conversation {conversation} has no interrupted run to resume: run it with input instead.");
        }

        // D52: a conversation keeps its mode, unless the run's settings name one
        if (!runOptions.RunSetsMode && entries.LastOrDefault(entry => entry.Mode is not null)?.Mode is { } kept)
            mode = kept;

        // D33: the background sub-agents of a run that stopped are lost; the model hears so, once
        input = [.. LostBackground(entries), .. input];

        var history = ConversationHistory.From(entries);
        List<ChatMessage> call = [.. history, .. input];

        // D24: a standing approval holds for the whole conversation, beyond what compaction left of it
        var summarized = entries.FirstOrDefault()?.StandingApprovals ?? [];
        var granted = ApprovalChatClient.StandingIn(entries.SelectMany(entry => entry.Messages));

        var agentRun = new AgentRun(agent, call, runOptions, services, mode)
        {
            InputStart = history.Count,
            Activity = activity,
            Standing = [.. summarized, .. granted],
            LastCall = LastCall(entries)
        };

        var logger = (services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
            .CreateLogger<ConversationChatClient>();

        var run = new ConversationRun(conversation, agentRun, runOptions.ModelId ?? "", Store, Time, logger);

        runOptions.ConversationId = null;
        runOptions.AgentRun = agentRun;
        runOptions.ConversationRun = run;

        // D34: handoffs need a conversation to continue in
        if (conversation is not null && agent.Handoffs.Count > 0)
        {
            var handoffs = agent.Handoffs.Select(handoff => Declare(handoff(services)));
            runOptions.Tools = [.. runOptions.Tools ?? [], .. handoffs];
        }

        Runs.Begin(run);
        run.MarkStored(history);

        // D59: the ids of what the history leaves out, and of the background calls a summary left working
        run.HoldCallIds(UniqueCallIdsChatClient.Ids(entries.SelectMany(entry => entry.Messages)));
        run.HoldCallIds(entries.FirstOrDefault()?.BackgroundCalls?.Keys ?? []);

        if (input.Count > 0) await run.AppendAsync(input);

        return (run, call, runOptions);
    }

    // D32: the messages of the last model call whose provider reported its input tokens, and that report
    private static ReportedCall? LastCall(IReadOnlyList<ConversationEntry> entries)
    {
        var last = entries.ToList().FindLastIndex(entry => entry.Usage?.InputTokenCount is not null);

        return last < 0
            ? null
            : new ReportedCall(ConversationHistory.From([.. entries.Take(last)]), entries[last].Usage!);
    }

    private static ChatMessage? WithoutRequests(ChatMessage message)
    {
        if (!message.Contents.Any(content => content is ToolApprovalRequestContent)) return message;

        var kept = message.Clone();
        kept.Contents = [.. message.Contents.Where(content => content is not ToolApprovalRequestContent)];
        return kept.Contents.Count > 0 ? kept : null;
    }

    private static AIFunctionDeclaration Declare(IAgentHost target) =>
        AIFunctionFactory.CreateDeclaration(
            $"{HandoffPrefix}{target.Agent.Name}",
            $"Hands the conversation to {target.Agent.Name}: {target.Agent.Description}",
            System.Text.Json.JsonElement.Parse("""{ "type": "object", "properties": {} }"""));

    // The background sub-agents that started, or that a summary left working, and that never answered themselves
    private IEnumerable<ChatMessage> LostBackground(IReadOnlyList<ConversationEntry> entries)
    {
        var background = agent.SubAgents
            .Where(use => use.Background)
            .Select(use => use.Host(services).Agent.Name)
            .ToHashSet();

        if (background.Count == 0) yield break;

        var messages = entries.SelectMany(entry => entry.Messages).ToList();
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var started = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        var answered = messages
            .Select(message => message.AdditionalProperties?.GetValueOrDefault(ConversationRun.BackgroundCall))
            .Select(call => call?.ToString())
            .ToHashSet();

        var working = entries.FirstOrDefault()?.BackgroundCalls ?? new Dictionary<string, string>();

        var lost = contents.OfType<FunctionCallContent>()
            .Where(call => started.Contains(call.CallId))
            .Select(call => (call.CallId, Agent: call.Name))
            .Concat(working.Select(call => (CallId: call.Key, Agent: call.Value)))
            .Where(call => background.Contains(call.Agent) && !answered.Contains(call.CallId))
            .DistinctBy(call => call.CallId);

        foreach (var call in lost)
        {
            yield return new ChatMessage(
                ChatRole.User,
                $"The background agent {call.Agent} was lost when the run stopped; call it again if you need it.")
            {
                AdditionalProperties = new() { [ConversationRun.BackgroundCall] = call.CallId }
            };
        }
    }
}
