using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Generated;

namespace Typeshift;

// D24, D52: above the tool loop. It binds the host's answers to the requests the conversation stored, offers the
// tools of the run's mode, and answers what rules, standing approvals and the mode decide, continuing the loop, so
// only the rest reach the host
internal sealed class ApprovalChatClient(IChatClient inner, FunctionInvokingChatClient loop)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run } resolved)
            return await base.GetResponseAsync(messages, options, cancellationToken);

        var call = Prepare(messages, run, out var standing);
        var decided = new HashSet<string>();
        ChatResponse? merged = null;

        try
        {
            while (true)
            {
                var response = await base.GetResponseAsync(call, ForMode(resolved, run.Mode), cancellationToken);
                merged = merged is null ? response : ChatResponses.Merge(merged, response);

                var answers = await DecideAsync(response.Messages, run, standing, cancellationToken);
                decided.UnionWith(answers.Select(answer => answer.RequestId));

                if (!await ContinueAsync(response.Messages, answers, resolved)) return Without(merged, decided);

                call = [.. call, .. response.Messages, new ChatMessage(ChatRole.User, [.. answers])];
            }
        }
        finally
        {
            loop.MaximumIterationsPerRequest = resolved.RunOptions.MaxToolIterations;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun: { } run } resolved)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        var call = Prepare(messages, run, out var standing);

        try
        {
            while (true)
            {
                var updates = new List<ChatResponseUpdate>();

                var streamed = base.GetStreamingResponseAsync(call, ForMode(resolved, run.Mode), cancellationToken);

                await foreach (var update in streamed)
                {
                    updates.Add(update);

                    // The requests a rule may answer wait until the stream ends; the host sees only those left to it
                    if (!update.Contents.Any(IsRequest)) yield return update;
                }

                var response = updates.ToChatResponse();
                var answers = await DecideAsync(response.Messages, run, standing, cancellationToken);
                var answered = answers.Select(answer => answer.RequestId).ToHashSet();

                foreach (var update in updates.Where(update => update.Contents.Any(IsRequest)))
                {
                    var left = update.Contents.Where(content => !IsAnswered(content, answered)).ToList();

                    if (left.Count > 0)
                        yield return new ChatResponseUpdate(update.Role, left) { AuthorName = update.AuthorName };
                }

                if (!await ContinueAsync(response.Messages, answers, resolved)) yield break;

                call = [.. call, .. response.Messages, new ChatMessage(ChatRole.User, [.. answers])];
            }
        }
        finally
        {
            loop.MaximumIterationsPerRequest = resolved.RunOptions.MaxToolIterations;
        }
    }

    // The tool loop asks again with every request answered; a run with requests left ends, storing the requests and
    // the answers it gave, which the host's answers in the conversation's next run join
    private async Task<bool> ContinueAsync(
        IList<ChatMessage> response,
        IReadOnlyList<ToolApprovalResponseContent> answers,
        ResolvedChatOptions options)
    {
        var left = Requests(response)
            .Where(request => answers.All(answer => answer.RequestId != request.RequestId))
            .ToList();

        if (left.Count == 0) return answers.Count > 0 && Budgets(options);

        if (options.ConversationRun is not { } conversation)
        {
            var tools = string.Join(", ", left.Select(request => (request.ToolCall as FunctionCallContent)?.Name));

            throw new InvalidOperationException(
                $"The model called {tools}, which require approval, and a run without a conversation cannot ask "
                + "for it: run with a conversation id, approve the calls with a rule, or run in auto mode.");
        }

        await conversation.AppendAsync([new ChatMessage(ChatRole.Assistant, [.. Requests(response)])]);
        if (answers.Count > 0) await conversation.AppendAsync([new ChatMessage(ChatRole.User, [.. answers])]);

        return false;
    }

    // D32: the rounds of a run share its limit. A round goes on from the model call that asked, so it may make the
    // calls left; past the limit the run ends without the calls the round approved, as the tool loop leaves the calls
    // past its own. shortcut: a round that runs tools before asking again may run one round of tools past the limit
    private bool Budgets(ResolvedChatOptions options)
    {
        var left = options.RunOptions.MaxToolIterations - options.AgentRun!.ModelCalls;
        if (left < 0) return false;

        loop.MaximumIterationsPerRequest = Math.Max(1, left);
        return true;
    }

    // D24: an answer runs the call its request stored, whatever call the host sent; an answer to no stored request is
    // dropped, a request keeps its first answer, and a request no answer reached is left out, as the tool loop
    // rejects it
    private static List<ChatMessage> Prepare(
        IEnumerable<ChatMessage> messages,
        AgentRun run,
        out HashSet<string> standing)
    {
        var list = messages.ToList();
        var contents = list.SelectMany(message => message.Contents).ToList();
        // A store that retried an append may hold a request twice
        var requests = contents.OfType<ToolApprovalRequestContent>()
            .DistinctBy(request => request.RequestId)
            .ToDictionary(request => request.RequestId);
        var answers = contents.OfType<ToolApprovalResponseContent>()
            .Where(answer => requests.ContainsKey(answer.RequestId))
            .DistinctBy(answer => answer.RequestId)
            .ToList();
        var results = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();

        standing = [.. answers
            .Where(answer => answer.Approved && IsMarked(answer, ChatResponseExtensions.ForConversation))
            .Select(answer => requests[answer.RequestId].ToolCall)
            .OfType<FunctionCallContent>()
            .Select(call => call.Name)];

        // D52: an approved plan the tool loop has not run yet switches the run's mode
        foreach (var answer in answers.Where(answer => answer.Approved))
        {
            if (requests[answer.RequestId].ToolCall is FunctionCallContent { Name: ExitPlanModeFunction.ToolName } plan
                && !results.Contains(plan.CallId))
                run.Mode = GetPlanMode(answer);
        }

        var bound = answers.ToDictionary(
            answer => answer.RequestId,
            answer => new ToolApprovalResponseContent(
                answer.RequestId,
                answer.Approved,
                requests[answer.RequestId].ToolCall)
            {
                Reason = answer.Reason
            });

        var sent = new HashSet<string>();
        return [.. list.Select(message => Bind(message, bound, sent)).OfType<ChatMessage>()];
    }

    private static ChatMessage? Bind(
        ChatMessage message,
        Dictionary<string, ToolApprovalResponseContent> answers,
        HashSet<string> sent)
    {
        if (!message.Contents.Any(content => content is ToolApprovalRequestContent or ToolApprovalResponseContent))
            return message;

        var contents = message.Contents
            .Select(content => content switch
            {
                ToolApprovalResponseContent answer => sent.Add(answer.RequestId)
                    ? answers.GetValueOrDefault(answer.RequestId)
                    : null,
                ToolApprovalRequestContent request => answers.ContainsKey(request.RequestId) ? request : null,
                _ => content
            })
            .OfType<AIContent>()
            .ToList();

        if (contents.Count == 0) return null;

        var bound = message.Clone();
        bound.Contents = contents;
        return bound;
    }

    // Rules see every call that needs approval first; a rejection wins, then an approval, a standing approval, and
    // auto mode
    private static async Task<IReadOnlyList<ToolApprovalResponseContent>> DecideAsync(
        IList<ChatMessage> response,
        AgentRun run,
        HashSet<string> standing,
        CancellationToken ct)
    {
        var answers = new List<ToolApprovalResponseContent>();
        var rules = run.Services.GetServices<IToolApprovalRule>().ToList();

        var needing = (run.Options.Tools ?? [])
            .Where(tool => tool.GetService<ApprovalRequiredAIFunction>() is not null)
            .Select(tool => tool.Name)
            .ToHashSet();

        foreach (var request in Requests(response))
        {
            if (request.ToolCall is not FunctionCallContent call) continue;

            if (call.Name == ExitPlanModeFunction.ToolName) continue;

            // The tool loop asks for every call of a response once one of them needs approval; the others run
            if (!needing.Contains(call.Name))
            {
                answers.Add(request.CreateResponse(approved: true));
                continue;
            }

            var decisions = new List<ToolDecision>();

            foreach (var rule in rules)
            {
                decisions.Add(await rule.DecideAsync(call, run, ct));
            }

            if (decisions.FirstOrDefault(decision => decision.Rejection is not null) is { Rejection: { } reason })
                answers.Add(request.CreateResponse(approved: false, reason));
            else if (decisions.Any(decision => decision.Approved)
                     || standing.Contains(call.Name)
                     || run.Mode == AgentMode.Auto)
                answers.Add(request.CreateResponse(approved: true));
        }

        return answers;
    }

    // D52: plan mode offers the tools that only read and the tool that presents the plan
    private static ChatOptions ForMode(ResolvedChatOptions options, AgentMode mode)
    {
        if (mode != AgentMode.Plan) return options;

        var planning = (ResolvedChatOptions)options.Clone();

        planning.Tools =
        [
            .. options.Tools?.Where(tool => tool.GetService<GeneratedToolFunction>() is { IsReadOnly: true }) ?? [],
            ExitPlanModeFunction.Instance
        ];

        planning.Instructions = options.Instructions is { } instructions
            ? $"{instructions}\n\n{ExitPlanModeFunction.Instructions}"
            : ExitPlanModeFunction.Instructions;

        return planning;
    }

    private static ChatResponse Without(ChatResponse response, HashSet<string> answered)
    {
        if (answered.Count == 0) return response;

        var messages = response.Messages
            .Select(message =>
            {
                if (!message.Contents.Any(content => IsAnswered(content, answered))) return message;

                var left = message.Clone();
                left.Contents = [.. message.Contents.Where(content => !IsAnswered(content, answered))];
                return left.Contents.Count > 0 ? left : null;
            })
            .OfType<ChatMessage>()
            .ToList();

        return new ChatResponse(messages)
        {
            ModelId = response.ModelId,
            FinishReason = response.FinishReason,
            Usage = response.Usage,
            ResponseId = response.ResponseId,
            CreatedAt = response.CreatedAt,
            AdditionalProperties = response.AdditionalProperties
        };
    }

    private static bool IsRequest(AIContent content) => content is ToolApprovalRequestContent;

    private static bool IsAnswered(AIContent content, HashSet<string> answered) =>
        content is ToolApprovalRequestContent request && answered.Contains(request.RequestId);

    private static IEnumerable<ToolApprovalRequestContent> Requests(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>();

    private static AgentMode GetPlanMode(ToolApprovalResponseContent answer) =>
        answer.AdditionalProperties?.TryGetValue(ChatResponseExtensions.PlanMode, out var mode) is true
        && Enum.TryParse<AgentMode>(mode?.ToString()?.Trim('"'), out var parsed)
        && parsed != AgentMode.Plan
            ? parsed
            : AgentMode.Default;

    // A mark read back from a store is a JSON element, not the value it was written as
    private static bool IsMarked(ToolApprovalResponseContent answer, string key) =>
        answer.AdditionalProperties?.TryGetValue(key, out var value) is true
        && value is true or System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True };
}
