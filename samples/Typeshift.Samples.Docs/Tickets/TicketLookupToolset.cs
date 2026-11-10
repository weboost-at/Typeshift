using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Tickets;

// begin-snippet: union-parameter
internal union TicketKey(int, string);

internal partial class TicketLookupToolset : Toolset
{
    public TicketLookupToolset()
    {
        AddTool(FindTicketAsync)
            .ReadOnly();
    }

    /// <summary>
    /// Finds a ticket by its number, or by words its title contains.
    /// </summary>
    /// <param name="key">The ticket's number, or words its title contains.</param>
    public static async Task<Ticket?> FindTicketAsync(TicketKey key, IServiceProvider services, CancellationToken ct)
    {
        var tickets = services.GetRequiredService<ITicketClient>();

        return key switch
        {
            int number => await tickets.GetAsync(number, ct),
            string words => tickets.Search(words, includeClosed: true, limit: 1).FirstOrDefault()
        };
    }
}
// end-snippet
