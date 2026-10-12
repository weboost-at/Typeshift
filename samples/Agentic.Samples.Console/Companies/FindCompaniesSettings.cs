namespace Agentic.Samples.Companies;

// begin-snippet: quickstart-settings-record
internal record FindCompaniesSettings
{
    public int Limit { get; init; } = 5;

    public bool IncludeInactive { get; init; }
}
// end-snippet
