namespace Typeshift.Samples.Companies;

internal partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        WithDisplayName("Companies");

        AddTool(FindCompanies)
            .ReadOnly()
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);

        AddTool(CountCompanies)
            .ReadOnly();
    }

    /// <summary>
    /// Finds the companies of a city.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    /// <param name="settings">The settings the search runs with; the model never sees them.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, FindCompaniesSettings settings) =>
    [
        .. KnownCompanies.All
            .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
            .Where(company => company.Active || settings.IncludeInactive)
            .Take(settings.Limit)
    ];

    /// <summary>
    /// Counts the active companies of a sector.
    /// </summary>
    /// <param name="sector">The sector the companies work in.</param>
    public static SectorCount CountCompanies(Sector sector) =>
        new(sector, KnownCompanies.All.Count(company => company.Active && company.Sector == sector));
}
