namespace Typeshift.Generators.Tests.Diagnostics.Support;

// One mistake each, in an otherwise valid toolset or agent
public static class DiagnosticSources
{
    public const string NotPartial =
        """
        using Typeshift;

        internal class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string NotConstant =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            private static readonly string Prefix = "say_";

            public EchoToolset() => AddTool(Echo).WithName(Prefix + "echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string Lambda =
        """
        using System;
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool((Func<string, string>)(text => text)).WithDescription("Echoes.");
        }
        """;

    public const string RefParameter =
        """
        using Typeshift;

        internal partial class CounterToolset : Toolset
        {
            public CounterToolset() => AddTool(Bump).WithDescription("Bumps.");

            public static void Bump(ref int value) => value++;
        }
        """;

    public const string SettingsNotRecord =
        """
        using Typeshift;

        internal class EchoSettings
        {
            public int Repeat { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string SettingsLessAccessible =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; }
        }

        /// <summary>Echoes.</summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>Declares the tool.</summary>
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            internal static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string DuplicateName =
        """
        using Typeshift;

        internal partial class VersionToolset : Toolset
        {
            public VersionToolset()
            {
                AddTool(Get).WithName("get_v2").WithDescription("Gets.");
                AddTool(GetOther).WithName("get_v_2").WithDescription("Gets.");
            }

            public static string Get(string key) => key;

            public static string GetOther(string key) => key;
        }
        """;

    public const string DuplicateAcrossToolsets =
        """
        using Typeshift;

        internal partial class FirstToolset : Toolset
        {
            public FirstToolset() => AddTool(Find).WithDescription("Finds.");

            public static string Find(string query) => query;
        }

        internal partial class SecondToolset : Toolset
        {
            public SecondToolset() => AddTool(Find).WithDescription("Finds.");

            public static string Find(string query) => query;
        }
        """;

    public const string NotLowerSnakeCase =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("Echo").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string MissingDescription =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo);

            public static string Echo(string text) => text;
        }
        """;

    public const string UnreadSettings =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>();

            public static string Echo(string text) => text;
        }
        """;

    public const string SummaryOnly =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo);

            /// <summary>
            /// Echoes the text.
            /// </summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string ReadOnlyPick =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            public int Twice => Repeat * 2;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo)
                    .WithDescription("Echoes.")
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Twice);
            }

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string OutsideConstructor =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => Declare();

            public static string Echo(string text) => text;

            private void Declare() => AddTool(Echo).WithDescription("Echoes.");
        }
        """;

    public const string ConditionalTool =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            private static readonly bool Loud = true;

            public EchoToolset()
            {
                if (Loud) AddTool(Echo).WithDescription("Echoes.");
            }

            public static string Echo(string text) => text;
        }
        """;

    public const string GenericMethod =
        """
        using System;
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo<int>).WithDescription("Echoes.");

            public static T Echo<T>(T value) => value;
        }
        """;

    public const string ChainOnVariable =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                var tool = AddTool(Echo);
                tool.WithDescription("Echoes.");
            }

            /// <summary>Echoes.</summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string StoredChain =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                var tool = AddTool(Echo);
            }

            /// <summary>Echoes.</summary>
            public static string Echo(string text) => text;
        }
        """;

    public const string ComputedPick =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset()
            {
                AddTool(Echo)
                    .WithDescription("Echoes.")
                    .WithSettings<EchoSettings>()
                    .Configurable(s => s.Repeat + 1);
            }

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string PrivateNested =
        """
        using Typeshift;

        internal partial class Outer
        {
            private partial class EchoToolset : Toolset
            {
                public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

                public static string Echo(string text) => text;
            }
        }
        """;

    public const string Derived =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class LoudToolset : EchoToolset
        {
        }
        """;

    public const string Generic =
        """
        using Typeshift;

        internal partial class EchoToolset<T> : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string ClashingMember =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;

            public static void CreateEcho()
            {
            }
        }
        """;

    public const string AgentNotPartial =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    public const string ComputedAgentName =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            private static readonly string Prefix = "echo";

            public EchoAgent() => WithName(Prefix + "_agent");
        }
        """;

    public const string UnstorableOption =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => WithChatOptions().Configurable(o => o.Tools);
        }
        """;

    public const string DeclarationInHelper =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => Declare();

            private void Declare() => UseToolset<EchoToolset>();
        }
        """;

    public const string ConditionalDeclaration =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent(bool echo)
            {
                if (echo) UseToolset<EchoToolset>();
            }

            public EchoAgent()
                : this(true)
            {
            }
        }
        """;

    public const string UseOnVariable =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                var use = UseToolset<EchoToolset>();
                use.Include(t => t.Echo);
            }
        }
        """;

    public const string ComputedToolPick =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>().Include(t => Pick(t));

            private static ToolName Pick(ToolList<EchoToolset> tools) => tools.Echo;
        }
        """;

    public const string AgentWithoutParameterlessConstructor =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent(string model) => WithChatOptions().WithModel("openai", model);
        }
        """;

    public const string DerivedAgent =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal abstract class BaseAgent : Agent;

        internal partial class EchoAgent : BaseAgent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    public const string AgentClashingMember =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();

            public int Settings { get; init; }
        }
        """;

    public const string ToolsetUsedTwice =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                UseToolset<EchoToolset>();

                UseToolset<EchoToolset>();
            }
        }
        """;

    public const string PublicAgentOfInternalSettings =
        """
        #pragma warning disable CS1591

        using Typeshift;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Repeat);

            public static string Echo(string text, EchoSettings settings) => text;
        }

        public partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    // D77: WithModel makes the agent's providers of Demo.Vendor
    public const string PublicAgentOfInternalProviderIdType =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace Demo;

        internal enum Vendor
        {
            OpenAi,
            Anthropic
        }

        public partial class SupportAgent : Agent
        {
            public SupportAgent() =>
                WithChatOptions().WithModel(Vendor.OpenAi, "fast-model").Configurable(o => o.ModelId);
        }
        """;

    public const string UnstorablePick =
        """
        using System;
        using Typeshift;

        internal record EchoSettings
        {
            public Func<int> Pick { get; init; } = () => 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Pick);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string UnstorableDenyList =
        """
        using Typeshift;

        internal record Node
        {
            public Node? Next { get; init; }
        }

        internal record EchoSettings
        {
            public Node Chain { get; init; } = new();
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string SharedKind =
        """
        using Typeshift;

        namespace Support
        {
            internal partial class HelpAgent : Agent
            {
                public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
            }
        }

        namespace Billing
        {
            internal partial class HelpAgent : Agent
            {
                public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
            }
        }
        """;

    public const string SubAgentWithoutDescription =
        """
        using Typeshift;

        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }

        internal partial class LeadAgent : Agent
        {
            public LeadAgent() => UseAgent<HelpAgent>();
        }
        """;

    public const string AgentSummaryOnly =
        """
        using Typeshift;

        /// <summary>Answers questions about the help center.</summary>
        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }
        """;

    public const string SubAgentDescribedBySummary =
        """
        using Typeshift;

        /// <summary>Answers questions about the help center.</summary>
        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }

        internal partial class LeadAgent : Agent
        {
            public LeadAgent() => UseAgent<HelpAgent>();
        }
        """;

    public const string SubAgentNeedingApproval =
        """
        using Typeshift;

        internal partial class TicketToolset : Toolset
        {
            public TicketToolset() => AddTool(Close).WithDescription("Closes a ticket.").RequiresApproval();

            public static void Close(int number)
            {
            }
        }

        internal partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Helps.");

                UseToolset<TicketToolset>();
            }
        }

        internal partial class LeadAgent : Agent
        {
            public LeadAgent() => UseAgent<HelpAgent>();
        }
        """;

    public const string SubAgentWithOwnToolNeedingApproval =
        """
        using Typeshift;

        internal partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Helps.");

                AddTool(Close).WithDescription("Closes a ticket.").RequiresApproval();
            }

            private static void Close(int number)
            {
            }
        }

        internal partial class LeadAgent : Agent
        {
            public LeadAgent() => UseAgent<HelpAgent>();
        }
        """;

    public const string AgentToolNamedLikeAToolsets =
        """
        using Typeshift;

        internal partial class TicketToolset : Toolset
        {
            public TicketToolset() => AddTool(Close).WithDescription("Closes a ticket.");

            public static void Close(int number)
            {
            }
        }

        internal partial class DeskAgent : Agent
        {
            public DeskAgent() => AddTool(CloseDesk).WithName("close").WithDescription("Closes a desk.");

            private static void CloseDesk(int desk)
            {
            }
        }
        """;

    public const string AgentToolsOfOneNameWithoutHandoff =
        """
        using Typeshift;

        internal partial class OrderAgent : Agent
        {
            public OrderAgent() => AddTool(FindOrder).WithName("lookup").WithDescription("Finds an order.");

            private static string FindOrder(int number) => "";
        }

        internal partial class TicketAgent : Agent
        {
            public TicketAgent() => AddTool(FindTicket).WithName("lookup").WithDescription("Finds a ticket.");

            private static string FindTicket(int number) => "";
        }
        """;

    public const string AgentToolsOfOneNameAcrossHandoff =
        """
        using Typeshift;

        internal partial class OrderAgent : Agent
        {
            public OrderAgent()
            {
                AddTool(FindOrder).WithName("lookup").WithDescription("Finds an order.");

                UseHandoff<TicketAgent>();
            }

            private static string FindOrder(int number) => "";
        }

        internal partial class TicketAgent : Agent
        {
            public TicketAgent()
            {
                WithDescription("Answers about tickets.");

                AddTool(FindTicket).WithName("lookup").WithDescription("Finds a ticket.");
            }

            private static string FindTicket(int number) => "";
        }
        """;

    public const string AgentToolsOfOneNameAcrossSubAgent =
        """
        using Typeshift;

        internal partial class OrderAgent : Agent
        {
            public OrderAgent()
            {
                AddTool(FindOrder).WithName("lookup").WithDescription("Finds an order.");

                UseAgent<TicketAgent>();
            }

            private static string FindOrder(int number) => "";
        }

        internal partial class TicketAgent : Agent
        {
            public TicketAgent()
            {
                WithDescription("Answers about tickets.");

                AddTool(FindTicket).WithName("lookup").WithDescription("Finds a ticket.");
            }

            private static string FindTicket(int number) => "";
        }
        """;

    public const string AgentToolsOfOneNameInAHandoffCircle =
        """
        using Typeshift;

        internal partial class OrderAgent : Agent
        {
            public OrderAgent()
            {
                WithDescription("Answers about orders.");

                AddTool(FindOrder).WithName("lookup").WithDescription("Finds an order.");

                UseHandoff<TicketAgent>();
            }

            private static string FindOrder(int number) => "";
        }

        internal partial class TicketAgent : Agent
        {
            public TicketAgent()
            {
                WithDescription("Answers about tickets.");

                AddTool(FindTicket).WithName("lookup").WithDescription("Finds a ticket.");

                UseHandoff<OrderAgent>();
            }

            private static string FindTicket(int number) => "";
        }
        """;

    public const string AgentToolLikeItsToolsetsButForUnderscores =
        """
        using Typeshift;

        internal partial class VersionToolset : Toolset
        {
            public VersionToolset() => AddTool(GetV2).WithDescription("Gets the second version.");

            public static string GetV2() => "";
        }

        internal partial class VersionAgent : Agent
        {
            public VersionAgent()
            {
                UseToolset<VersionToolset>();

                AddTool(GetOwn).WithName("get_v_2").WithDescription("Gets this agent's second version.");
            }

            private static string GetOwn() => "";
        }
        """;

    public const string AgentToolLikeAToolsetsAtTheEndOfHandoffs =
        """
        using Typeshift;

        internal partial class TicketToolset : Toolset
        {
            public TicketToolset() => AddTool(Close).WithDescription("Closes a ticket.");

            public static void Close(int number)
            {
            }
        }

        internal partial class DeskAgent : Agent
        {
            public DeskAgent()
            {
                AddTool(CloseDesk).WithName("close").WithDescription("Closes a desk.");

                UseHandoff<RouterAgent>();
            }

            private static void CloseDesk(int desk)
            {
            }
        }

        internal partial class RouterAgent : Agent
        {
            public RouterAgent()
            {
                WithDescription("Routes the conversation.");

                UseHandoff<TicketAgent>();
            }
        }

        internal partial class TicketAgent : Agent
        {
            public TicketAgent()
            {
                WithDescription("Answers about tickets.");

                UseToolset<TicketToolset>();
            }
        }
        """;

    public const string AgentToolOfAnInstanceMethod =
        """
        using Typeshift;

        internal partial class DeskAgent : Agent
        {
            public DeskAgent() => AddTool(Close).WithDescription("Closes a desk.");

            private void Close(int desk)
            {
            }
        }
        """;

    public const string AgentCircle =
        """
        using Typeshift;

        internal partial class AskAgent : Agent
        {
            public AskAgent()
            {
                WithDescription("Asks.");

                UseAgent<TellAgent>();
            }
        }

        internal partial class TellAgent : Agent
        {
            public TellAgent()
            {
                WithDescription("Tells.");

                UseAgent<AskAgent>();
            }
        }
        """;

    public const string StreamWithoutJsonMetadata =
        """
        using System.Collections.Generic;
        using System.Text.Json.Serialization;
        using Typeshift;

        internal sealed record Company(string Name);

        internal partial class CompanyToolset : Toolset
        {
            public CompanyToolset()
            {
                AddTool(List).WithDescription("Lists companies.");
            }

            public static async IAsyncEnumerable<Company> List(string name)
            {
                yield return new(name);
            }
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSerializable(typeof(Company))]
        internal partial class CompanyJson() : JsonSerializerContext(null)
        {
            public static CompanyJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    public const string ToolTypeWithoutJsonMetadata =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        internal sealed record Company(string Name);

        internal partial class CompanyToolset : Toolset
        {
            public CompanyToolset()
            {
                AddTool(Find).WithDescription("Finds a company.");

                AddTool(Count).WithDescription("Counts companies.");
            }

            public static Company Find(string name) => new(name);

            public static int Count(System.Guid region) => 0;
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSerializable(typeof(Company))]
        internal partial class CompanyJson() : JsonSerializerContext(null)
        {
            public static CompanyJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    public const string RequiredStoredMember =
        """
        using Typeshift;

        internal sealed class Endpoint
        {
            public required string Url { get; init; }
        }

        internal record EchoSettings
        {
            public Endpoint? Primary { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Primary);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string ObjectPick =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public object? Payload { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Payload);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string MemberlessClassPick =
        """
        using Typeshift;

        internal sealed class Marker
        {
            public string Name => "marker";
        }

        internal record EchoSettings
        {
            public Marker? Marker { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Marker);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string ObjectListUnderADenyList =
        """
        using System.Collections.Generic;
        using Typeshift;

        internal record EchoSettings
        {
            public List<object>? Payloads { get; init; }

            public int Limit { get; init; } = 5;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string ObjectDictionaryUnderADenyList =
        """
        using System.Collections.Generic;
        using Typeshift;

        internal record EchoSettings
        {
            public Dictionary<string, object>? Metadata { get; init; }

            public int Limit { get; init; } = 5;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string NullableStringItemsPick =
        """
        using System.Collections.Generic;
        using Typeshift;

        internal record EchoSettings
        {
            public List<string?> Tags { get; init; } = [];
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Tags);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string NullableRecordValuesPick =
        """
        using System.Collections.Generic;
        using Typeshift;

        internal record Place
        {
            public string City { get; init; } = "";
        }

        internal record EchoSettings
        {
            public Dictionary<string, Place?> Places { get; init; } = [];
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Places);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    // What an editor writes for ///, before anyone fills it in
    public const string EmptySummary =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo);

            /// <summary></summary>
            /// <param name="text"></param>
            public static string Echo(string text) => text;
        }
        """;

    public const string EmptyDescription =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("");

            public static string Echo(string text) => text;
        }
        """;

    public const string BlankDescription =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("   ");

            public static string Echo(string text) => text;
        }
        """;

    public const string SubAgentWithAnEmptyDescription =
        """
        using Typeshift;

        internal partial class HelpAgent : Agent
        {
            public HelpAgent() => WithDescription("");
        }

        internal partial class DeskAgent : Agent
        {
            public DeskAgent()
            {
                WithDescription("Answers at the desk.");
                UseAgent<HelpAgent>();
            }
        }
        """;

    public const string PlainStructPick =
        """
        using Typeshift;

        internal struct GeoPoint
        {
            public double Latitude { get; set; }
        }

        internal record EchoSettings
        {
            public GeoPoint Home { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Home);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string PrivateJsonContext =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        internal sealed record Company(string Name);

        internal partial class CompanyToolset : Toolset
        {
            public CompanyToolset() => AddTool(Find).WithDescription("Finds a company.");

            public static Company Find(string name) => new(name);

            [JsonSerializable(typeof(Company))]
            private partial class Json() : JsonSerializerContext(null)
            {
                protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

                public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) =>
                    null;
            }
        }
        """;

    // System.Text.Json's generator writes a Default only for a partial context, so the module registers none here
    public const string JsonContextThatIsNotPartial =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        internal sealed record Company(string Name);

        internal partial class CompanyToolset : Toolset
        {
            public CompanyToolset() => AddTool(Find).WithDescription("Finds a company.");

            public static Company Find(string name) => new(name);
        }

        [JsonSerializable(typeof(Company))]
        internal class CompanyJson() : JsonSerializerContext(null)
        {
            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    public const string PickBeingTyped =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<EchoToolset>().Include();
        }
        """;

    public const string ToolsetUseBeingTyped =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<>();
        }
        """;

    public const string ToolsetUseUnresolvedGlobal =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<global::Missing>();
        }
        """;

    // A namespace several assemblies share places the missing type in none of them
    public const string ToolsetUseUnresolvedShared =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent() => UseToolset<System.Missing>();
        }
        """;

    public const string AgentUsesBeingTyped =
        """
        using Typeshift;

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                UseAgent<>();
                UseAgent<>();
            }
        }
        """;

    public const string SettingsBeingTyped =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<>();

            public static string Echo(string text) => text;
        }
        """;

    // The pick is written before its record is
    public const string SettingsPickOfAnUnresolvedType =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Limit);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string SettingOfAnUnresolvedType =
        """
        using Typeshift;

        internal record EchoSettings
        {
            public Missing Home { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() =>
                AddTool(Echo).WithDescription("Echoes.").WithSettings<EchoSettings>().Configurable(s => s.Home);

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string UnresolvedSignature =
        """
        using System.Collections.Generic;
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static Unknown1 Echo(Unknown2 text, List<Unknown3> more) => default;
        }
        """;

    public const string StoreBeingTyped =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<>();
        }
        """;

    public const string ChatClientProviderBeingTyped =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithChatClientProvider<>();
        }
        """;

    public const string NoStore =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class Clock;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<Clock>();
        }
        """;

    public const string OtherStoreIdType =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<InMemoryTypeshiftStore<int>>();
        }
        """;

    public const string ConversationStoreOfAnotherIdType =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class EntryStore : IConversationStore<int>
        {
            public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
                int conversation,
                CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
                int conversation,
                CancellationToken ct) =>
                throw new NotSupportedException();

            public Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct) =>
                throw new NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<EntryStore>();
        }
        """;

    public const string DecisionStoreAlone =
        """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class WaitingStore : IDecisionStore<int>
        {
            public Task<StoredDocument?> LoadDecisionsAsync(int conversation, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<long> SaveDecisionsAsync(
                int conversation,
                string json,
                long loadedVersion,
                CancellationToken ct) =>
                throw new NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<WaitingStore>();
        }
        """;

    public const string AgentSettingsStore =
        """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class AgentStore : IAgentSettingsStore
        {
            public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
                throw new NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<AgentStore>();
        }
        """;

    public const string AgentSettingsStoreWithSettingsOfAnotherIdType =
        """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class MixedStore : IAgentSettingsStore, ISettingsStore<int>
        {
            public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<long> SaveAsync(int id, string kind, string json, long loadedVersion, CancellationToken ct) =>
                throw new NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<MixedStore>();
        }
        """;

    public const string AgentSettingsStoreHeldAsItsInterface =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal static class Startup
        {
            public static void Register(IServiceCollection services, IAgentSettingsStore store) =>
                services.AddTypeshift().WithStore(store);
        }
        """;

    public const string SettingsStoreAlone =
        """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class SettingsStore : ISettingsStore<Guid>
        {
            public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
                throw new NotSupportedException();

            public Task<long> SaveAsync(
                Guid id,
                string kind,
                string json,
                long loadedVersion,
                CancellationToken ct) =>
                throw new NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<SettingsStore>();
        }
        """;

    // D77: the generated Settings, in a file of their own, cannot name an enum nested as private in the agent
    public const string PrivateProviderIdType =
        """
        using Typeshift;

        internal partial class SupportAgent : Agent
        {
            public SupportAgent() =>
                WithChatOptions().WithModel(Vendor.OpenAi, "fast-model").Configurable(o => o.ModelId);

            private enum Vendor
            {
                OpenAi
            }
        }
        """;

    // Nor an enum of the agent's file alone
    public const string FileProviderIdType =
        """
        using Typeshift;

        file enum Vendor
        {
            OpenAi
        }

        internal partial class SupportAgent : Agent
        {
            public SupportAgent() =>
                WithChatOptions().WithModel(Vendor.OpenAi, "fast-model").Configurable(o => o.ModelId);
        }
        """;

    // D77: new HelpAgent() runs the constructor it chains to, whose provider is a record
    public const string ChainedUnstorableProvider =
        """
        using Typeshift;

        public sealed record ProviderKey(string Name);

        public partial class HelpAgent : Agent
        {
            public HelpAgent()
                : this(new ProviderKey("openai"))
            {
            }

            private HelpAgent(ProviderKey key) =>
                WithChatOptions().WithModel(key, "fast-model").Configurable(o => o.ModelId);
        }
        """;

    // A constructor new HelpAgent() never runs names a record, which settings never see
    public const string UnstorableProviderOfAnotherConstructor =
        """
        using Typeshift;

        public sealed record ProviderKey(string Name);

        public partial class HelpAgent : Agent
        {
            public HelpAgent() =>
                WithChatOptions().WithModel("openai", "fast-model").Configurable(o => o.ModelId);

            public HelpAgent(string name) =>
                WithChatOptions().WithModel(new ProviderKey(name), "fast-model");
        }
        """;

    // A helper generic over the provider and the store, which its callers' type arguments make one
    public const string GenericRegistrationHelpers =
        """
        using Typeshift;

        internal static class Registration
        {
            public static TypeshiftBuilder WithClients<T>(TypeshiftBuilder builder)
                where T : class, IChatClientProvider<string> =>
                builder.WithChatClientProvider<T>();

            public static TypeshiftBuilder WithSettings<T>(TypeshiftBuilder builder)
                where T : class, ISettingsStore<int> =>
                builder.WithStore<T>();
        }
        """;

    // D77: settings store the agent's providers, which are records
    public const string UnstorableProvider =
        """
        using Typeshift;

        public sealed record ProviderKey(string Name);

        public partial class HelpAgent : Agent
        {
            public HelpAgent() =>
                WithChatOptions().WithModel(new ProviderKey("openai"), "fast-model").Configurable(o => o.ModelId);
        }
        """;

    // Settings store no provider, so a record serves
    public const string UnstoredProvider =
        """
        using Typeshift;

        public sealed record ProviderKey(string Name);

        public partial class HelpAgent : Agent
        {
            public HelpAgent() =>
                WithChatOptions().WithModel(new ProviderKey("openai"), "fast-model").Configurable(o => o.Temperature);
        }
        """;

    public const string NoChatClientProvider =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        public sealed class Clients;

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithChatClientProvider<Clients>();
        }
        """;

    // D77: an agent's providers may be ints, and a provider of them serves those agents
    public const string ChatClientProviderOfInts =
        """
        #pragma warning disable CS1591

        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.AI;
        using Microsoft.Extensions.DependencyInjection;

        public partial class HelpAgent : Agent
        {
            public HelpAgent() => WithChatOptions().WithModel(42, "fast-model").Configurable(o => o.ModelId);
        }

        public sealed class NumberedClients : IChatClientProvider<int>
        {
            public ValueTask<IChatClient> GetChatClientAsync(int provider, string model, CancellationToken ct) =>
                throw new System.NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithChatClientProvider<NumberedClients>();
        }
        """;

    // Only its own file can name a file-local type, and the generated code is in others
    public const string FileLocalToolset =
        """
        using Typeshift;

        namespace Demo;

        file partial class ScratchTools : Toolset
        {
            public ScratchTools() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string FileLocalAgent =
        """
        using Typeshift;

        namespace Demo;

        file partial class ScratchTools : Toolset
        {
            public ScratchTools() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        file partial class ScratchAgent : Agent
        {
            public ScratchAgent()
            {
                WithDescription("Scratch.");
                UseToolset<ScratchTools>();
            }
        }
        """;

    public const string ToolsetNestedInAFileLocalClass =
        """
        using Typeshift;

        namespace Demo;

        file static partial class Scratch
        {
            internal partial class EchoTools : Toolset
            {
                public EchoTools() => AddTool(Echo).WithDescription("Echoes.");

                public static string Echo(string text) => text;
            }
        }
        """;

    public const string FileLocalToolMethod =
        """
        using Typeshift;

        namespace Demo;

        internal partial class DictionaryTools : Toolset
        {
            public DictionaryTools() => AddTool(Lookups.LookUp).WithDescription("Looks a word up.");
        }

        file static class Lookups
        {
            public static string LookUp(string word) => word;
        }
        """;

    // The compiler reports the settings in the method's signature too, as CS9051
    public const string FileLocalSettings =
        """
        using Typeshift;

        namespace Demo;

        file record ScratchSettings
        {
            public int Limit { get; init; } = 3;
        }

        internal partial class ScratchTools : Toolset
        {
            public ScratchTools() => AddTool(Echo).WithDescription("Echoes.").WithSettings<ScratchSettings>();

            public static string Echo(string text, ScratchSettings settings) => text;
        }
        """;

    public const string SettingsOfAFileLocalTypeArgument =
        """
        using Typeshift;

        namespace Demo;

        internal record Paged<T>
        {
            public int Size { get; init; } = 20;
        }

        file record Scratch;

        internal partial class PageTools : Toolset
        {
            public PageTools() => AddTool(Page).WithDescription("Pages.").WithSettings<Paged<Scratch>>();

            public static string Page(string query) => query;
        }
        """;

    // The compiler reports the type in the method's signature, as CS9051; the analyzer adds nothing
    public const string FileLocalParameterType =
        """
        using Typeshift;

        namespace Demo;

        file record Scratch(string Text);

        internal partial class ScratchTools : Toolset
        {
            public ScratchTools() => AddTool(Take).WithDescription("Takes.");

            public static string Take(Scratch scratch) => scratch.Text;
        }
        """;

    public const string DynamicParameter =
        """
        using Typeshift;

        namespace Demo;

        internal partial class InspectTools : Toolset
        {
            public InspectTools() => AddTool(Inspect).WithDescription("Inspects.");

            public static string Inspect(dynamic value) => "";
        }
        """;

    // Compiled with unsafe code allowed
    public const string PointerArrayParameter =
        """
        using Typeshift;

        namespace Demo;

        internal unsafe partial class SumTools : Toolset
        {
            public SumTools() => AddTool(Sum).WithDescription("Sums.");

            public static string Sum(int*[] values) => "";
        }
        """;

    public const string RefStructResult =
        """
        using System;
        using Typeshift;

        namespace Demo;

        internal partial class CharTools : Toolset
        {
            public CharTools() => AddTool(Chars).WithDescription("Reads.");

            public static ReadOnlySpan<char> Chars(string value) => value;
        }
        """;

    // Compiled with unsafe code allowed
    public const string PointerResult =
        """
        using Typeshift;

        namespace Demo;

        internal unsafe partial class AddressTools : Toolset
        {
            public AddressTools() => AddTool(Address).WithDescription("Points.");

            public static int* Address(string value) => null;
        }
        """;

    public const string DynamicResult =
        """
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal partial class AnyTools : Toolset
        {
            public AnyTools() => AddTool(AnyAsync).WithDescription("Returns anything.");

            public static Task<dynamic> AnyAsync(string value) => Task.FromResult<dynamic>(value);
        }
        """;

    public const string NullableTaskResult =
        """
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal partial class MaybeTools : Toolset
        {
            public MaybeTools() => AddTool(MaybeAsync).WithDescription("Maybe.");

            public static Task<string>? MaybeAsync(string value) => null;
        }
        """;

    public const string NullableValueTaskResult =
        """
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal partial class MaybeTools : Toolset
        {
            public MaybeTools() => AddTool(MaybeAsync).WithDescription("Maybe.");

            public static ValueTask<string>? MaybeAsync(string value) => null;
        }
        """;

    public const string NullableStreamResult =
        """
        using System.Collections.Generic;
        using Typeshift;

        namespace Demo;

        internal partial class MaybeTools : Toolset
        {
            public MaybeTools() => AddTool(Maybe).WithDescription("Maybe.");

            public static IAsyncEnumerable<string>? Maybe(string value) => null;
        }
        """;

    // The compiler reports the member where AddTool names it, as CS8926; the analyzer adds nothing
    public const string StaticVirtualInterfaceMember =
        """
        using Typeshift;

        namespace Demo;

        internal interface IShout
        {
            static virtual string Shout(string text) => text.ToUpperInvariant();
        }

        internal partial class ShoutTools : Toolset
        {
            public ShoutTools() => AddTool(IShout.Shout).WithDescription("Shouts.");
        }
        """;

    public const string MemberOfAGenericExtensionBlock =
        """
        using System.Collections.Generic;
        using Typeshift;

        namespace Demo;

        internal static class ListExtensions
        {
            extension<T>(List<T>)
            {
                public static int Grow(int count) => count * 2;
            }
        }

        internal partial class ListTools : Toolset
        {
            public ListTools() => AddTool(List<int>.Grow).WithDescription("Sizes a list.");
        }
        """;

    public const string NameEndingInALineBreak =
        """
        using Typeshift;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithName("echo\n").WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    // create_report's function class and report_function's factory are both CreateReportFunction
    public const string ToolsSharingAGeneratedMember =
        """
        using Typeshift;

        namespace Demo;

        internal partial class ReportToolset : Toolset
        {
            public ReportToolset()
            {
                AddTool(CreateReport).WithDescription("Creates.");
                AddTool(ReportFunction).WithDescription("Reports.");
            }

            public static string CreateReport(string text) => text;

            public static string ReportFunction(string text) => text;
        }
        """;

    // Both are EqualsTool on the tool list: the first gets the suffix, since the list has an Equals from object
    public const string ToolsSharingAToolListProperty =
        """
        using Typeshift;

        namespace Demo;

        internal partial class CompareToolset : Toolset
        {
            public CompareToolset()
            {
                AddTool(Compare).WithName("equals").WithDescription("Compares.");
                AddTool(Check).WithName("equals_tool").WithDescription("Checks.");
            }

            public static bool Compare(string left, string right) => left == right;

            public static bool Check(string left, string right) => left == right;
        }
        """;

    // A class cannot hold a member of its own name
    public const string AgentNamedLikeAGeneratedMember =
        """
        using Typeshift;

        namespace Demo;

        internal partial class Settings : Agent
        {
            public Settings() => WithDescription("Settings.");
        }
        """;

    public const string ToolsetNamedLikeAToolsFunction =
        """
        using Typeshift;

        namespace Demo;

        internal partial class EchoFunction : Toolset
        {
            public EchoFunction() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    public const string ToolsetNamedLikeAToolsFactory =
        """
        using Typeshift;

        namespace Demo;

        internal partial class CreateTicket : Toolset
        {
            public CreateTicket() => AddTool(Ticket).WithDescription("Creates.");

            public static string Ticket(string text) => text;
        }
        """;

    // A generic class named without its type argument, as while the argument is typed: the method's class is
    // Finder<T>, with the type parameter where an argument belongs
    public const string ToolOfAGenericClassWithoutItsTypeArgument =
        """
        using Typeshift;

        namespace Demo;

        internal static class Finder<T>
        {
            public static string Find(string text) => text;
        }

        internal partial class FindToolset : Toolset
        {
            public FindToolset() => AddTool(Finder<>.Find);
        }
        """;

    // An instance property where a static method belongs, which the compiler rejects (CS0120)
    public const string ToolArgumentThatDoesNotBind =
        """
        using System.Collections.Generic;
        using Typeshift;

        namespace Demo;

        internal partial class CountToolset : Toolset
        {
            public CountToolset() => AddTool(List<int>.Capacity).WithDescription("Counts.");
        }
        """;

    public const string ToolArgumentThatDoesNotExist =
        """
        using Typeshift;

        namespace Demo;

        internal partial class CountToolset : Toolset
        {
            public CountToolset() => AddTool(Missing).WithName("count").WithDescription("Counts.");
        }
        """;

    // The generator finds a class by the name in its base list (D3), which an alias hides
    public const string AliasedToolsetBase =
        """
        using Typeshift;
        using ToolBase = Typeshift.Toolset;

        namespace Demo;

        internal partial class EchoToolset : ToolBase
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    // An app with an Agent class of its own names Typeshift's through an alias, here one for the whole project
    public const string AliasedAgentBase =
        """
        using Typeshift;

        namespace Demo;

        internal partial class DeskAgent : AgentBase
        {
            public DeskAgent() => WithDescription("Answers at the desk.");
        }
        """;

    public const string GlobalAgentAlias =
        """
        global using AgentBase = Typeshift.Agent;
        """;

    public const string AliasedBaseBesideAnInterface =
        """
        using System;
        using Typeshift;
        using ToolBase = Typeshift.Toolset;

        namespace Demo;

        internal partial class EchoToolset : IDisposable
        {
            public void Dispose()
            {
            }
        }

        internal partial class EchoToolset : ToolBase
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }
        """;

    // The generator finds a context by the name in its base list too, so the module registers none here
    public const string JsonContextNamedThroughAnAlias =
        """
        using System.Text.Json.Serialization;
        using Typeshift;
        using ContextBase = System.Text.Json.Serialization.JsonSerializerContext;

        internal sealed record Company(string Name);

        internal partial class CompanyToolset : Toolset
        {
            public CompanyToolset() => AddTool(Find).WithDescription("Finds a company.");

            public static Company Find(string name) => new(name);
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSerializable(typeof(Company))]
        internal partial class CompanyJson() : ContextBase(null)
        {
            public static CompanyJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    // No toolset, no agent and no AddTypeshift(): nothing the generator writes code for
    public const string NothingOfTypeshift =
        """
        namespace Demo;

        internal record Address(string Street, string City);
        """;
}
