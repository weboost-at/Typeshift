using Agentic.Tools;
using Agentic.Toolsets;

namespace Agentic.Tests.Toolsets.Companies;

public partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        DisplayName = "Companies";

        AddTool(FindCompanies)
            .WithName("find_companies")
            .WithSettings<FindCompaniesSettings>(settings => settings
                .Configurable(s => s.Limit));

        AddTool(CreateCompanyProfile)
            .WithName("create_company_profile")
            .WithDescription("Creates the profile of a company and returns its name.");
    }

    /// <summary>
    /// Finds companies and returns the settings the search ran with.
    /// </summary>
    /// <param name="query">What to search for.</param>
    /// <param name="serviceProvider">The services of the call.</param>
    public static FindCompaniesSettings FindCompanies(string query, IServiceProvider serviceProvider) =>
        serviceProvider.GetToolSettings<FindCompaniesSettings>();

    /// <param name="name">The company's legal name.</param>
    /// <param name="country">Two-letter ISO country code, or null when unknown.</param>
    public static string CreateCompanyProfile(string name, string? country = null) => name;
}
