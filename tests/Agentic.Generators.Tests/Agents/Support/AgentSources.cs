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
}
