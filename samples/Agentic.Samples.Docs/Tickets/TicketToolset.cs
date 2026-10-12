using Agentic.Toolsets;
using Microsoft.Extensions.DependencyInjection;

namespace Agentic.Samples.Docs.Tickets;

// begin-snippet: toolset
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        DisplayName = "Tickets";

        // begin-snippet: settings-picks
        AddTool(SearchTickets)
            .WithName("search_tickets")
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);
        // end-snippet

        AddTool(GetTicketAsync)
            .WithName("get_ticket")
            .WithDescription("Reads one ticket by its number.");
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    /// <param name="settings">The settings the search runs with; the model never sees them.</param>
    /// <param name="services">The services of the call.</param>
    // begin-snippet: read-settings
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);
    // end-snippet

    /// <param name="number">The ticket's number.</param>
    /// <param name="services">The services of the call.</param>
    /// <param name="ct">Cancelled when the call is.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);
}
// end-snippet
