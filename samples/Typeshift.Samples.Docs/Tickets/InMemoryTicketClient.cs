namespace Typeshift.Samples.Tickets;

// The tickets of the guide's scenarios
internal sealed class InMemoryTicketClient(TimeProvider time) : ITicketClient
{
    private readonly List<Ticket> _tickets = [new(1234, "Refund for order 77", Closed: false, time.GetUtcNow())];

    public IReadOnlyList<Ticket> Search(string query, bool includeClosed, int limit) =>
    [
        .. _tickets
            .Where(ticket => ticket.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(ticket => includeClosed || !ticket.Closed)
            .Take(limit)
    ];

    public Task<Ticket?> GetAsync(int number, CancellationToken ct) =>
        Task.FromResult(_tickets.FirstOrDefault(ticket => ticket.Number == number));

    public Task CloseAsync(int number, string reason, CancellationToken ct)
    {
        var index = _tickets.FindIndex(ticket => ticket.Number == number);
        if (index >= 0) _tickets[index] = _tickets[index] with { Closed = true };

        return Task.CompletedTask;
    }
}
