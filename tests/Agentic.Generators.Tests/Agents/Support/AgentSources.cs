namespace Agentic.Generators.Tests.Agents.Support;

public static class AgentSources
{
    public const string Profiling =
        """
        using Agentic.Agents;
        using Microsoft.Extensions.AI;

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
                WithUniqueName("profiling_agent");
                WithDescription("Profiles new company information.");

                var options = new ChatOptions
                {
                    AllowMultipleToolCalls = true,
                    Temperature = 0.2f
                };

                WithChatOptions(options, picker => picker
                    .Configurable(o => o.Temperature)
                    .Configurable(o => o.MaxOutputTokens)
                    .Configurable(o => o.Instructions));

                UseToolset<WebResearchToolset>(WebResearchToolset.SearchWebAsync, WebResearchToolset.ReadWebPage);

                WithToolSettings(new SearchWebSettings { Limit = 5, SafeSearch = false });
            }
        }
        """;

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
                WithUniqueName("research_agent");
                UseToolset<WebResearchToolset>();
                WithToolSettings(new SearchWebSettings { Limit = 3 });
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
                    .WithSettings<LookupSettings>(settings => settings.Configurable(s => s.Limit));

                AddTool(Configure)
                    .WithName("configuration")
                    .WithDescription("Reads the configuration.")
                    .WithSettings<LookupSettings>(settings => settings.Configurable(s => s.Limit));
            }

            public static string Create(string name, IServiceProvider services) => name;

            public static string Configure(IServiceProvider services) => "";
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
                WithUniqueName("lookup_agent");
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
                WithUniqueName("lookup_agent");
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
                    WithUniqueName("lookup_agent");
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
                    WithUniqueName("first_lookup_agent");
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
                    WithUniqueName("second_lookup_agent");
                    UseToolset<LookupToolset>();
                }
            }
        }
        """;
}
