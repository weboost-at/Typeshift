using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval.Support;

// The calls the guarded tools made, so a test sees which ran and with what
public sealed class ToolCalls : List<string>;

// A tool behind approval, one that only reads, and one that does neither
public partial class GuardedToolset : Toolset
{
    public GuardedToolset()
    {
        AddTool(CloseTicket)
            .RequiresApproval();

        AddTool(ReadTicket)
            .ReadOnly();

        AddTool(Note);
    }

    /// <summary>
    /// Closes a ticket.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static string CloseTicket(int number, IServiceProvider services)
    {
        services.GetRequiredService<ToolCalls>().Add($"close_ticket {number}");
        return $"closed {number}";
    }

    /// <summary>
    /// Reads a ticket.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static string ReadTicket(int number, IServiceProvider services)
    {
        services.GetRequiredService<ToolCalls>().Add($"read_ticket {number}");
        return $"ticket {number}";
    }

    /// <summary>
    /// Notes something.
    /// </summary>
    /// <param name="text">The note.</param>
    public static string Note(string text, IServiceProvider services)
    {
        services.GetRequiredService<ToolCalls>().Add($"note {text}");
        return "noted";
    }
}

public partial class GuardedAgent : Agent
{
    public GuardedAgent()
    {
        WithChatOptions().WithModel("guarded-model");

        WithRunOptions()
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations);

        UseToolset<GuardedToolset>();
    }
}
