using System.Linq;
using Agentic.Generators.Models;

namespace Agentic.Generators;

internal static class Summaries
{
    public static ToolsetSummary From(ToolsetModel toolset) =>
        new(
            toolset.Type.FullName,
            toolset.Tools
                .Select(tool => new ToolSummary(
                    Name: tool.Name,
                    Settings: tool.Settings?.Type,
                    Configurable: tool.Settings?.Properties
                        .Where(property => property.IsConfigurable)
                        .Select(property => property.Name)
                        .ToEquatableArray() ?? EquatableArray<string>.Empty,
                    Location: tool.Location))
                .ToEquatableArray());
}
