namespace Typeshift.Generators.Tests.Diagnostics.Support;

// Sources of referenced assemblies and of subjects that use them
public static class ReferenceSources
{
    public const string PlainToolset =
        """
        using Typeshift;

        namespace Plain;

        public partial class PlainToolset : Toolset
        {
            public PlainToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string PlainToolsetAgent =
        """
        using Typeshift;
        using Plain;

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<PlainToolset>();
        }
        """;

    public const string CompanyToolset =
        """
        using Typeshift;

        namespace Companies;

        public record FindCompaniesSettings
        {
            public int Limit { get; init; } = 3;

            public string Country { get; init; } = "AT";
        }

        public partial class CompanyToolset : Toolset
        {
            public CompanyToolset()
            {
                AddTool(FindCompanies)
                    .WithDescription("Finds companies.")
                    .WithSettings<FindCompaniesSettings>()
                    .Configurable(s => s.Limit);

                AddTool(CountCompanies).WithDescription("Counts companies.");
            }

            public static string[] FindCompanies(string name, FindCompaniesSettings settings) => [name];

            public static int CountCompanies() => 3;
        }
        """;

    public const string SameNamedToolset =
        """
        using Typeshift;

        internal partial class SearchToolset : Toolset
        {
            public SearchToolset() => AddTool(FindCompanies).WithDescription("Finds companies too.");

            public static string FindCompanies(string name) => name;
        }
        """;

    public const string CompanyAgent =
        """
        using Typeshift;
        using Companies;
        using Microsoft.Extensions.DependencyInjection;

        namespace Research;

        internal partial class ResearchAgent : Agent
        {
            public ResearchAgent()
            {
                WithChatOptions()
                    .WithModel("openai", "fast-model")
                    .Configurable(o => o.Temperature);

                UseToolset<CompanyToolset>().Include(t => t.FindCompanies);

                ConfigureFindCompanies()
                    .WithCountry("DE");
            }
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    public const string UndescribedHelpAgent =
        """
        using Typeshift;

        namespace Help;

        public partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }
        """;

    public const string AskingHelpAgent =
        """
        using Typeshift;

        namespace Help;

        public partial class TicketToolset : Toolset
        {
            public TicketToolset() => AddTool(Close).WithDescription("Closes a ticket.").RequiresApproval();

            public static void Close(int number)
            {
            }
        }

        public partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Helps.");

                UseToolset<TicketToolset>();
            }
        }
        """;

    public const string HelpAgentUse =
        """
        using Typeshift;
        using Help;

        internal partial class LeadAgent : Agent
        {
            public LeadAgent() => UseAgent<HelpAgent>();
        }
        """;
}
