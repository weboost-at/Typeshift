namespace Typeshift.Generators.Models;

// A class the generator handles, found by its base type: exactly one of the two is set
internal sealed record ClassModel(ToolsetModel? Toolset, AgentModel? Agent);
