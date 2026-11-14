namespace Typeshift.Samples.Companies;

internal enum Sector
{
    Robotics,
    Drives
}

internal record Company(string Name, string City, bool Active, Sector Sector);

// What count_companies returns: an enum in a tool's result, as in its parameter
internal sealed record SectorCount(Sector Sector, int Count);
