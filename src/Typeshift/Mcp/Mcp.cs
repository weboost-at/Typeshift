using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Connects the MCP servers agents name in <c>UseMcpServer</c>, with the MCP client your app chooses. Registered
/// with <c>WithMcpToolProvider&lt;T&gt;()</c>, resolved from the run's services, so it may connect each customer with
/// their own credentials.
/// </summary>
public interface IMcpToolProvider
{
    /// <summary>
    /// Returns the tools of a server. Their descriptions and results are untrusted input.
    /// </summary>
    /// <param name="server">The server's name, as the agent names it.</param>
    /// <param name="ct">Cancels the connection.</param>
    /// <returns>The server's tools.</returns>
    ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct);
}

/// <summary>
/// An agent's use of an MCP server.
/// </summary>
public sealed class McpServerUse
{
    internal McpServerUse(string server) => Server = server;

    internal string Server { get; }

    internal bool AsksFirst { get; private set; }

    internal bool OnlyReads { get; private set; }

    /// <summary>
    /// Pauses a run that calls any of the server's tools until your app approves or rejects the call.
    /// </summary>
    /// <returns>The same use.</returns>
    public McpServerUse RequiresApproval()
    {
        AsksFirst = true;
        return this;
    }

    /// <summary>
    /// Marks every tool of the server as reading only, so plan mode offers them.
    /// </summary>
    /// <returns>The same use.</returns>
    public McpServerUse ReadOnly()
    {
        OnlyReads = true;
        return this;
    }

    private AIFunction Reading(AIFunction function) => OnlyReads ? new ReadOnlyFunction(function) : function;

    // D35: the server's tools as this use offers them. One that requires approval knows its server, which its
    // decision and its standing approval name (D80)
    internal AITool Offer(AITool tool) =>
        tool switch
        {
            AIFunction function when AsksFirst =>
                new ApprovalRequiredAIFunction(new McpServerFunction(Reading(function), Server)),
            AIFunction function => Reading(function),
            _ => tool
        };
}

internal sealed class McpServerFunction(AIFunction inner, string server) : DelegatingAIFunction(inner)
{
    public string Server => server;
}

// D52: a tool plan mode offers, though no toolset declares it
internal sealed class ReadOnlyFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    public static bool IsReadOnly(AITool tool) =>
        tool.GetService<Generated.GeneratedToolFunction>() is { IsReadOnly: true }
        || tool.GetService<ReadOnlyFunction>() is not null;
}
