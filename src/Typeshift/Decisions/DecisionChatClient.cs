using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D81: the gate, below the tool loop. It reads each model response before the loop does and gives every call that
// needs one its verdict: a rule's, a standing approval's, the mode's. When your app must decide, it opens a batch
// (D80) and marks every call of the response as not to be invoked, so the loop ends on its own and never pauses.
// Once the batch is answered, it hands the loop the batch's calls in place of a model response, and the loop carries
// them out as it carries out any response
internal sealed class DecisionChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolvedChatOptions.OfRun(options);

        if (resolved.ConversationRun!.TakeReplay() is { } replay) return replay;

        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var calls = response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().ToList();

        if (await DecideAsync(calls, resolved, cancellationToken) is { } answered) response.Messages.Add(answered);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resolved = ResolvedChatOptions.OfRun(options);

        if (resolved.ConversationRun!.TakeReplay() is { } replay)
        {
            foreach (var update in replay.ToChatResponseUpdates())
            {
                yield return update;
            }

            yield break;
        }

        // The tool loop reads the calls of each update as it arrives, so the updates from the first call on wait
        // until the stream ended and the gate decided, as the loop itself holds them back
        var held = new List<ChatResponseUpdate>();

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (held.Count == 0 && !update.Contents.Any(content => content is FunctionCallContent))
            {
                yield return update;
                continue;
            }

            held.Add(update);
        }

        var calls = held.SelectMany(update => update.Contents).OfType<FunctionCallContent>().ToList();
        var answered = await DecideAsync(calls, resolved, cancellationToken);

        foreach (var update in held)
        {
            yield return update;
        }

        if (answered is not null) yield return new ChatResponseUpdate(answered.Role, answered.Contents);
    }

    // Every call of a response gets its verdict before the loop sees it. When one of them waits for your app, the
    // response becomes a batch and none of its calls runs; the calls no replay could carry out are answered at once
    private static async Task<ChatMessage?> DecideAsync(
        List<FunctionCallContent> calls,
        ResolvedChatOptions options,
        CancellationToken ct)
    {
        var run = options.ConversationRun!;
        var decided = new List<(FunctionCallContent Call, AITool? Tool, Ask? Ask, string? Rejected)>();

        // A call a provider ran itself is marked already, and is none of the loop's
        foreach (var call in calls.Where(call => !call.InformationalOnly))
        {
            var tool = options.Tools?.FirstOrDefault(tool => tool.Name == call.Name);
            var (ask, rejected) = await DecideAsync(call, tool, options, ct);

            if (rejected is not null) run.Decide(call.CallId, CallVerdict.AnswerWith(rejected));
            else if (ask is null && tool?.GetService<GatedFunction>() is not null) run.Decide(call.CallId, default);

            decided.Add((call, tool, ask, rejected));
        }

        if (decided.All(call => call.Ask is null)) return null;

        if (run.Conversation is null)
        {
            throw DecisionUnavailableException.For(
                [.. decided.Where(call => call.Ask is not null).Select(call => call.Call.Name).Distinct()]);
        }

        // A batch never points at calls the history lacks: the response's own append must have succeeded
        if (run.ResponseFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();

        // A declaration the loop cannot invoke, a handoff for example, would make it hand a replay back untouched
        var unreplayable = decided.Where(call => call.Tool is AIFunctionDeclaration and not AIFunction).ToList();

        var batch = new DecisionBatch(
            run.Agent,
            run.Time.GetUtcNow(),
            BatchState.Open,
            [
                .. decided.Except(unreplayable).Select(call =>
                    new BatchCall(call.Call.CallId, call.Call.Name, ArgumentsOf(call.Call))
                    {
                        Server = call.Tool?.GetService<GatedFunction>()?.Server,
                        Ask = call.Ask,
                        Rejected = call.Rejected
                    })
            ]);

        await run.OpenAsync(batch);

        foreach (var (call, _, _, _) in decided)
        {
            call.InformationalOnly = true;
        }

        return unreplayable.Count == 0
            ? null
            : DecisionResults.Message(unreplayable.Select(call => (call.Call.CallId, NotRun(call.Call))));
    }

    // Rules see every call of a tool that requires approval first; a rejection wins, then an approval, a standing
    // approval and auto mode. What is left waits for your app, or is rejected in a sub-agent's run (D33). The plan
    // tool asks only a run that is planning (D52)
    private static async Task<(Ask? Ask, string? Rejected)> DecideAsync(
        FunctionCallContent call,
        AITool? tool,
        ResolvedChatOptions options,
        CancellationToken ct)
    {
        var run = options.AgentRun!;
        var time = options.ConversationRun!.Time;

        if (call.Name == ExitPlanModeFunction.ToolName && tool is null or ExitPlanModeFunction)
        {
            return run.Mode == AgentMode.Plan && options.Caller is null
                ? (Ask.Create(AskKind.Plan, time), null)
                : (null, DecisionResults.NotPlanning);
        }

        // D82: questions are asked in every mode, of a host's run with a conversation, by an agent that offers them
        if (call.Name == AskUserFunction.ToolName && tool is null or AskUserFunction)
        {
            return tool is null ? (null, AskUserFunction.NotAvailable)
                : AskUserFunction.Check(ArgumentsOf(call)) is { } wrong ? (null, wrong)
                : (Ask.Create(AskKind.Question, time), null);
        }

        if (tool?.GetService<GatedFunction>() is not { } gated) return (null, null);

        var verdicts = new List<RuleVerdict>();
        var logger = LogEvents.For(run.Services);

        foreach (var rule in run.Services.GetServices<IToolApprovalRule>())
        {
            var verdict = await rule.DecideAsync(call, run, ct);
            verdicts.Add(verdict);

            // A rule that abstains leaves no trace; one that decides is why a call ran unasked or did not run
            if (verdict.Rejected || verdict.Approved)
            {
                LogEvents.RuleDecided(
                    logger,
                    rule.GetType().Name,
                    verdict.Rejected ? "rejected" : "approved",
                    call.CallId,
                    call.Name);
            }
        }

        if (verdicts.FirstOrDefault(verdict => verdict.Rejected) is { Rejected: true, Rejection: var reason })
            return (null, reason);

        if (verdicts.Any(verdict => verdict.Approved)
            || run.Standing.Any(approval => approval.Tool == call.Name && approval.Server == gated.Server)
            || run.Mode == AgentMode.Auto)
            return (null, null);

        return options.Caller is null
            ? (Ask.Create(AskKind.Approval, time), null)
            : (null, DecisionResults.SubAgent);
    }

    // Microsoft.Extensions.AI's contract for an argument a client did not deliver as JSON
    private static readonly System.Text.Json.Serialization.Metadata.JsonTypeInfo Values =
        AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object));

    private static string NotRun(FunctionCallContent call) =>
        call.Name.StartsWith(ConversationChatClient.HandoffPrefix, StringComparison.Ordinal)
            ? DecisionResults.NotHandedOff
            : DecisionResults.NotRun;

    // The arguments as the model sent them, which a provider's client delivers as JSON values
    private static JsonElement ArgumentsOf(FunctionCallContent call)
    {
        if (call.Arguments is not { Count: > 0 } arguments) return BatchCall.NoArguments;

        return JsonElement.Parse(Generated.StorableJson.Write(writer =>
        {
            writer.WriteStartObject();

            foreach (var (name, value) in arguments)
            {
                writer.WritePropertyName(name);

                if (value is JsonElement element) element.WriteTo(writer);
                else JsonSerializer.Serialize(writer, value, Values);
            }

            writer.WriteEndObject();
        }));
    }
}
