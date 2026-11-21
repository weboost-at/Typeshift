using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Companies;

// Stands in for a company database
internal static class KnownCompanies
{
    public static readonly Company[] All =
    [
        new("Danube Robotics", "Vienna", Active: true, Sector.Robotics),
        new("Prater Motion", "Vienna", Active: true, Sector.Drives),
        new("Ringstrasse Automation", "Vienna", Active: false, Sector.Robotics),
        new("Alpine Grippers", "Graz", Active: true, Sector.Robotics),
        new("Wienerwald Drives", "Vienna", Active: true, Sector.Drives)
    ];

    // A company's logo, an image whose bytes stand in for one
    public static DataContent LogoOf(Company company) =>
        new(System.Text.Encoding.UTF8.GetBytes(company.Name), "image/png");
}
