namespace Typeshift.Generators.Tests.Toolsets.Support;

public static class ToolsetSources
{
    public const string WebResearch =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift.Tools;
        using Typeshift.Toolsets;
        using Microsoft.Extensions.AI;

        namespace Demo;

        /// <summary>
        /// The settings of the web search.
        /// </summary>
        public record SearchWebSettings
        {
            /// <summary>
            /// The most results to return.
            /// </summary>
            public int Limit { get; init; } = 10;

            /// <summary>
            /// Whether explicit results are filtered.
            /// </summary>
            public bool SafeSearch { get; init; } = true;

            /// <summary>
            /// The region results are ranked for.
            /// </summary>
            public string? Region { get; init; }
        }

        /// <summary>
        /// How a page is read.
        /// </summary>
        public enum ReadMode
        {
            /// <summary>
            /// Only the main content.
            /// </summary>
            Main,

            /// <summary>
            /// The whole page.
            /// </summary>
            Full
        }

        /// <summary>
        /// One search result.
        /// </summary>
        public record WebSearchResult(string Url, string Title);

        /// <summary>
        /// Searches and reads the public web.
        /// </summary>
        public partial class WebResearchToolset : Toolset
        {
            private const string SearchWebDescription =
                "Search the public web. Returns the most relevant results first.";

            /// <summary>
            /// Declares the tools.
            /// </summary>
            public WebResearchToolset()
            {
                DisplayName = "Web research";

                AddTool(SearchWebAsync)
                    .WithName("search_web")
                    .WithDescription(SearchWebDescription)
                    .WithSettings<SearchWebSettings>()
                    .Configurable(s => s.Limit)
                    .Configurable(s => s.Region);

                AddTool(ReadWebPage)
                    .WithName("read_web_page");

                AddTool(Forget)
                    .WithName("forget")
                    .WithDescription("Forgets what was read.");
            }

            /// <param name="query">What to search for.</param>
            /// <param name="country">Two-letter ISO country code for local results, or null.</param>
            /// <param name="settings">The settings the search runs with.</param>
            /// <param name="serviceProvider">The services of the call.</param>
            /// <param name="ct">Cancels the search.</param>
            public static Task<IReadOnlyList<WebSearchResult>> SearchWebAsync(
                string query,
                string? country,
                SearchWebSettings settings,
                IServiceProvider serviceProvider,
                CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<WebSearchResult>>([]);

            /// <summary>
            /// Reads the main content of a web page as markdown, at most
            /// <paramref name="maximumLength"/> characters of it. Pass the URL of a
            /// <see cref="WebSearchResult"/> from the <see cref="IReadOnlyList{T}"/> the search returns.
            /// </summary>
            /// <param name="url">The page to read.</param>
            /// <param name="maximumLength">The longest text to return.</param>
            /// <param name="mode">How much of the page to read.</param>
            public static string ReadWebPage(string url, int maximumLength = 4000, ReadMode mode = ReadMode.Main) =>
                url;

            /// <summary>
            /// Forgets what was read.
            /// </summary>
            /// <param name="arguments">The raw arguments of the call.</param>
            public static ValueTask Forget(AIFunctionArguments arguments) => ValueTask.CompletedTask;
        }
        """;

    // A default of each kind that needs care to come out as a literal of the parameter's own type
    public const string Defaults =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        internal enum Level
        {
            Low,
            High
        }

        internal partial class GreetingToolset : Toolset
        {
            public GreetingToolset() => AddTool(Greet).WithName("greet").WithDescription("Greets.");

            public static string Greet(
                string greeting = "say \"hi\"",
                bool loud = true,
                long limit = 5,
                Level level = Level.High,
                Level unnamed = (Level)7,
                Level negative = (Level)(-1),
                Level? fallback = Level.Low) =>
                greeting;
        }
        """;

    public const string Nested =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        /// <summary>
        /// Holds a toolset.
        /// </summary>
        public static partial class Outer
        {
            /// <summary>
            /// A toolset nested in another class.
            /// </summary>
            public partial class EchoToolset : Toolset
            {
                /// <summary>
                /// Declares the tool.
                /// </summary>
                public EchoToolset()
                {
                    AddTool(Echo)
                        .WithName("echo")
                        .WithDescription("Echoes the text.");
                }

                /// <summary>
                /// Echoes the text.
                /// </summary>
                /// <param name="text">The text to echo.</param>
                public static string Echo(string text) => text;
            }
        }
        """;

    // A description spanning lines, as a line break or a multi-line raw string puts it
    public const string MultiLineDescription =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.\nTwice.");

            public static string Echo(string text) => text;
        }
        """;

    public const string KeywordNamespace =
        """
        using Typeshift.Toolsets;

        namespace Demo.@event;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.");

            public static string Echo(string text) => text;
        }
        """;

    public const string DocumentedTwice =
        """
        using Typeshift.Toolsets;

        namespace Demo;

        /// <summary>
        /// Echoes text.
        /// </summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>
            /// Declares the tool.
            /// </summary>
            public EchoToolset() => AddTool(Echo).WithName("echo");

            /// <summary>
            /// Echoes the text.
            /// </summary>
            /// <param name="text">The text to echo.</param>
            /// <param name="text">The text, again.</param>
            public static string Echo(string text) => text;
        }
        """;

    public const string ReservedNames =
        """
        using System;
        using Typeshift.Toolsets;

        namespace Demo;

        /// <summary>
        /// Settings whose members are named like the members of the generated overrides.
        /// </summary>
        public record ClashingSettings
        {
            /// <summary>
            /// A member named like the overrides' writer.
            /// </summary>
            public int Serialize { get; init; }

            /// <summary>
            /// A member named like the overrides' reader.
            /// </summary>
            public int Deserialize { get; init; }

            /// <summary>
            /// A member named like the overrides themselves.
            /// </summary>
            public int ToolsOverrides { get; init; }
        }

        /// <summary>
        /// A toolset with tools named like members of its Tools class and of object.
        /// </summary>
        public partial class ClashingToolset : Toolset
        {
            /// <summary>
            /// Declares the tools.
            /// </summary>
            public ClashingToolset()
            {
                AddTool(ListTools)
                    .WithName("tools")
                    .WithDescription("Lists the tools.")
                    .WithConfigurableSettings<ClashingSettings>();

                AddTool(Describe)
                    .WithName("equals")
                    .WithDescription("Compares the tools.");

                AddTool(Describe)
                    .WithName("to_string")
                    .WithDescription("Prints the tools.");
            }

            /// <summary>
            /// Lists the tools.
            /// </summary>
            /// <param name="settings">The settings of the call.</param>
            public static string ListTools(ClashingSettings settings) => "";

            /// <summary>
            /// Describes the tools.
            /// </summary>
            public static string Describe() => "";
        }
        """;
}
