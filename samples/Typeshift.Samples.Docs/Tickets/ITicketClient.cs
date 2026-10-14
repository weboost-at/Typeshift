namespace Typeshift.Samples.Tickets;

internal interface ITicketClient
{
    IReadOnlyList<Ticket> Search(string query, bool includeClosed, int limit);

    Task<Ticket?> GetAsync(int number, CancellationToken ct);

    Task CloseAsync(int number, string reason, CancellationToken ct);
}
