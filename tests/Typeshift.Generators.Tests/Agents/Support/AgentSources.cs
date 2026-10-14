namespace Typeshift.Generators.Tests.Agents.Support;

public static class AgentSources
{
    public const string Profiling =
        """"
        using Typeshift.Agents;

        namespace Demo;

        /// <summary>
        /// Profiles companies.
        /// </summary>
        public partial class ProfilingAgent : AgentDefinition
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public ProfilingAgent()
            {
                WithName("profiling_agent");
                WithDescription("Profiles new company information.");

                WithChatOptions()
                    .WithAllowMultipleToolCalls(true)
                    .WithTemperature(0.2f)
                    .Configurable(o => o.Temperature)
                    .Configurable(o => o.MaxOutputTokens)
                    .Configurable(o => o.Instructions);

                UseToolset<WebResearchToolset>()
                    .Include(t => t.SearchWeb)
                    .Include(t => t.ReadWebPage);

                ConfigureSearchWeb()
                    .WithLimit(5)
                    .WithSafeSearch(false);
            }

            /// <inheritdoc />
            protected override string GetInstructions() =>
                """
                Profile the company from what the web says about it.
                """;
        }
        """";

    public const string UsesWebResearch =
        """
        using Typeshift.Agents;
        using Demo;

        namespace App;

        /// <summary>
        /// Researches the web.
        /// </summary>
        public partial class ResearchAgent : AgentDefinition
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public ResearchAgent()
            {
                WithName("research_agent");
                UseToolset<WebResearchToolset>().Exclude(t => t.ReadWebPage).Exclude(t => t.Forget);
                ConfigureSearchWeb().WithLimit(3);
            }
        }
        """;

    // Settings and toolset for the agents below, with tools named like members of the agent's Settings record;
    // internal, so they need no docs
    public const string Lookup =
        """
        using System;
        using Typeshift.Toolsets;

        namespace Demo;

        internal record LookupSettings
        {
            public int Limit { get; init; } = 10;
        }

        internal partial class LookupToolset : Toolset
        {
            public LookupToolset()
            {
                AddTool(Find)
                    .WithName("model")
                    .WithDescription("Finds a model.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);

                AddTool(Find)
                    .WithName("settings")
                    .WithDescription("Finds settings.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);

                AddTool(Find)
                    .WithName("serialize")
                    .WithDescription("Finds a serializer.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);

                AddTool(Find)
                    .WithName("equals")
                    .WithDescription("Finds equal entries.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);

                AddTool(Find)
                    .WithName("temperature")
                    .WithDescription("Finds a temperature.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);
            }

            public static string Find(string name, LookupSettings settings) => name;
        }
        """;

    public const string ReservedToolNames =
        """
        using Typeshift.Agents;

        namespace Demo;

        internal partial class LookupAgent : AgentDefinition
        {
            public LookupAgent()
            {
                WithName("lookup_agent");
                WithChatOptions().Configurable(o => o.Temperature);
                UseToolset<LookupToolset>();
            }
        }
        """;

    public const string PublicAgentOfInternalToolset =
        """
        using Typeshift.Agents;

        namespace Demo;

        /// <summary>
        /// A public agent offering the tools of an internal toolset.
        /// </summary>
        public partial class LookupAgent : AgentDefinition
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public LookupAgent()
            {
                WithName("lookup_agent");
                UseToolset<LookupToolset>();
            }
        }
        """;

    public const string PublicAgentInInternalClass =
        """
        using Typeshift.Agents;

        namespace Demo;

        internal static partial class Holder
        {
            /// <summary>
            /// A public agent that only its assembly can see.
            /// </summary>
            public partial class LookupAgent : AgentDefinition
            {
                /// <summary>
                /// Declares the agent.
                /// </summary>
                public LookupAgent()
                {
                    WithName("lookup_agent");
                    UseToolset<LookupToolset>();
                }
            }
        }
        """;

    public const string NestedTwins =
        """
        using Typeshift.Agents;

        namespace Demo;

        internal static partial class First
        {
            internal partial class LookupAgent : AgentDefinition
            {
                public LookupAgent()
                {
                    WithName("first_lookup_agent");
                    UseToolset<LookupToolset>();
                }
            }
        }

        internal static partial class Second
        {
            internal partial class LookupAgent : AgentDefinition
            {
                public LookupAgent()
                {
                    WithName("second_lookup_agent");
                    UseToolset<LookupToolset>();
                }
            }
        }
        """;

    public const string InheritedSetting =
        """
        using Typeshift.Agents;
        using Typeshift.Toolsets;

        namespace Demo;

        internal record PagedSettings
        {
            public int Limit { get; init; } = 10;
        }

        internal record SearchSettings : PagedSettings
        {
            public bool Strict { get; init; }
        }

        internal partial class SearchToolset : Toolset
        {
            public SearchToolset() =>
                AddTool(Search)
                    .WithName("search")
                    .WithDescription("Searches.")
                    .WithSettings<SearchSettings>()
                    .Configurable(s => s.Limit);

            public static string Search(string query, SearchSettings settings) => query;
        }

        internal partial class SearchAgent : AgentDefinition
        {
            public SearchAgent()
            {
                UseToolset<SearchToolset>();
                ConfigureSearch().WithLimit(3);
            }
        }
        """;

    // A class named like its namespace and a namespace named System, both shadowing the start of a cref
    public const string ShadowingNames =
        """
        using Typeshift.Agents;
        using Typeshift.Toolsets;

        namespace Chatbot.System;

        /// <summary>
        /// A class named like the namespace it lives in.
        /// </summary>
        public static class Chatbot;

        /// <summary>
        /// Echoes text.
        /// </summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>
            /// Declares the tool.
            /// </summary>
            public EchoToolset() => AddTool(Echo).WithName("echo").WithDescription("Echoes the text.");

            /// <summary>
            /// Echoes the text.
            /// </summary>
            /// <param name="text">The text to echo.</param>
            public static string Echo(string text) => text;
        }

        /// <summary>
        /// Echoes what it is told.
        /// </summary>
        public partial class EchoAgent : AgentDefinition
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    // A tool named like the generated builder, settings with an obsolete member and summaries that need escaping
    public const string AwkwardSettings =
        """
        using System;
        using Typeshift.Agents;
        using Typeshift.Toolsets;

        namespace Demo;

        internal record AwkwardSettings
        {
            /// <summary>
            /// Fewer than 5 &amp; more &lt;tags&gt;.
            /// </summary>
            public int Limit { get; init; } = 3;

            /// <summary>
            /// Results &lt; limit.
            /// </summary>
            public bool Strict { get; init; }

            [Obsolete("Use Limit.")]
            public int Old { get; init; }
        }

        internal partial class AwkwardToolset : Toolset
        {
            public AwkwardToolset() =>
                AddTool(Tool)
                    .WithName("tool")
                    .WithDescription("Finds &lt; things & more.")
                    .WithSettings<AwkwardSettings>()
                    .Configurable(s => s.Limit);

            public static string Tool(string query, AwkwardSettings settings) => query;
        }

        internal partial class AwkwardAgent : AgentDefinition
        {
            public AwkwardAgent()
            {
                UseToolset<AwkwardToolset>();
                ConfigureTool().WithStrict(true);
            }
        }
        """;
}
