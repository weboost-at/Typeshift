namespace Agentic.Catalogs;

/// <summary>
/// An agent as the catalog lists it, defined in code or stored as data.
/// </summary>
public sealed record AgentListing(string UniqueName, string DisplayName, string? Description, bool IsStored);
