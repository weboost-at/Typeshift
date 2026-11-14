using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift;

// D46, D50: outermost, around the tool loop. It begins every run (its AgentRun, mode and invoke_agent span, D36),
// and for a run with a conversation resumes it, settles the decisions it finds before the first model call (D80),
// marks where the run begins and ends, and publishes the updates of streaming runs (D47). A run goes on while
// steering input or a background sub-agent's answer waits (D33, D51), and a handoff continues it with the agent
// handed to (D34). A run that waits for your app returns its decisions after it stored its end. The provider never
// receives the conversation id; the response names it (D73)
internal sealed class ConversationChatClient(IChatClient inner, Agent agent, IServiceProvider services)
    : DelegatingChatClient(inner)
{
    public const string HandoffPrefix = "handoff_to_";

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options = WithNewConversation(messages, options);
        using var activity = Telemetry.StartRun(agent, options, InnerClient);

        try
        {
            var response = await RunAsync(messages, options, activity, cancellationToken);
            if (Named(options) is { } conversation) response.ConversationId = conversation;
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
        options = WithNewConversation(messages, options);
        using var activity = Telemetry.StartRun(agent, options, InnerClient);
        var usage = new UsageDetails();
        var conversation = Named(options);

        await using var updates = RunStreamingAsync(messages, options, activity, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            // D36: an iterator resumes in its caller's context, which would leave the run's later spans outside it
            if (activity is not null) Activity.Current = activity;

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

            // A copy, since the tool loop still holds the update, and one that names a conversation would make it
            // leave the history to the service
            yield return conversation is null ? updates.Current : Named(updates.Current, conversation);
        }

        Telemetry.Answered(activity, usage);
    }

    // D73: a run of the host's own, with input and no conversation, starts one when every conversation is kept; a
    // sub-agent's run belongs to its caller's, and a run without input resumes an interrupted run (D50). On the raw
    // path the id is a string, so the response's ConversationId names the same conversation when the app passes it back
    private ChatOptions? WithNewConversation(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        if (options is not ResolvedChatOptions { Conversation: null, ConversationId: null, Caller: null } resolved
            || services.GetService<TypeshiftOptions>() is not { PersistAllConversations: true } typeshift
            || !messages.Any())
            return options;

        var started = (ResolvedChatOptions)resolved.Clone();
        started.Conversation = resolved.HostRun
            ? typeshift.NewConversationId(Time)
            : typeshift.NewRawConversationId(Time);

        return started;
    }

    private static ChatResponseUpdate Named(ChatResponseUpdate update, string conversation)
    {
        var named = update.Clone();
        named.ConversationId = conversation;
        return named;
    }

    // The conversation the run is stored in, as the response names it
    private static string? Named(ChatOptions? options) => options is null ? null : GetConversation(options)?.ToString();

    private LiveRuns Runs => services.GetRequiredService<LiveRuns>();

#pragma warning disable RS0030 // the fallback for a host that registered no TimeProvider
    private TimeProvider Time => services.GetService<TimeProvider>() ?? TimeProvider.System;
#pragma warning restore RS0030

    private StoreAccess Store => services.GetRequiredService<StoreAccess>();

    private DecisionLedger Ledger => new(Store, Time);

    private ILogger Logger =>
        (services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger<ConversationChatClient>();

    private async Task<ChatResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Activity? activity,
        CancellationToken ct)
    {
        var (conversation, entries, claim, answering) = await LoadAsync(options, ct);

        if (answering is not null)
        {
            try
            {
                return await answering.RunAsync(messages, PassedOn(options, conversation, claim), ct);
            }
            finally
            {
                Runs.Release(claim);
            }
        }

        var (run, call, runOptions, waiting) =
            await BeginAsync(messages, options, conversation, entries, claim, activity, ct);

        if (waiting is not null) return new ChatResponse(Asking(waiting, messageId: null));

        var handedOff = false;

        try
        {
            run.Publisher = CreatePublisher(run);
            var calls = runOptions.AgentRun!.ModelCalls;
            var last = await base.GetResponseAsync(call, runOptions, ct);
            var response = last;

            while (true)
            {
                await run.StoreResultsAsync(last.Messages);

                var handoff = HandoffIn(last.Messages) is { } handing
                    ? await HandOffAsync(run, runOptions, last.Messages, handing)
                    : null;

                if (handoff is var (result, target, handed))
                {
                    handedOff = true;
                    ChatResponse answer;

                    try
                    {
                        answer = await target.RunAsync([], handed, ct);
                    }
                    finally
                    {
                        Runs.Release(handed.Claim);
                    }

                    return ChatResponses.Merge(ChatResponses.Merge(response, new ChatResponse(result)), answer);
                }

                if (!GoesFurther(runOptions, ref calls) || !await run.GoesOnAsync(ct)) break;

                call = [.. call, .. last.Messages];

                // D51, D80: steering input passed the batch the run asked by
                if (run.Asked is { } passed)
                {
                    if (await ExpireAsync(run, passed) is not { } results) break;

                    call.Add(results);
                    response = ChatResponses.Merge(response, new ChatResponse(results));
                }

                last = await base.GetResponseAsync(call, runOptions, ct);
                response = ChatResponses.Merge(response, last);
            }

            await run.EndAsync(null);

            // D80: the decisions come last, once the run stored its end, so each one your app sees can be answered
            if (run.Asked is { } asked)
            {
                var asking = Asking(asked, last.Messages.LastOrDefault(IsCalling)?.MessageId);

                response.Messages.Add(asking);
                run.Publisher?.Add(AsUpdate(asking));
            }

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
        var (conversation, entries, claim, answering) = await LoadAsync(options, ct);

        if (answering is not null)
        {
            try
            {
                var answer = answering.RunStreamingAsync(messages, PassedOn(options, conversation, claim), ct);

                await foreach (var update in answer)
                {
                    yield return update;
                }
            }
            finally
            {
                Runs.Release(claim);
            }

            yield break;
        }

        var (run, call, runOptions, waiting) =
            await BeginAsync(messages, options, conversation, entries, claim, activity, ct);

        if (waiting is not null)
        {
            yield return AskingUpdate(waiting, []);
            yield break;
        }

        LivePublisher? publisher = null;
        var ended = false;
        var calls = runOptions.AgentRun!.ModelCalls;
        var updates = new List<ChatResponseUpdate>();

        try
        {
            publisher = CreatePublisher(run);

            while (true)
            {
                updates.Clear();

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

                if (HandoffIn(last) is { } handoff
                    && await HandOffAsync(run, runOptions, last, handoff) is (var result, var target, var handed))
                {
                    ended = true;

                    try
                    {
                        yield return new ChatResponseUpdate(ChatRole.Tool, result.Contents);

                        await foreach (var update in target.RunStreamingAsync([], handed, ct))
                        {
                            yield return update;
                        }
                    }
                    finally
                    {
                        Runs.Release(handed.Claim);
                    }

                    yield break;
                }

                if (!GoesFurther(runOptions, ref calls) || !await run.GoesOnAsync(ct)) break;

                call = [.. call, .. last];

                // D51, D80: steering input passed the batch the run asked by
                if (run.Asked is { } passed)
                {
                    if (await ExpireAsync(run, passed) is not { } results) break;

                    var expired = AsUpdate(results);

                    call.Add(results);
                    publisher?.Add(expired);
                    yield return expired;
                }
            }

            ended = true;
            await run.EndAsync(null);

            // D80: the decisions come in the last update, once the run stored its end, so a reader that stops there
            // leaves nothing half done. It carries the ids of the response that made the calls
            if (run.Asked is { } asked)
            {
                var asking = AskingUpdate(asked, updates);

                publisher?.Add(asking);
                yield return asking;
            }
        }
        finally
        {
            publisher?.Flush();

            // A stream that failed, was cancelled or was left early still ends its run
            if (!ended) await run.EndAsync("The run stopped before it answered.");
            Runs.End(run);
        }
    }

    // D80: the decisions a run waits for travel in a message of their own, in memory only
    private static ChatMessage Asking(DecisionBatch batch, string? messageId) =>
        new(ChatRole.Assistant, [.. batch.Unanswered.Select(AsContent)])
        {
            AuthorName = batch.Agent,
            MessageId = messageId
        };

    private static AIContent AsContent(Decision decision) => new DecisionRequestContent(decision);

    private static ChatResponseUpdate AsUpdate(ChatMessage message) =>
        new(message.Role, message.Contents)
        {
            AuthorName = message.AuthorName,
            MessageId = message.MessageId
        };

    private static ChatResponseUpdate AskingUpdate(DecisionBatch batch, List<ChatResponseUpdate> updates)
    {
        var calling = updates.LastOrDefault(update => update.Contents.Any(content => content is FunctionCallContent));
        var asking = AsUpdate(Asking(batch, calling?.MessageId));

        asking.ResponseId = calling?.ResponseId;
        asking.ModelId = calling?.ModelId;
        asking.CreatedAt = calling?.CreatedAt;
        asking.FinishReason = updates.LastOrDefault(update => update.FinishReason is not null)?.FinishReason;

        return asking;
    }

    private static bool IsCalling(ChatMessage message) =>
        message.Contents.Any(content => content is FunctionCallContent);

    // T5 inside the run that asked: every call of the batch reads that it did not run, and the model then reads the
    // input. So it is for a batch your app withdrew meanwhile (T6). Null when answers came first: the batch then
    // waits for the run that carries them out
    private async Task<ChatMessage?> ExpireAsync(ConversationRun run, DecisionBatch asked)
    {
        if (await run.ExpireAsync() is not { State: BatchState.Expired or BatchState.Cancelled } passed
            || !passed.IsSame(asked))
            return null;

        var closed = DecisionResults.Closed(passed.State);
        var results = DecisionResults.Message(passed.Calls.Select(call => (call.CallId, closed)));

        Telemetry.Settled(run.Activity, passed, passed.Calls, repaired: false);
        if (await run.AppendAsync([results]) is null) await run.CloseAsync(passed);
        return results;
    }

    // D47: only a conversation has watchers, and only realtime tracking feeds them
    private LivePublisher? CreatePublisher(ConversationRun run) =>
        run.Conversation is not null && services.GetService<RealtimeTrackingOptions>() is { } realtime
            ? new LivePublisher(Runs, run.Conversation, realtime, Time)
            : null;

    // D72: the host's conversation, or the raw path's ChatOptions.ConversationId, a string id
    private static StoreId? GetConversation(ChatOptions options) =>
        options switch
        {
            ResolvedChatOptions { Conversation: { } conversation } => conversation,
            { ConversationId: { } text } => new StoreId<string>(text),
            _ => null
        };

    // D34: a run passed on to the agent that answers the conversation keeps its settings id and the format of a
    // typed run (D64), and takes the claim and none of the run's own settings
    private static RunHandover PassedOn(ChatOptions? options, StoreId? conversation, RunClaim? claim)
    {
        var resolved = options as ResolvedChatOptions;

        return new RunHandover(resolved?.SettingsId, conversation, claim, resolved?.ResultFormat);
    }

    // D30, D32: a pass that called no model (run middleware answered) or the run's limit spent ends the run; the
    // input still waiting is stored with its end
    private static bool GoesFurther(ResolvedChatOptions options, ref int calls)
    {
        var made = options.AgentRun!.ModelCalls;
        var further = made > calls && made <= options.RunOptions.MaxToolIterations;

        calls = made;
        return further;
    }

    // The run's conversation from its last summary, claimed and loaded once: for the agent that answers, which takes
    // the claim on, and for the run's history (D50). A run handed a claim takes it in place of claiming (D34)
    private async Task<(
        StoreId? Conversation,
        IReadOnlyList<ConversationEntry> Entries,
        RunClaim? Claim,
        IAgentHost? Answering)> LoadAsync(ChatOptions? options, CancellationToken ct)
    {
        var conversation = options is null ? null : GetConversation(options);
        if (conversation is null) return (null, [], null, null);

        var claim = options is ResolvedChatOptions { Claim: { } handed }
            ? handed
            : await Runs.ClaimAsync(conversation, ct);

        try
        {
            var entries = await Store.LoadConversationSinceCompactionAsync(conversation, ct);

            // D78: said once per load, since the run goes on without what the entries left out
            if (entries.Sum(entry => entry.UnreadContents) is > 0 and var unread)
            {
                Logger.LogWarning(
                    "Conversation {Conversation} holds {Count} contents this version of Microsoft.Extensions.AI "
                    + "cannot read, and the run goes on without them.",
                    conversation,
                    unread);
            }

            // D52: a run whose settings name a mode switches the conversation to it before anyone answers, so the
            // agent the run is passed on or handed to runs in it too (D34)
            if (options is ResolvedChatOptions { RunSetsMode: true } named)
                await Ledger.SetModeAsync(conversation, named.RunOptions.Mode, ct);

            return (conversation, entries, claim, AnsweringAgent(entries));
        }
        catch
        {
            Runs.Release(claim);
            throw;
        }
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

    // The last handoff the response calls. A call the tool loop answered, as it answers a tool it does not know, is
    // none
    private FunctionCallContent? HandoffIn(IList<ChatMessage> messages) =>
        agent.Handoffs.Count == 0
            ? null
            : Unanswered(messages)
                .LastOrDefault(call => call.Name.StartsWith(HandoffPrefix, StringComparison.Ordinal));

    // The calls the tool loop left without a result. It hands a whole response back uninvoked when one of its calls
    // names a declaration, as a handoff is. A call a provider ran itself is none of the loop's
    private static List<FunctionCallContent> Unanswered(IList<ChatMessage> messages)
    {
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var answered = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        return
        [
            .. contents.OfType<FunctionCallContent>()
                .Where(call => !call.InformationalOnly && !answered.Contains(call.CallId))
        ];
    }

    // D34: the call's result is stored, and the run is the other agent's from here: it resumes the conversation,
    // so this agent neither ends the run nor holds it, and hands its claim on with what else the other run takes
    // over; input steered to it is stored for the other agent. The calls made beside the handoff did not run, and
    // each reads so in the same tool message. Null for a call of no agent this one hands off to
    private async Task<(ChatMessage Result, IAgentHost Target, RunHandover Handed)?> HandOffAsync(
        ConversationRun run,
        ResolvedChatOptions options,
        IList<ChatMessage> messages,
        FunctionCallContent call)
    {
        var target = agent.Handoffs.Select(handoff => handoff(services))
            .FirstOrDefault(host => $"{HandoffPrefix}{host.Agent.Name}" == call.Name);

        if (target is null) return null;

        var beside =
            $"This call did not run: the conversation was handed off to {target.Agent.Name} in the same step. "
            + "Call it again if it is still needed.";

        var result = new ChatMessage(
            ChatRole.Tool,
            [
                .. Unanswered(messages)
                    .Where(other => other != call)
                    .Select(other => new FunctionResultContent(other.CallId, beside)),
                new FunctionResultContent(call.CallId, $"Handed off to {target.Agent.Name}.")
            ]);

        await run.AppendHandoffAsync(result, target.Agent.Name);
        await run.CloseAsync(target.Agent.Name);

        var handed = new RunHandover(options.SettingsId, run.Conversation, Runs.HandOff(run), options.ResultFormat);

        return (result, target, handed);
    }

    // The run that began, or, for a run without input whose decisions are still open, the batch that waits
    private async Task<(
        ConversationRun Run,
        List<ChatMessage> Call,
        ResolvedChatOptions Options,
        DecisionBatch? Waiting)> BeginAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        StoreId? conversation,
        IReadOnlyList<ConversationEntry> entries,
        RunClaim? claim,
        Activity? activity,
        CancellationToken ct)
    {
        try
        {
            return await BeginClaimedAsync(messages, options, conversation, entries, claim, activity, ct);
        }
        catch
        {
            Runs.Release(claim);
            throw;
        }
    }

    private async Task<(
        ConversationRun Run,
        List<ChatMessage> Call,
        ResolvedChatOptions Options,
        DecisionBatch? Waiting)> BeginClaimedAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        StoreId? conversation,
        IReadOnlyList<ConversationEntry> entries,
        RunClaim? claim,
        Activity? activity,
        CancellationToken ct)
    {
        var input = messages.ToList();

        // D80: a decision is answered by its id, in the document; approval contents in a run's input answer nothing
        if (input.SelectMany(message => message.Contents).Any(IsApprovalContent))
        {
            throw new ArgumentException(
                "The input carries an approval request or answer of Microsoft.Extensions.AI, which a run does not "
                + "take: answer the decisions of a response by their ids, with AnswerAsync or RecordAnswersAsync.",
                nameof(messages));
        }

        var runOptions = ResolvedChatOptions.From(options ?? new ChatOptions());

        // Options the raw path made itself run with the agent's provider and run options, as its tool loop does
        if (options is not ResolvedChatOptions)
        {
            runOptions.Provider = agent.Provider;
            runOptions.RunOptions = agent.RunOptions;
        }

        var hasInput = input.Count > 0;
        var document = conversation is null ? DecisionDocument.Empty : await Ledger.LoadAsync(conversation, ct);

        // D52: until the document names a mode, the one an earlier version stored on the conversation's entries holds
        var legacy = document.Mode is null ? entries.LastOrDefault(entry => entry.Mode is not null)?.Mode : null;
        var ledger = new DecisionLedger(Store, Time, legacy);

        // D50: a run without input continues the conversation's interrupted run, or carries out what your app answered
        if (conversation is not null
            && !hasInput
            && document.Batch is null
            && (entries.Count == 0 || entries[^1].EndsRun))
        {
            throw new InvalidOperationException(
                $"Conversation {conversation} has no interrupted run to resume and no answered decisions to carry "
                + "out: run it with input instead.");
        }

        // D80: the batch the run finds is settled before its first model call, so no model call and no compaction
        // ever sees calls that wait. New input passes an open batch by (T5), and an answered one starts (T3), which
        // switches the conversation to the mode of an approved plan (D52)
        var batch = document.Batch;
        var started = false;

        if (batch is { State: BatchState.Open } && hasInput) batch = await ledger.ExpireAsync(conversation!, ct);

        if (batch is { State: BatchState.Answered })
        {
            document = await ledger.StartAsync(conversation!, batch, ct);
            batch = document.Batch!;
            started = true;
        }

        // D52: a conversation runs in the mode its document names, which the run's own settings, an approved plan or
        // your app set. While it names none, each run is in its agent's own mode
        var mode = document.Mode ?? legacy ?? runOptions.RunOptions.Mode;

        // What the calls of a batch that will not run read: of an expired or a withdrawn one that it did not run, and
        // of one another run started and left that nobody knows (T7). The calls that have their result already keep it
        var results = batch switch
        {
            { State: BatchState.Expired or BatchState.Cancelled } =>
                ResultsOf(batch, entries, _ => DecisionResults.Closed(batch.State)),
            { State: BatchState.Started } when !started =>
                ResultsOf(batch, entries, call => DecisionResults.VerdictOf(call).Answer ?? DecisionResults.Unknown),
            _ => null
        };

        if (batch is not null && results is not null)
            Telemetry.Settled(activity, batch, CallsOf(batch, results), repaired: batch.State is BatchState.Started);

        // D33: the background sub-agents of a run that stopped are lost; the model hears so, once
        input = [.. LostBackground(entries), .. input];

        // The results follow their calls as if the store held them already (D46)
        var history = ConversationHistory.From(
            results is null ? entries : [.. entries, new ConversationEntry(agent.Name, [results], Time.GetUtcNow())]);

        List<ChatMessage> call = [.. history, .. input];

        var agentRun = new AgentRun(agent, call, runOptions, services, mode)
        {
            InputStart = history.Count,
            Activity = activity,

            // D80: the tools approved for the conversation, and a sub-agent's caller's (D33)
            Standing = [.. document.Standing, .. runOptions.Caller?.Standing ?? []],
            LastCall = LastCall(entries)
        };

        var run = new ConversationRun(conversation, agentRun, runOptions.ModelId ?? "", Store, ledger, Time, Logger);

        runOptions.ConversationId = null;
        runOptions.AgentRun = agentRun;
        runOptions.ConversationRun = run;

        // D34: handoffs need a conversation to continue in
        if (conversation is not null && agent.Handoffs.Count > 0)
        {
            var handoffs = agent.Handoffs.Select(handoff => Declare(handoff(services)));
            runOptions.Tools = [.. runOptions.Tools ?? [], .. handoffs];
        }

        run.MarkStored(history);

        // D59: the ids of what the history leaves out, and of the background calls a summary left working
        run.HoldCallIds(UniqueCallIdsChatClient.Ids(entries.SelectMany(entry => entry.Messages)));
        run.HoldCallIds(entries.FirstOrDefault()?.BackgroundCalls?.Keys ?? []);

        // D80: decisions still open and nothing to continue with: no model is called, and the run's end is stored if
        // a crash lost it, so the conversation reads as waiting
        if (batch is { State: BatchState.Open })
        {
            if (entries is [.., { EndsRun: false }]) await run.EndAsync(null);
            Runs.Release(claim);

            return (run, call, runOptions, batch);
        }

        // The batch closes once the conversation holds a result for each call: now, for one that will not run, or
        // with the last result of the one this run carries out (T4)
        if (started) run.Carry(batch!);
        else if (batch is not null && (results is null || await run.AppendAsync([results]) is null))
            await run.CloseAsync(batch);

        if (input.Count > 0) await run.AppendAsync(input);

        // Last, so nothing that fails before it leaves the conversation claimed by a run that never ends
        Runs.Begin(run, claim);

        return (run, call, runOptions, null);
    }

    private static bool IsApprovalContent(AIContent content) => content is InputRequestContent or InputResponseContent;

    // A result for each call of the batch the conversation holds none for; null when it holds them all
    private static ChatMessage? ResultsOf(
        DecisionBatch batch,
        IReadOnlyList<ConversationEntry> entries,
        Func<BatchCall, string> result)
    {
        var stored = entries
            .SelectMany(entry => entry.Messages)
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(content => content.CallId)
            .ToHashSet();

        var missing = batch.Calls.Where(call => !stored.Contains(call.CallId)).ToList();

        return missing.Count == 0
            ? null
            : DecisionResults.Message(missing.Select(call => (call.CallId, result(call))));
    }

    // The calls of a batch that a message of results answers
    private static IEnumerable<BatchCall> CallsOf(DecisionBatch batch, ChatMessage results)
    {
        var answered = results.Contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        return batch.Calls.Where(call => answered.Contains(call.CallId));
    }

    // D32: the last model call the conversation holds both compaction's estimate and the provider's report of. A
    // report alone, of a run without a maximum or stored by an earlier version, says nothing about what was sent
    private static ReportedCall? LastCall(IReadOnlyList<ConversationEntry> entries) =>
        entries.Select(Reported).LastOrDefault(call => call is not null);

    private static ReportedCall? Reported(ConversationEntry entry) =>
        entry is { EstimatedInputTokens: { } estimated and > 0, Usage.InputTokenCount: { } reported }
            ? new ReportedCall(estimated, reported)
            : null;

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
