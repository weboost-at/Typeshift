namespace Typeshift.Samples.Tickets;

internal sealed record Ticket(int Number, string Title, bool Closed, DateTimeOffset CreatedAt);
