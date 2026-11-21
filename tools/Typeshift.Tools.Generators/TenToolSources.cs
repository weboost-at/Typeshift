using System.Text;

namespace Typeshift.Tools.Generators;

// A toolset of ten documented tools that share one settings type, and an agent that uses it: what a settings type
// costs when many tools of a toolset name it
internal static class TenToolSources
{
    private const int Tools = 10;

    public static string Pair(int index, int count)
    {
        var source = new StringBuilder(
            $$"""
            using Typeshift;

            namespace Scale;

            /// <summary>How area {{index}} of {{count}} is searched.</summary>
            public record Area{{index}}Settings
            {
                /// <summary>The most items.</summary>
                public int Limit { get; init; } = 10;

                /// <summary>Whether closed items count.</summary>
                public bool IncludeClosed { get; init; }
            }

            /// <summary>The tools of area {{index}}.</summary>
            public partial class Area{{index}}Toolset : Toolset
            {
                /// <summary>Declares the tools.</summary>
                public Area{{index}}Toolset()
                {

            """);

        for (var tool = 0; tool < Tools; tool++)
        {
            source.AppendLine(
                $"        AddTool(Find{index}_{tool}).WithSettings<Area{index}Settings>().Configurable(s => s.Limit);");
        }

        source.AppendLine("    }");

        for (var tool = 0; tool < Tools; tool++)
        {
            source.AppendLine(
                $$"""

                    /// <summary>Finds the items of kind {{tool}} in area {{index}}.</summary>
                    /// <param name="query">The text to look for.</param>
                    /// <param name="settings">How the search runs.</param>
                    /// <returns>The first item.</returns>
                    public static string Find{{index}}_{{tool}}(string query, Area{{index}}Settings settings) => query;
                """);
        }

        source.AppendLine(
            $$"""
            }

            /// <summary>Answers questions about area {{index}}.</summary>
            public partial class Area{{index}}Agent : Agent
            {
                /// <summary>Declares the agent.</summary>
                public Area{{index}}Agent()
                {
                    WithChatOptions()
                        .WithModel("p", "m")
                        .Configurable(o => o.Temperature);

                    UseToolset<Area{{index}}Toolset>();
                }
            }
            """);

        return source.ToString();
    }
}
