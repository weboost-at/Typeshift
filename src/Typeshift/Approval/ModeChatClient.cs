using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D52, D32: above the tool loop, around each pass of a run. It offers the tools of the run's mode, hands the loop the
// tools that require approval without the mark the loop would pause on (D81), and gives the pass what is left of the
// run's limit of model calls
internal sealed class ModeChatClient(IChatClient inner, Func<int, IChatClient> loop) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = (ResolvedChatOptions)options!;

        return loop(Budget(resolved)).GetResponseAsync(messages, ForMode(resolved), cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resolved = (ResolvedChatOptions)options!;
        var pass = loop(Budget(resolved)).GetStreamingResponseAsync(messages, ForMode(resolved), cancellationToken);

        await foreach (var update in pass)
        {
            yield return update;
        }
    }

    // D32: each pass of a run, as steering input or a background answer starts one, gets what is left of the run's
    // limit. Carrying out an answered batch takes one turn of the loop that calls no model (D81)
    private static int Budget(ResolvedChatOptions options)
    {
        var left = options.RunOptions.MaxToolIterations - options.AgentRun!.ModelCalls;

        return Math.Max(1, left) + (options.ConversationRun!.HasReplay ? 1 : 0);
    }

    // D52: plan mode offers the tools that only read, a toolset's or an MCP server's, the sub-agents, which plan too,
    // and the tool that presents the plan; a sub-agent presents none, it answers its caller
    private static ChatOptions ForMode(ResolvedChatOptions options)
    {
        var offered = (ResolvedChatOptions)options.Clone();
        var tools = options.Tools ?? [];

        // D82: an agent that declares questions asks them in every mode, where a decision can wait: in a host's
        // run with a conversation, never as a sub-agent
        var asks = options is { Caller: null, AgentRun.Agent.AsksUser: true, ConversationRun.Conversation: not null };
        AITool[] questions = asks ? [AskUserFunction.Instance] : [];

        if (options.AgentRun!.Mode != AgentMode.Plan)
        {
            offered.Tools = [.. tools.Select(GatedFunction.Wrap), .. questions];
            return offered;
        }

        var planning = tools.Where(IsPlanningTool).Select(GatedFunction.Wrap);

        if (options.Caller is not null)
        {
            offered.Tools = [.. planning];
            return offered;
        }

        offered.Tools = [.. planning, .. questions, ExitPlanModeFunction.Instance];

        offered.Instructions = options.Instructions is { } instructions
            ? $"{instructions}\n\n{ExitPlanModeFunction.Instructions}"
            : ExitPlanModeFunction.Instructions;

        return offered;
    }

    private static bool IsPlanningTool(AITool tool) => ReadOnlyFunction.IsReadOnly(tool) || tool is SubAgentFunction;
}
