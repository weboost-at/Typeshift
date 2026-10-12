namespace Agentic.Samples.Docs.Tickets;

internal sealed class InMemoryTicketClient : ITicketClient
{
    private static readonly Ticket[] Tickets =
    [
        new(42, "Refund not received", Closed: false),
        new(41, "Refund for a damaged parcel", Closed: true),
        new(40, "Invoice address wrong", Closed: false)
    ];

    public IReadOnlyList<Ticket> Search(string query, bool includeClosed, int limit) =>
    [
        .. Tickets
            .Where(ticket => ticket.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(ticket => includeClosed || !ticket.Closed)
            .Take(limit)
    ];

    public Task<Ticket?> GetAsync(int number, CancellationToken ct) =>
        Task.FromResult(Tickets.FirstOrDefault(ticket => ticket.Number == number));
}
