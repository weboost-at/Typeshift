namespace Typeshift.Generators.Tests.Incremental.Support;

// A toolset of three documented tools, one with settings, and an agent that uses two toolsets: one pair of a
// project with many
public static class ScaleSources
{
    public const string Unrelated =
        """
        namespace Scale;

        /// <summary>Unrelated code.</summary>
        public class Unrelated
        {
            /// <summary>A value.</summary>
            public int Value { get; set; }

            /// <summary>Computes.</summary>
            /// <returns>A number.</returns>
            public int Compute() => Value + 1;
        }
        """;

    public static string Pair(int index, int count) =>
        $$"""
        using System.Collections.Generic;
        using Typeshift;

        namespace Scale;

        /// <summary>An item {{index}}.</summary>
        public record Item{{index}}(int Id, string Name);

        /// <summary>How search {{index}} runs.</summary>
        public record Search{{index}}Settings
        {
            /// <summary>The most items.</summary>
            public int Limit { get; init; } = 5;

            /// <summary>Whether closed items count.</summary>
            public bool IncludeClosed { get; init; }
        }

        /// <summary>The tools of area {{index}}.</summary>
        public partial class Toolset{{index}} : Toolset
        {
            /// <summary>Declares the tools.</summary>
            public Toolset{{index}}()
            {
                AddTool(Search{{index}})
                    .ReadOnly()
                    .WithSettings<Search{{index}}Settings>()
                    .Configurable(s => s.Limit);

                AddTool(Get{{index}});
                AddTool(Delete{{index}});
            }

            /// <summary>Searches the items of area {{index}}.</summary>
            /// <param name="query">The text to look for.</param>
            /// <param name="settings">How the search runs.</param>
            /// <returns>The items.</returns>
            public static IReadOnlyList<Item{{index}}> Search{{index}}(
                string query,
                Search{{index}}Settings settings) =>
                [new({{index}}, query)];

            /// <summary>Gets an item of area {{index}}.</summary>
            /// <param name="id">The item's id.</param>
            /// <returns>The item.</returns>
            public static Item{{index}} Get{{index}}(int id) => new(id, "x");

            /// <summary>Deletes an item of area {{index}}.</summary>
            /// <param name="id">The item's id.</param>
            public static void Delete{{index}}(int id)
            {
            }
        }

        /// <summary>Answers questions about area {{index}}.</summary>
        public partial class Agent{{index}} : Agent
        {
            /// <summary>Declares the agent.</summary>
            public Agent{{index}}()
            {
                WithInstructions("Answer briefly.");

                WithChatOptions()
                    .WithProvider("p")
                    .WithModel("m")
                    .WithTemperature(0.2f)
                    .Configurable(o => o.ModelId)
                    .Configurable(o => o.Temperature);

                WithRunOptions()
                    .Configurable(r => r.MaxToolIterations);

                UseToolset<Toolset{{index}}>();
                UseToolset<Toolset{{(index + 1) % count}}>();

                ConfigureSearch{{index}}()
                    .WithLimit(3);
            }
        }
        """;
}
