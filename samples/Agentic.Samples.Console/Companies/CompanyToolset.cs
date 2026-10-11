using Agentic.Tools;
using Agentic.Toolsets;

namespace Agentic.Samples.Companies;

internal partial class CompanyToolset : Toolset
{
    private static readonly Company[] KnownCompanies =
    [
        new("Danube Robotics", "Vienna", Active: true),
        new("Prater Motion", "Vienna", Active: true),
        new("Ringstrasse Automation", "Vienna", Active: false),
        new("Alpine Grippers", "Graz", Active: true),
        new("Wienerwald Drives", "Vienna", Active: true)
    ];

    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>(settings => settings
                .Configurable(s => s.Limit));
    }

    /// <summary>
    /// Finds the companies of a city, best matches first.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    /// <param name="services">The services of the call.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, IServiceProvider services)
    {
        var settings = services.GetToolSettings<FindCompaniesSettings>();

        return
        [
            .. KnownCompanies
                .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
                .Where(company => company.Active || settings.IncludeInactive)
                .Take(settings.Limit)
        ];
    }
}
