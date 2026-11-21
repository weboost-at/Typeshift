namespace Typeshift.Generators.Models;

// IsReachable: the toolset's assembly can name it outside the class, so the tool list can extend it. ExperimentalIds
// are the ids of every [Experimental] a name of the toolset reports
internal sealed record ToolsetModel(
    TypeModel Type,
    bool IsReachable,
    EquatableArray<ToolModel> Tools,
    EquatableArray<string> ExperimentalIds);
