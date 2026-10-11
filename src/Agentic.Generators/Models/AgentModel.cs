namespace Agentic.Generators.Models;

internal sealed record AgentModel(
    TypeModel Type,
    string? UniqueName,
    LocationInfo? Location);
