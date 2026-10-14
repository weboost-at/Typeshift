namespace Typeshift.Generators.Models;

// IsReachable: the toolset's assembly can name it outside the class, so the tool list can extend it
internal sealed record ToolsetModel(TypeModel Type, bool IsReachable, EquatableArray<ToolModel> Tools);
