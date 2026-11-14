using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Tickets;

// begin-snippet: tool-exception
internal partial class TicketEscalationToolset : Toolset
{
    public TicketEscalationToolset() => AddTool(EscalateTicketAsync);

    /// <summary>
    /// Hands an open ticket to a person of the support team.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static async Task<string> EscalateTicketAsync(int number, IServiceProvider services, CancellationToken ct)
    {
        var ticket = await services.GetRequiredService<ITicketClient>().GetAsync(number, ct)
            ?? throw new ToolException($"No ticket {number} exists. Search for it first.");

        if (ticket.Closed)
            throw new ToolException($"Ticket {number} is closed. Tell the customer to open a new one.");

        return $"Ticket {number} is with the support team now.";
    }
}
// end-snippet
