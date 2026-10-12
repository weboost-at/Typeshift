namespace Agentic.Samples.Companies;

// Stands in for a company database
internal static class KnownCompanies
{
    public static readonly Company[] All =
    [
        new("Danube Robotics", "Vienna", Active: true),
        new("Prater Motion", "Vienna", Active: true),
        new("Ringstrasse Automation", "Vienna", Active: false),
        new("Alpine Grippers", "Graz", Active: true),
        new("Wienerwald Drives", "Vienna", Active: true)
    ];
}
