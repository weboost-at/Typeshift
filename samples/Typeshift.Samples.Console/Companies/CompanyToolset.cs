using Typeshift.Toolsets;

namespace Typeshift.Samples.Companies;

// begin-snippet: quickstart-toolset
internal partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);
    }

    /// <summary>
    /// Finds the companies of a city, best matches first.
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
}
// end-snippet
