namespace Typeshift.Samples.Docs.Tickets;

// begin-snippet: settings-record
internal record SearchTicketsSettings
{
    public int Limit { get; init; } = 10;

    public bool IncludeClosed { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
// end-snippet
