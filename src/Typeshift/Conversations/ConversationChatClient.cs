using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D46, D50: outermost, around the tool loop. It begins every run (its AgentRun, mode and invoke_agent span, D36),
// and for a run with a conversation resumes it, settles the decisions it finds before the first model call (D80),
// marks where the run begins and ends, and publishes the updates of streaming runs (D47). A run goes on while
// steering input or a background sub-agent's answer waits (D33, D51), and a handoff continues it with the agent
// handed to (D34). A run that waits for your app returns its decisions after it stored its end. The provider never
// receives the conversation id; the response names it (D73). A call runs with the options the client was created
// from, under what the call's own options may change (D26)
internal sealed class ConversationChatClient(
    IChatClient inner,
    Agent agent,
    IServiceProvider services,
    ResolvedChatOptions created)
    : DelegatingChatClient(inner)
{
    public const string HandoffPrefix = "handoff_to_";

    // D34: two agents that each send the user to the other would hand off for as long as the caller waits, each
    // handoff a run with a limit of its own
    private const int MaxHandoffs = 10;

    // How a run ends that no exception ended: its caller left its stream early
    private const string Stopped = "The run stopped before it answered.";

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = WithNewConversation(messages, created.ForCall(options));
        using var activity = Telemetry.StartRun(agent, resolved, InnerClient);

        try
        {
            ChatResponse? response = null;
            Exception? notStored = null;

            await foreach (var part in RunAsync(messages, resolved, activity, streaming: false, cancellationToken))
            {
                if (part.NotStored is { } failure) notStored = failure;
                else response = part.JoinedTo(response);
            }

            if (Named(resolved) is { } conversation) response!.ConversationId = conversation;
            Telemetry.Answered(activity, response!.Usage);

            if (notStored is not null) NotStored(activity, resolved, response, notStored);
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
        var resolved = WithNewConversation(messages, created.ForCall(options));
        using var activity = Telemetry.StartRun(agent, resolved, InnerClient);
        var usage = new UsageDetails();
        var conversation = Named(resolved);

        // D46: what the caller got, for the exception of a run whose conversation lacks a step
        var handed = new List<ChatResponseUpdate>();

        await using var parts = RunAsync(messages, resolved, activity, streaming: true, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            // D36: an iterator resumes in its caller's context, which would leave the run's later spans outside it
            if (activity is not null) Activity.Current = activity;

            try
            {
                if (!await parts.MoveNextAsync()) break;
            }
            catch (Exception exception)
            {
                Telemetry.Failed(activity, exception);
                throw;
            }

            if (parts.Current.NotStored is { } failure)
            {
                try
                {
                    NotStored(activity, resolved, handed.ToChatResponse(), failure);
                }
                catch (Exception exception)
                {
                    Telemetry.Failed(activity, exception);
                    throw;
                }

                // A run that continues says so after its last update, in one of its own
                yield return ChatResponses.IncompleteUpdate();
                continue;
            }

            var update = parts.Current.Update!;

            foreach (var content in update.Contents.OfType<UsageContent>())
            {
                usage.Add(content.Details);
            }

            // A copy, since the tool loop still holds the update, and one that names a conversation would make it
            // leave the history to the service
            var named = conversation is null ? update : Named(update, conversation);

            handed.Add(named);
            yield return named;
        }

        Telemetry.Answered(activity, usage);
    }

    // D46: a run whose conversation lacks a step fails with what it answered, unless your app chose to have it return,
    // marked; either way its span is an error
    private void NotStored(Activity? activity, ResolvedChatOptions options, ChatResponse response, Exception failure)
    {
        if (services.GetService<TypeshiftOptions>() is not { StoreFailure: StoreFailure.ContinueRun })
            throw ConversationStoreException.For(GetConversation(options)!, response, failure);

        ChatResponses.Incomplete(response);
        Telemetry.NotStored(activity, failure);
    }

    // D73: a run of the host's own, with input and no conversation, starts one when every conversation is kept; a
    // sub-agent's run belongs to its caller's, and a run without input resumes an interrupted run (D50). On the raw
    // path the id is a string, so the response's ConversationId names the same conversation when the app passes it back
    private ResolvedChatOptions WithNewConversation(IEnumerable<ChatMessage> messages, ResolvedChatOptions options)
    {
        if (options is not { Conversation: null, ConversationId: null, Caller: null }
            || services.GetService<TypeshiftOptions>() is not { PersistAllConversations: true } typeshift
            || !messages.Any())
            return options;

        var started = (ResolvedChatOptions)options.Clone();
        started.Conversation = options.HostRun
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
    private static string? Named(ResolvedChatOptions options) => GetConversation(options)?.ToString();

    private LiveRuns Runs => services.GetRequiredService<LiveRuns>();

#pragma warning disable RS0030 // the fallback for a host that registered no TimeProvider
    private TimeProvider Time => services.GetService<TimeProvider>() ?? TimeProvider.System;
#pragma warning restore RS0030

    private StoreAccess Store => services.GetRequiredService<StoreAccess>();

    private DecisionLedger Ledger => new(Store, Time);

    private ILogger Logger => LogEvents.For(services);

    // D29: a run from the claim on its conversation to its end, written once for a run that streams and one that
    // does not. The two differ in how a pass reaches the caller (PassesAsync), and in what they hand on: the updates
    // of a stream, or whole responses, which the caller joins. A run that failed, was cancelled or whose stream was
    // left early still ends
    private async IAsyncEnumerable<RunPart> RunAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions options,
        Activity? activity,
        bool streaming,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (taken, answering) = await TakeAsync(options, ct);

        if (answering is not null)
        {
            try
            {
                await foreach (var part in RunOn(answering, messages, taken, streaming, ct))
                {
                    yield return part;
                }
            }
            finally
            {
                Runs.Release(taken.Claim);
            }

            yield break;
        }

        var (run, call, runOptions, waiting, answered) = await BeginAsync(messages, options, taken, activity, ct);

        if (waiting is not null)
        {
            yield return streaming
                ? new(AskingUpdate(waiting, []))
                : new(Response: new ChatResponse(Asking(waiting, messageId: null)));

            if (run.NotStored is { } lost) yield return new(NotStored: lost);
            yield break;
        }

        if (answered is not null)
        {
            if (!streaming) yield return new(Response: new ChatResponse([.. answered]));

            foreach (var message in streaming ? answered : [])
            {
                yield return new(AsUpdate(message));
            }

            if (run.NotStored is { } missing) yield return new(NotStored: missing);
            yield break;
        }

        string? failure = null;

        try
        {
            run.PublishTo(CreatePublisher(run), streaming);

            await using var parts = PassesAsync(run, call, runOptions, streaming, ct).GetAsyncEnumerator(ct);

            while (true)
            {
                // An iterator cannot catch around what it yields, so the parts are read by hand
                try
                {
                    if (!await parts.MoveNextAsync()) break;
                }
                catch (Exception exception)
                {
                    failure = exception.Message;
                    throw;
                }

                yield return parts.Current;
            }

            // D46: once the run stored what it could, its caller learns that a step is missing
            if (run.NotStored is { } lost) yield return new(NotStored: lost);
        }
        finally
        {
            try
            {
                run.Publisher?.Flush();

                // D46: nothing for a run that stored its end or handed the conversation on. A run that failed or
                // was cancelled ends with the exception's message, streaming or not, and a stream left early with a
                // text of its own
                await run.EndAsync(failure ?? Stopped);
            }
            finally
            {
                // Whatever became of the end: a run that waits for this one to end would wait for ever
                Runs.End(run);
            }
        }
    }

    // The passes of a run, each one tool loop, and what follows each: its results are stored, a handoff is carried
    // out (D34), and the run goes on while steering input or a background sub-agent's answer waits (D33, D51). The
    // pass itself is the one step a streaming run takes another way
    private async IAsyncEnumerable<RunPart> PassesAsync(
        ConversationRun run,
        List<ChatMessage> call,
        ResolvedChatOptions options,
        bool streaming,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var calls = options.AgentRun!.ModelCalls;
        var updates = new List<ChatResponseUpdate>();
        IList<ChatMessage> last;

        while (true)
        {
            if (streaming)
            {
                updates.Clear();

                await foreach (var update in base.GetStreamingResponseAsync(call, options, ct))
                {
                    updates.Add(update);
                    run.Publisher?.Add(update);
                    yield return new(update);
                }

                // Rebuilt from the updates, the messages hold new text the checkpoints stored already
                last = updates.ToChatResponse().Messages;
                run.MarkStored(last);
            }
            else
            {
                var response = await base.GetResponseAsync(call, options, ct);

                last = response.Messages;
                yield return new(Response: response);
            }

            // D47: the pass's text reaches watchers whole before the run waits, goes on or hands off
            run.Publisher?.Flush();
            await run.StoreResultsAsync(last);

            var handoff = HandoffIn(last) is { } handing
                ? await HandOffAsync(run, options, last, handing)
                : null;

            if (handoff is { Target: { } target, Handed: { } handed })
            {
                try
                {
                    // Published as the run stored it, so a watcher pairs the handoff's call with its result
                    yield return Made(run, handoff.Result, streaming);

                    if (Spent(run, options, streaming) is { } spent) yield return spent;

                    await foreach (var part in RunOn(target, [], handed, streaming, ct))
                    {
                        yield return part;
                    }
                }
                finally
                {
                    Runs.Release(handed.Claim);
                }

                yield break;
            }

            // D34: a handoff the run had none left for is answered, and its own agent goes on at once
            if (handoff is not null) yield return Made(run, handoff.Result, streaming);

            if (!GoesFurther(options, ref calls) || (handoff is null && !await run.GoesOnAsync(ct))) break;

            call = [.. call, .. last];
            if (handoff is not null) call.Add(handoff.Result);

            // D51, D80: steering input passed the batch the run asked by
            if (run.Asked is { } passed)
            {
                if (await ExpireAsync(run, passed) is not { } results) break;

                call.Add(results);
                yield return Made(run, results, streaming);
            }
        }

        await run.EndAsync(null);

        if (Spent(run, options, streaming) is { } beside) yield return beside;

        // D80: the decisions come last, once the run stored its end, so each one your app sees can be answered and
        // a reader that stops there leaves nothing half done
        if (run.Asked is not { } asked) yield break;

        if (streaming)
        {
            // It carries the ids of the response that made the calls
            var asking = AskingUpdate(asked, updates);

            run.Publisher?.Add(asking);
            yield return new(asking);
        }
        else
        {
            var asking = Asking(asked, last.LastOrDefault(IsCalling)?.MessageId);

            run.Publisher?.Add(AsUpdate(asking));
            yield return new(Asking: asking);
        }
    }

    // Another agent's run as a part of this one: the agent the conversation was handed to, by this run or an
    // earlier one
    private static async IAsyncEnumerable<RunPart> RunOn(
        IAgentHost host,
        IEnumerable<ChatMessage> messages,
        RunHandover handover,
        bool streaming,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!streaming)
        {
            yield return new(Response: await host.RunAsync(messages, handover, ct));
            yield break;
        }

        await foreach (var update in host.RunStreamingAsync(messages, handover, ct))
        {
            yield return new(update);
        }
    }

    // D33, D32: what the run spent beside its model calls, on its sub-agents and compaction's summaries, joins what it
    // reports once it is all spent. A run that does not stream adds it to its response; a streaming run sends it in
    // an update of its own, ahead of its decisions, which the response its updates make adds up as it adds the
    // model's. Null when the run spent nothing beside
    private static RunPart? Spent(ConversationRun run, ResolvedChatOptions options, bool streaming)
    {
        if (options.AgentRun!.SpentBeside() is not { } spent) return null;
        if (!streaming) return new(Usage: spent);

        var update = new ChatResponseUpdate { Contents = [new UsageContent(spent)] };

        run.Publisher?.Add(update);
        return new(update);
    }

    // A message the run made itself, as its caller gets it. A streaming run publishes it here, with its update; a
    // run that does not stream published it when it stored it (D47)
    private static RunPart Made(ConversationRun run, ChatMessage message, bool streaming)
    {
        if (!streaming) return new(Response: new ChatResponse(message));

        var update = AsUpdate(message);

        run.Publisher?.Add(update);
        return new(update);
    }

    // What a run hands its caller next. A streaming run hands on updates. A run that does not stream hands on whole
    // responses, each pass's and that of an agent it handed off to, which its caller merges into one; what it spent
    // beside its model calls and the decisions it asks join that response as they are, the decisions in a message
    // of their own. Last, a run whose conversation lacks a step hands on the store's exception (D46)
    private readonly record struct RunPart(
        ChatResponseUpdate? Update = null,
        ChatResponse? Response = null,
        ChatMessage? Asking = null,
        Exception? NotStored = null,
        UsageDetails? Usage = null)
    {
        public ChatResponse JoinedTo(ChatResponse? response)
        {
            // A sum of its own, since the stored entry of the run's model call may hold the usage it reported
            if (Usage is not null)
            {
                var usage = new UsageDetails();

                if (response!.Usage is { } own) usage.Add(own);
                usage.Add(Usage);

                response.Usage = usage;
                return response;
            }

            if (Asking is null) return response is null ? Response! : ChatResponses.Merge(response, Response!);

            response!.Messages.Add(Asking);
            return response;
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

    // D47: only a conversation has watchers, and only live updates feed them
    private LivePublisher? CreatePublisher(ConversationRun run) =>
        run.Conversation is not null && services.GetService<LiveUpdateOptions>() is { } realtime
            ? new LivePublisher(Runs, run.Conversation, realtime, Time)
            : null;

    // D72: the host's conversation, or the raw path's ChatOptions.ConversationId, a string id
    public static StoreId? GetConversation(ResolvedChatOptions options) =>
        options switch
        {
            { Conversation: { } conversation } => conversation,
            { ConversationId: { } text } => new StoreId<string>(text),
            _ => null
        };

    // D30, D32: a pass that called no model (run middleware answered) or the run's limit spent ends the run; the
    // input still waiting is stored with its end
    private static bool GoesFurther(ResolvedChatOptions options, ref int calls)
    {
        var made = options.AgentRun!.ModelCalls;
        var further = made > calls && made <= options.RunOptions.MaxToolIterations;

        calls = made;
        return further;
    }

    // D34, D50: the conversation the run takes. A host's run took it before these options resolved, and they hold
    // the claim and the entries; a call on the raw path takes it here, and passes it on when another agent answers,
    // with the settings id and the format of a typed run (D64) and none of its own settings
    private Task<(RunHandover Taken, IAgentHost? Answering)> TakeAsync(
        ResolvedChatOptions options,
        CancellationToken ct)
    {
        var handover = new RunHandover(
            options.SettingsId,
            GetConversation(options),
            options.Claim,
            options.ResultFormat,
            options.Handoffs,
            options.Entries);

        return new RunStart(agent, services, Ledger).TakeAsync(
            handover,
            options.RunSetsMode ? options.RunOptions.Mode : null,
            ct);
    }

    // The last handoff the response calls. A call the tool loop answered, as it answers a tool it does not know, is
    // none
    private FunctionCallContent? HandoffIn(IList<ChatMessage> messages) =>
        agent.Handoffs.Count == 0
            ? null
            : Unanswered(messages)
                .LastOrDefault(call => call.Name.StartsWith(HandoffPrefix, StringComparison.Ordinal));

    // The calls without a result. The tool loop hands a whole response back uninvoked when one of its calls names a
    // declaration, as a handoff is
    private static List<FunctionCallContent> Unanswered(IList<ChatMessage> messages)
    {
        var contents = messages.Contents().ToList();
        var answered = contents.ResultIds().ToHashSet();

        return [.. contents.OfType<FunctionCallContent>().Where(call => !answered.Contains(call.CallId))];
    }

    // D34: the one place a handoff is carried out. The call's result is stored, and the run is the other agent's
    // from here: it resumes the conversation, so this agent neither ends the run nor holds it, and hands its claim
    // on with what else the other run takes over; input steered to it is stored for the other agent. The calls made
    // beside the handoff did not run, and each reads so in the same tool message. A run that has handed off as often
    // as it may does not: the call reads so, and this agent goes on. Null for a call of no agent this one hands
    // off to
    private async Task<Handoff?> HandOffAsync(
        ConversationRun run,
        ResolvedChatOptions options,
        IList<ChatMessage> messages,
        FunctionCallContent call)
    {
        var target = agent.Handoffs.Select(handoff => handoff(services))
            .FirstOrDefault(host => $"{HandoffPrefix}{host.Agent.Name}" == call.Name);

        if (target is null) return null;

        var spent = options.Handoffs >= MaxHandoffs;

        var beside = spent
            ? "This call did not run: it was made beside a handoff. Call it again if it is still needed."
            : $"This call did not run: the conversation was handed off to {target.Agent.Name} in the same step. "
                + "Call it again if it is still needed.";

        var result = new ChatMessage(
            ChatRole.Tool,
            [
                // A call the loop would not have run, as one a provider ran itself, is not its to answer
                .. Unanswered(messages)
                    .Where(other => other != call && !other.InformationalOnly)
                    .Select(other => new FunctionResultContent(other.CallId, beside)),
                new FunctionResultContent(
                    call.CallId,
                    spent
                        ? "No more handoffs in this run: answer the user yourself."
                        : $"Handed off to {target.Agent.Name}.")
            ]);

        if (spent)
        {
            await run.AppendAsync([result]);
            return new Handoff(result);
        }

        await run.HandOffAsync(result, target.Agent.Name);
        LogEvents.HandedOff(Logger, agent.Name, run.Conversation, target.Agent.Name);

        var handed = new RunHandover(
            options.SettingsId,
            run.Conversation,
            Runs.HandOff(run),
            options.ResultFormat,
            options.Handoffs + 1);

        return new Handoff(result, target, handed);
    }

    // D34: a handoff call as it was answered: the tool message for it and for the calls made beside it, and the
    // agent that takes over with what its run is handed. Without an agent the run had no handoff left
    private sealed record Handoff(ChatMessage Result, IAgentHost? Target = null, RunHandover? Handed = null);

    // The run that began, or, for a run without input, what it returns without one: the batch that waits while its
    // decisions are still open, or the answer the conversation ends with when only the end was not stored
    private async Task<(
        ConversationRun Run,
        List<ChatMessage> Call,
        ResolvedChatOptions Options,
        DecisionBatch? Waiting,
        IReadOnlyList<ChatMessage>? Answered)> BeginAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions options,
        RunHandover taken,
        Activity? activity,
        CancellationToken ct)
    {
        try
        {
            return await BeginClaimedAsync(
                messages,
                options,
                taken.Conversation,
                taken.Entries ?? [],
                taken.Claim,
                activity,
                ct);
        }
        catch
        {
            Runs.Release(taken.Claim);
            throw;
        }
    }

    private async Task<(
        ConversationRun Run,
        List<ChatMessage> Call,
        ResolvedChatOptions Options,
        DecisionBatch? Waiting,
        IReadOnlyList<ChatMessage>? Answered)> BeginClaimedAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions options,
        StoreId? conversation,
        IReadOnlyList<ConversationEntry> entries,
        RunClaim? claim,
        Activity? activity,
        CancellationToken ct)
    {
        var input = messages.ToList();

        // D80: a decision is answered by its id, in the document; approval contents in a run's input answer nothing
        if (input.Contents().Any(IsApprovalContent))
        {
            throw new ArgumentException(
                "The input carries an approval request or answer of Microsoft.Extensions.AI, which a run does not "
                + "take: answer the decisions of a response by their ids, with AnswerAsync or RecordAnswersAsync.",
                nameof(messages));
        }

        var runOptions = (ResolvedChatOptions)options.Clone();

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
            throw NothingToResumeException.For(conversation);
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

        var run = new ConversationRun(
            conversation,
            agentRun,
            runOptions.ModelId ?? "",
            Store,
            ledger,
            Time,
            Logger,
            conversation is null ? 0 : await LastSequenceAsync(conversation, entries, ct));

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
        run.HoldCallIds(UniqueCallIdsChatClient.Ids(entries));
        run.HoldCallIds(entries.FirstOrDefault()?.BackgroundCalls?.Keys ?? []);

        // D80: decisions still open and nothing to continue with: no model is called, and the run's end is stored if
        // a crash lost it, so the conversation reads as waiting
        if (batch is { State: BatchState.Open })
        {
            if (entries is [.., { EndsRun: false }]) await run.EndAsync(null);
            Runs.Release(claim);

            return (run, call, runOptions, batch, null);
        }

        // D50: the model answered and the run's end was not stored, after a crash between the two or a failed append:
        // nothing is left to do but store the end, and the answer is what the run returns
        if (!hasInput && batch is null && call.Count == history.Count && AnswerOf(entries) is { } answer)
        {
            await run.EndAsync(null);
            Runs.Release(claim);

            return (run, call, runOptions, null, answer);
        }

        // The batch closes once the conversation holds a result for each call: now, for one that will not run, or
        // with the last result of the one this run carries out (T4)
        if (started) run.Carry(batch!);
        else if (batch is not null && (results is null || await run.AppendAsync([results]) is null))
            await run.CloseAsync(batch);

        if (input.Count > 0) await run.AppendAsync(input);

        // Last, so nothing that fails before it leaves the conversation claimed by a run that never ends
        Runs.Begin(run, claim);

        return (run, call, runOptions, null, null);
    }

    // D50: the run numbers its entries on from the highest number it loaded. A summary an earlier version wrote, with
    // no number after it, may follow entries this version numbered, which the run did not load: their numbers come
    // from the whole conversation, once, since the next run finds this one's numbers after the summary
    private async Task<long> LastSequenceAsync(
        StoreId conversation,
        IReadOnlyList<ConversationEntry> entries,
        CancellationToken ct) =>
        ConversationHistory.LastSequence(
            entries is [{ IsSummary: true, Sequence: 0 }, ..] && entries.All(entry => entry.Sequence == 0)
                ? await Store.LoadConversationAsync(conversation, ct)
                : entries);

    // The model's answer the conversation's last entry holds, when nothing follows it: no call, which would be in
    // flight, no error, and no input or summary after it
    private static IReadOnlyList<ChatMessage>? AnswerOf(IReadOnlyList<ConversationEntry> entries) =>
        entries is [.., { EndsRun: false, IsSummary: false, Messages: [_, ..] messages }]
        && messages.All(message => message.Role == ChatRole.Assistant)
        && !messages.Contents().Any(IsStep)
            ? messages
            : null;

    private static bool IsStep(AIContent content) => content is FunctionCallContent or ErrorContent;

    private static bool IsApprovalContent(AIContent content) => content is InputRequestContent or InputResponseContent;

    // A result for each call of the batch the conversation holds none for; null when it holds them all
    private static ChatMessage? ResultsOf(
        DecisionBatch batch,
        IReadOnlyList<ConversationEntry> entries,
        Func<BatchCall, string> result)
    {
        var stored = entries.SelectMany(entry => entry.Messages).Contents().ResultIds().ToHashSet();

        var missing = batch.Calls.Where(call => !stored.Contains(call.CallId)).ToList();

        return missing.Count == 0
            ? null
            : DecisionResults.Message(missing.Select(call => (call.CallId, result(call))));
    }

    // The calls of a batch that a message of results answers
    private static IEnumerable<BatchCall> CallsOf(DecisionBatch batch, ChatMessage results)
    {
        var answered = results.Contents.ResultIds().ToHashSet();

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
        var contents = messages.Contents().ToList();
        var started = contents.ResultIds().ToHashSet();

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
            yield return ConversationRun.Lost(call.CallId, call.Agent, "the run stopped");
        }
    }
}
