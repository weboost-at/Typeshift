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

    // A second assembly with a find_companies, which has overrides as the first one's has
    public const string SearchToolset =
        """
        using Typeshift;

        namespace Search;

        public record FindSettings
        {
            public int Limit { get; init; } = 5;
        }

        public partial class SearchToolset : Toolset
        {
            public SearchToolset() =>
                AddTool(FindCompanies).WithDescription("Finds companies too.").WithConfigurableSettings<FindSettings>();

            public static string FindCompanies(string name, FindSettings settings) => name;
        }
        """;

    public const string BothCompanyToolsetsAgent =
        """
        using Typeshift;

        namespace Research;

        internal partial class ResearchAgent : Agent
        {
            public ResearchAgent()
            {
                WithDescription("Researches companies.");
                UseToolset<Companies.CompanyToolset>();
                UseToolset<Search.SearchToolset>();
            }
        }
        """;

    // As a library built by an earlier Typeshift left it: a module of another stamp version, which this generator
    // does not read
    public const string StaleStampToolset =
        """
        using Typeshift;

        [assembly: Typeshift.Generated.GeneratedModule(typeof(Stale.StaleModule), 1)]

        namespace Stale;

        public static class StaleModule;

        public partial class StaleToolset : Toolset
        {
            public StaleToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    // Times is obsolete and not configurable, so no generated member names it, in this project or in one that
    // references it
    public const string ObsoleteSettingToolset =
        """
        #pragma warning disable CS1591

        using System;
        using Typeshift;

        namespace Echoes;

        public record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            [Obsolete("Use Repeat.")]
            public int Times { get; init; }
        }

        public partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Repeat);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string ObsoleteSettingAgent =
        """
        using Typeshift;

        namespace Research;

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                WithDescription("Echoes.");
                UseToolset<Echoes.EchoToolset>();
                ConfigureEcho().WithRepeat(2);
            }
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

    public const string EmptyHost =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        namespace Host;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    public const string BillingHelpAgent =
        """
        using Typeshift;

        namespace Billing;

        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }
        """;

    public const string SupportHelpAgent =
        """
        using Typeshift;

        namespace Support;

        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
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

    public const string QuestioningHelpAgent =
        """
        using Typeshift;

        namespace Help;

        public partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Helps.");

                UseQuestions();
            }
        }
        """;

    public const string HelpAgentWithOwnTool =
        """
        using Typeshift;

        namespace Help;

        public partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Helps.");

                AddTool(Close).WithDescription("Closes a ticket.");
            }

            private static void Close(int number)
            {
            }
        }
        """;

    public const string HelpAgentHandoffWithOwnTool =
        """
        using Typeshift;
        using Help;

        internal partial class LeadAgent : Agent
        {
            public LeadAgent()
            {
                AddTool(CloseLead).WithName("close").WithDescription("Closes a lead.");

                UseHandoff<HelpAgent>();
            }

            private static void CloseLead(int lead)
            {
            }
        }
        """;

    public const string HelpAgentHandoffWithToolset =
        """
        using Typeshift;
        using Help;

        internal partial class LeadToolset : Toolset
        {
            public LeadToolset() => AddTool(Close).WithDescription("Closes a lead.");

            public static void Close(int lead)
            {
            }
        }

        internal partial class LeadAgent : Agent
        {
            public LeadAgent()
            {
                UseToolset<LeadToolset>();

                UseHandoff<HelpAgent>();
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

    public const string ExperimentalToolset =
        """
        using System.Diagnostics.CodeAnalysis;
        using Typeshift;

        namespace Trials;

        [Experimental("TRIAL001")]
        public enum Depth
        {
            Shallow,
            Deep
        }

        [Experimental("TRIAL002")]
        public record TrialSettings
        {
        #pragma warning disable TRIAL001
            public Depth Depth { get; init; }
        #pragma warning restore TRIAL001

            public int Limit { get; init; } = 3;
        }

        [Experimental("TRIAL003")]
        public partial class TrialToolset : Toolset
        {
            public TrialToolset()
            {
        #pragma warning disable TRIAL002
                AddTool(Try).WithDescription("Tries.").WithConfigurableSettings<TrialSettings>();
        #pragma warning restore TRIAL002
            }

        #pragma warning disable TRIAL002
            public static string Try(string text, TrialSettings settings) => text;
        #pragma warning restore TRIAL002
        }
        """;

    public const string ExperimentalToolsetAgent =
        """
        using Typeshift;
        using Trials;

        namespace Research;

        internal partial class TrialAgent : Agent
        {
            public TrialAgent()
            {
                WithDescription("Tries.");

        #pragma warning disable TRIAL001, TRIAL003
                UseToolset<TrialToolset>();
                ConfigureTry().WithDepth(Depth.Deep);
        #pragma warning restore TRIAL001, TRIAL003
            }
        }
        """;

    // Generated code names types from global::, which a reference under an extern alias alone is no part of
    public const string AliasedToolsetAgent =
        """
        extern alias lib;

        using lib::Demo;
        using Microsoft.Extensions.DependencyInjection;
        using Typeshift;

        namespace App;

        internal partial class AliasedAgent : Agent
        {
            public AliasedAgent()
            {
                WithDescription("Uses an aliased reference.");
                UseToolset<TicketToolset>();
            }
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    // A toolset for an assembly of any name: the namespace, the class and its one tool method
    public static string EchoToolset(string ns, string toolset, string method) =>
        $$"""
        #pragma warning disable CS1591

        using Typeshift;

        namespace {{ns}};

        public partial class {{toolset}} : Toolset
        {
            public {{toolset}}() => AddTool({{method}}).WithDescription("Echoes.");

            public static string {{method}}(string text) => text;
        }
        """;

    // One toolset in two files, each with a constructor that declares the same tool
    public const string SplitToolsetFirst =
        """
        using Typeshift;

        namespace Demo;

        internal partial class SplitToolset : Toolset
        {
            public SplitToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string SplitToolsetSecond =
        """
        using Typeshift;

        namespace Demo;

        internal partial class SplitToolset
        {
            public SplitToolset(int times) => AddTool(Echo).WithDescription("Echoes again.");
        }
        """;

    public const string TwoToolsetsHost =
        """
        using Microsoft.Extensions.DependencyInjection;
        using Typeshift;

        namespace Host;

        internal partial class HostAgent : Agent
        {
            public HostAgent()
            {
                WithDescription("Hosts.");
                UseToolset<First.FirstTools>();
                UseToolset<Second.SecondTools>();
            }
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    // Names the reference's toolset in full, with no using for its namespace
    public const string CompanyAgentNamingTheToolsetInFull =
        """
        using Typeshift;

        namespace Research;

        internal partial class ResearchAgent : Agent
        {
            public ResearchAgent()
            {
                WithDescription("Researches companies.");
                UseToolset<Companies.CompanyToolset>().Include(t => t.FindCompanies);
            }
        }
        """;
}
