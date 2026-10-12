using Agentic.Toolsets;

namespace Agentic.Tests.Toolsets.Companies;

public partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);

        AddTool(CreateCompanyProfile)
            .WithName("create_company_profile")
            .WithDescription("Creates the profile of a company and returns its name.");
    }

    /// <summary>
    /// Finds companies and returns the settings the search ran with.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="settings">The settings the search runs with.</param>
    public static FindCompaniesSettings FindCompanies(string query, FindCompaniesSettings settings) => settings;

    /// <param name="name">The company's legal name.</param>
    /// <param name="country">Two-letter ISO country code, or null when unknown.</param>
    public static string CreateCompanyProfile(string name, string? country = null) => name;
}
