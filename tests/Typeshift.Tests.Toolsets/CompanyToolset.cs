using System.Text.Json.Serialization;

namespace Typeshift.Tests.Toolsets;

/// <summary>
/// How find_companies searches.
/// </summary>
public record FindCompaniesSettings
{
    /// <summary>
    /// The most companies to return.
    /// </summary>
    public int Limit { get; init; } = 3;

    /// <summary>
    /// The country the companies are in.
    /// </summary>
    public string Country { get; init; } = "AT";
}

/// <summary>
/// A company find_companies found.
/// </summary>
/// <param name="CompanyName">The company's name.</param>
/// <param name="Country">Its country.</param>
public record FoundCompany(string CompanyName, string Country);

/// <summary>
/// Companies, in an assembly of their own.
/// </summary>
public partial class CompanyToolset : Toolset
{
    /// <summary>
    /// Declares the tools.
    /// </summary>
    public CompanyToolset()
    {
        WithDisplayName("Companies");

        AddTool(FindCompanies)
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);

        AddTool(CountCompanies);
    }

    /// <summary>
    /// Finds companies by name.
    /// </summary>
    /// <param name="name">Part of the name.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The companies.</returns>
    public static IReadOnlyList<FoundCompany> FindCompanies(string name, FindCompaniesSettings settings) =>
        [.. Enumerable.Range(1, settings.Limit).Select(index => new FoundCompany($"{name} {index}", settings.Country))];

    /// <summary>
    /// Counts the companies.
    /// </summary>
    /// <returns>The count.</returns>
    public static int CountCompanies() => 3;
}

/// <summary>
/// The JSON contracts of the toolset's results, which the module registers.
/// </summary>
[JsonSerializable(typeof(IReadOnlyList<FoundCompany>))]
public partial class CompanyJsonContext : JsonSerializerContext;
