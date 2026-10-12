using Agentic.Tools;
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
            .WithSettings<SearchTicketsSettings>(settings => settings
                .Configurable(s => s.Limit)
                .Configurable(s => s.IncludeClosed));
        // end-snippet

        AddTool(GetTicketAsync)
            .WithName("get_ticket")
            .WithDescription("Reads one ticket by its number.");
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    /// <param name="services">The services of the call.</param>
    public static IReadOnlyList<Ticket> SearchTickets(string query, IServiceProvider services)
    {
        // begin-snippet: read-settings
        var settings = services.GetToolSettings<SearchTicketsSettings>();
        // end-snippet
        var tickets = services.GetRequiredService<ITicketClient>();

        return tickets.Search(query, settings.IncludeClosed, settings.Limit);
    }

    /// <param name="number">The ticket's number.</param>
    /// <param name="services">The services of the call.</param>
    /// <param name="ct">Cancelled when the call is.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);
}
// end-snippet
