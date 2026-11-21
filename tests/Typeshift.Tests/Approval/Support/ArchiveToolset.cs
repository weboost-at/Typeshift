using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Approval.Support;

// How many tickets one archive call may reach
public record ArchiveSettings
{
    public int Limit { get; init; } = 1;
}

// A tool whose mark follows its settings call and its pick, as a chain in any order may (D17)
public partial class ArchiveToolset : Toolset
{
    public ArchiveToolset()
    {
        AddTool(ArchiveTicket)
            .WithSettings<ArchiveSettings>()
            .Configurable(s => s.Limit)
            .RequiresApproval();
    }

    /// <summary>
    /// Archives a ticket.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static string ArchiveTicket(int number, ArchiveSettings settings, IServiceProvider services)
    {
        services.GetRequiredService<ToolCalls>().Add($"archive_ticket {number}");
        return $"archived {number}";
    }
}

public partial class ArchiveAgent : Agent
{
    public ArchiveAgent()
    {
        WithChatOptions().WithProvider("scripted").WithModel("guarded-model");

        UseToolset<ArchiveToolset>();
    }
}
