using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Connects the MCP servers agents name in <c>UseMcpServer</c>, with the MCP client of the host's choice. Registered
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
    /// Pauses a run that calls any of the server's tools until the host approves or rejects the call.
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

    // D35: the server's tools as this use offers them
    internal AITool Offer(AITool tool) =>
        tool switch
        {
            AIFunction function => AsksFirst ? new ApprovalRequiredAIFunction(Reading(function)) : Reading(function),
            _ => tool
        };
}

// D52: a tool plan mode offers, though no toolset declares it
internal sealed class ReadOnlyFunction(AIFunction inner) : DelegatingAIFunction(inner)
{
    public static bool IsReadOnly(AITool tool) =>
        tool.GetService<Generated.GeneratedToolFunction>() is { IsReadOnly: true }
        || tool.GetService<ReadOnlyFunction>() is not null;
}
