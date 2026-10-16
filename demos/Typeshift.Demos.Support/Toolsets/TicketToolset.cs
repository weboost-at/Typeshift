using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Support.Domain;

namespace Typeshift.Demos.Support.Toolsets;

internal record SearchTicketsSettings
{
    public int Limit { get; init; } = 10;

    public bool IncludeClosed { get; init; }
}

internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        WithDisplayName("Tickets");

        AddTool(SearchTickets)
            .ReadOnly()
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.Limit)
            .Configurable(s => s.IncludeClosed);

        AddTool(GetTicket)
            .ReadOnly();

        AddTool(CloseTicket)
            .RequiresApproval();
    }

    /// <summary>
    /// Searches the customer's own tickets, newest first.
    /// </summary>
    /// <param name="query">Words of the ticket's title; empty for all.</param>
    public static IReadOnlyList<Ticket> SearchTickets(string query, SearchTicketsSettings settings, IServiceProvider services)
    {
        var customer = services.GetRequiredService<CurrentCustomer>().Id;
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return
        [
            .. services.GetRequiredService<CrmData>().Tickets
                .Where(ticket => ticket.Customer == customer)
                .Where(ticket => settings.IncludeClosed || ticket.Status != "closed")
                .Where(ticket => words.All(word => ticket.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(ticket => ticket.Number)
                .Take(settings.Limit)
        ];
    }

    /// <summary>
    /// Reads one of the customer's tickets.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static Ticket GetTicket(int number, IServiceProvider services)
    {
        var customer = services.GetRequiredService<CurrentCustomer>().Id;

        return services.GetRequiredService<CrmData>().Tickets
            .FirstOrDefault(ticket => ticket.Number == number && ticket.Customer == customer)
            ?? throw new InvalidOperationException($"The customer has no ticket {number}; search for it first.");
    }

    /// <summary>
    /// Closes a ticket for good.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    /// <param name="reason">Why it is closed, as the customer will read it.</param>
    public static string CloseTicket(int number, string reason, IServiceProvider services)
    {
        var data = services.GetRequiredService<CrmData>();
        var ticket = GetTicket(number, services);
        data.Tickets[data.Tickets.IndexOf(ticket)] = ticket with { Status = "closed" };
        data.Log.Add($"ticket {number} closed: {reason}");
        return $"Ticket {number} is closed.";
    }
}
