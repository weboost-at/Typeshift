namespace Agentic.Generators.Models;

// An agent as registration needs it; Location is null for an agent of a referenced assembly
internal sealed record AgentSummary(string Type, string UniqueName, LocationInfo? Location);
