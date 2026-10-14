using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Tickets;

// begin-snippet: toolset
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        WithDisplayName("Tickets");

        // begin-snippet: settings-picks
        AddTool(SearchTickets)
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);
        // end-snippet

        AddTool(GetTicketAsync)
            .WithDescription("Reads one ticket by its number.");

        // begin-snippet: approval-tool
        AddTool(CloseTicketAsync)
            .RequiresApproval();
        // end-snippet
    }

    // begin-snippet: read-settings
    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);
    // end-snippet

    /// <param name="number">The ticket's number.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);

    /// <summary>
    /// Closes a ticket for good.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    /// <param name="reason">Why it is closed, as the customer will read it.</param>
    public static Task CloseTicketAsync(int number, string reason, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().CloseAsync(number, reason, ct);
}
// end-snippet
