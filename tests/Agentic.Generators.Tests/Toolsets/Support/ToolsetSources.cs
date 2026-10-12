namespace Agentic.Generators.Tests.Toolsets.Support;

public static class ToolsetSources
{
    public const string WebResearch =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Agentic.Tools;
        using Agentic.Toolsets;
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
                    .WithSettings<SearchWebSettings>(settings => settings
                        .Configurable(s => s.Limit, s => s.Region));

                AddTool(ReadWebPage)
                    .WithName("read_web_page");

                AddTool(Forget)
                    .WithName("forget")
                    .WithDescription("Forgets what was read.");
            }

            /// <param name="query">What to search for.</param>
            /// <param name="country">Two-letter ISO country code for local results, or null.</param>
            /// <param name="serviceProvider">The services of the call.</param>
            /// <param name="ct">Cancels the search.</param>
            public static Task<IReadOnlyList<WebSearchResult>> SearchWebAsync(
                string query,
                string? country,
                IServiceProvider serviceProvider,
                CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<WebSearchResult>>([]);

            /// <summary>
            /// Reads the main content of a web page as markdown.
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

    public const string Nested =
        """
        using Agentic.Toolsets;

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

    public const string DocumentedTwice =
        """
        using Agentic.Toolsets;

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
        using Agentic.Toolsets;

        namespace Demo;

        /// <summary>
        /// Settings whose members are named like the members of a configuration view.
        /// </summary>
        public record ViewLikeSettings
        {
            /// <summary>
            /// A member named like the view's tool.
            /// </summary>
            public int Tool { get; init; }

            /// <summary>
            /// A member named like the view's factory.
            /// </summary>
            public int Create { get; init; }

            /// <summary>
            /// A member named like the view's reader.
            /// </summary>
            public int Get { get; init; }

            /// <summary>
            /// A member named like the view's writer.
            /// </summary>
            public int Set { get; init; }

            /// <summary>
            /// A member named like the view's stored values.
            /// </summary>
            public int Stored { get; init; }

            /// <summary>
            /// A member named like the view itself.
            /// </summary>
            public int ToolsConfiguration { get; init; }
        }

        /// <summary>
        /// A toolset with a tool named like its Tools class.
        /// </summary>
        public partial class ViewLikeToolset : Toolset
        {
            /// <summary>
            /// Declares the tool.
            /// </summary>
            public ViewLikeToolset()
            {
                AddTool(ListTools)
                    .WithName("tools")
                    .WithDescription("Lists the tools.")
                    .WithSettings<ViewLikeSettings>(settings => settings
                        .Configurable(s => s.Tool, s => s.Create, s => s.Get, s => s.Set, s => s.Stored)
                        .Configurable(s => s.ToolsConfiguration));
            }

            /// <summary>
            /// Lists the tools.
            /// </summary>
            /// <param name="services">The services of the call.</param>
            public static string ListTools(IServiceProvider services) => "";
        }
        """;
}
