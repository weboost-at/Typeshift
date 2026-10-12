namespace Typeshift.Samples.Docs.Tickets;

// The ticket system's API, as a host registers it
internal interface ITicketClient
{
    IReadOnlyList<Ticket> Search(string query, bool includeClosed, int limit);

    Task<Ticket?> GetAsync(int number, CancellationToken ct);
}
