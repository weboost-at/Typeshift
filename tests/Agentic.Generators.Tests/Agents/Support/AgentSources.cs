namespace Agentic.Generators.Tests.Agents.Support;

public static class AgentSources
{
    public const string Profiling =
        """"
        using Agentic.Agents;

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
                    .SearchWeb()
                    .ReadWebPage();

                WithToolSettings()
                    .SearchWeb()
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
        using Agentic.Agents;
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
                UseToolset<WebResearchToolset>().SearchWeb();
                WithToolSettings().SearchWeb().WithLimit(3);
            }
        }
        """;

    // Settings and toolset for the agents below; internal, so they need no docs
    public const string Lookup =
        """
        using System;
        using Agentic.Toolsets;

        namespace Demo;

        internal record LookupSettings
        {
            public int Limit { get; init; } = 10;
        }

        internal partial class LookupToolset : Toolset
        {
            public LookupToolset()
            {
                AddTool(Create)
                    .WithName("create")
                    .WithDescription("Creates an entry.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);

                AddTool(Configure)
                    .WithName("configuration")
                    .WithDescription("Reads the configuration.")
                    .WithSettings<LookupSettings>()
                    .Configurable(s => s.Limit);
            }

            public static string Create(string name, LookupSettings settings) => name;

            public static string Configure(LookupSettings settings) => "";
        }
        """;

    public const string ReservedToolNames =
        """
        using Agentic.Agents;

        namespace Demo;

        internal partial class LookupAgent : AgentDefinition
        {
            public LookupAgent()
            {
                WithName("lookup_agent");
                UseToolset<LookupToolset>();
            }
        }
        """;

    public const string PublicAgentOfInternalToolset =
        """
        using Agentic.Agents;

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
        using Agentic.Agents;

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
        using Agentic.Agents;

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
}
