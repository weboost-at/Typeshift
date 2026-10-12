namespace Typeshift.Tests.Toolsets.Companies;

public record FindCompaniesSettings
{
    public int Limit { get; init; } = 10;

    public bool IncludeInactive { get; init; }
}
