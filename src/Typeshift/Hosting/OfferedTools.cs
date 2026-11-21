using Microsoft.Extensions.AI;
using Typeshift.Generated;

namespace Typeshift;

// D35, D57: a run's tools come from toolsets, the agent, its sub-agents and handoffs, its chat options, MCP servers
// and context providers, and the model calls a tool by its name alone. A name offered twice would leave the model
// one of the two tools, and a name the run offers itself would stand in the built-in tool's place, so either fails
// the run. The analyzer reports what a declaration shows (TSH007, TSH025, TSH035); this is for what only a run knows
internal static class OfferedTools
{
    public static void Check(AgentRun run, IEnumerable<AITool> tools)
    {
        var offered = new Dictionary<string, AIFunctionDeclaration>();

        // D34: the run's own handoff tools, each named after an agent this one hands off to
        var handoffs = new Lazy<HashSet<string>>(() =>
        [
            .. run.Agent.Handoffs
                .Select(handoff => handoff(run.Services).Agent.Name)
                .Select(name => ConversationChatClient.HandoffPrefix + name)
        ]);

        // Only a function is called by its name; a hosted tool's name is that of its kind
        foreach (var tool in tools.OfType<AIFunctionDeclaration>())
        {
            if (ReservedFor(tool, handoffs) is { } builtIn)
            {
                throw new InvalidOperationException(
                    $"The agent '{run.Agent.Name}' offers a tool '{tool.Name}' through {SourceOf(tool, handoffs)}, "
                    + $"and the name is reserved for {builtIn}: rename the tool, or leave it out.");
            }

            if (offered.TryAdd(tool.Name, tool)) continue;

            throw new InvalidOperationException(
                $"The agent '{run.Agent.Name}' offers the tool '{tool.Name}' twice, through "
                + $"{SourceOf(offered[tool.Name], handoffs)} and through {SourceOf(tool, handoffs)}: rename one of "
                + "them, or leave one out.");
        }
    }

    // The built-in tool a name belongs to, which the run adds itself where it applies
    private static string? ReservedFor(AIFunctionDeclaration tool, Lazy<HashSet<string>> handoffs) =>
        tool.Name switch
        {
            AskUserFunction.ToolName => "the tool that asks the user",
            ExitPlanModeFunction.ToolName => "the tool that presents a plan",
            var name when name.StartsWith(ConversationChatClient.HandoffPrefix, StringComparison.Ordinal)
                && !IsHandoff(tool, handoffs) => "the handoffs of UseHandoff<T>()",
            _ => null
        };

    // A handoff is a declaration the loop cannot invoke; a function of that name is none
    private static bool IsHandoff(AIFunctionDeclaration tool, Lazy<HashSet<string>> handoffs) =>
        tool is not AIFunction && handoffs.Value.Contains(tool.Name);

    private static string SourceOf(AIFunctionDeclaration tool, Lazy<HashSet<string>> handoffs) =>
        tool switch
        {
            SubAgentFunction => "its sub-agents",
            _ when tool.GetService<McpServerFunction>() is { Server: var server } => $"the MCP server '{server}'",
            _ when tool.GetService<GeneratedToolFunction>() is not null => "its toolsets or its own tools",
            _ when IsHandoff(tool, handoffs) => "its handoffs",
            _ => "its chat options or a context provider"
        };
}
