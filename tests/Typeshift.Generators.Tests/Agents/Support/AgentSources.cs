namespace Typeshift.Generators.Tests.Agents.Support;

public static class AgentSources
{
    public const string Tickets =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace Demo;

        public record SearchTicketsSettings
        {
            public int Limit { get; init; } = 10;

            public bool IncludeClosed { get; init; }
        }

        public partial class TicketToolset : Toolset
        {
            public TicketToolset()
            {
                AddTool(SearchTickets)
                    .WithDescription("Searches tickets.")
                    .WithSettings<SearchTicketsSettings>()
                    .Configurable(s => s.Limit);

                AddTool(CloseTicket).WithDescription("Closes a ticket.");

                AddTool(DeleteTicket).WithDescription("Deletes a ticket.");
            }

            public static string SearchTickets(string query, SearchTicketsSettings settings) => query;

            public static void CloseTicket(int number)
            {
            }

            public static void DeleteTicket(int number)
            {
            }
        }
        """;

    // An agent whose model and summary model are configurable, of the provider id type its WithModel names (D77)
    public static string Routed(string providerIdType, string provider) =>
        $$"""
        #pragma warning disable CS1591

        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.AI;
        using Microsoft.Extensions.DependencyInjection;

        namespace Demo;

        public enum Provider
        {
            OpenAI,
            LmStudio
        }

        public partial class RoutedAgent : Agent
        {
            public RoutedAgent()
            {
                WithChatOptions()
                    .WithModel({{provider}}, "gpt-5-mini")
                    .Configurable(o => o.ModelId);

                WithRunOptions()
                    .WithSummaryModel("gpt-5-nano")
                    .Configurable(r => r.SummaryModel);
            }
        }

        public sealed class Clients : IChatClientProvider<{{providerIdType}}>
        {
            public ValueTask<IChatClient> GetChatClientAsync(
                {{providerIdType}} provider,
                string model,
                CancellationToken ct) =>
                throw new System.NotSupportedException();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithChatClientProvider<Clients>();
        }
        """;

    // D77: one agent's providers are an enum, the other's strings, and each agent's model is configurable without a
    // WithModel, which makes its providers strings
    public const string TwoProviderIdTypes =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace Demo;

        public enum Vendor
        {
            OpenAi,
            Anthropic
        }

        public partial class VendorAgent : Agent
        {
            public VendorAgent() =>
                WithChatOptions().WithModel(Vendor.OpenAi, "fast-model").Configurable(o => o.ModelId);
        }

        public partial class NamedAgent : Agent
        {
            public NamedAgent() =>
                WithChatOptions().WithModel("openai", "fast-model").Configurable(o => o.ModelId);
        }

        public partial class PickedAgent : Agent
        {
            public PickedAgent() => WithChatOptions().Configurable(o => o.ModelId);
        }
        """;

    // D77: new SplitAgent() runs the enum's constructor, and new OverridingAgent() its own WithModel after it
    public const string ProvidersOfSeveralConstructors =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace Demo;

        public enum Vendor
        {
            OpenAi
        }

        public partial class SplitAgent : Agent
        {
            public SplitAgent()
                : this(Vendor.OpenAi)
            {
            }

            public SplitAgent(Vendor vendor) =>
                WithChatOptions().WithModel(vendor, "fast-model").Configurable(o => o.ModelId);

            public SplitAgent(string provider) => WithChatOptions().WithModel(provider, "fast-model");
        }

        public partial class OverridingAgent : Agent
        {
            public OverridingAgent()
                : this(Vendor.OpenAi) =>
                WithChatOptions().WithModel("openai", "fast-model").Configurable(o => o.ModelId);

            public OverridingAgent(Vendor vendor) => WithChatOptions().WithModel(vendor, "fast-model");
        }
        """;

    // Names the agent's type in full, without importing its namespace
    public const string CallerOutsideTheAgentsNamespace =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace App;

        public static class Caller
        {
            public static System.Threading.Tasks.Task<Microsoft.Extensions.AI.ChatResponse> AskAsync(
                AgentHost<Demo.SupportAgent> host) =>
                host.RunAsync("Where is my refund?");
        }
        """;

    // Names AgentHost in full from the agent's own namespace, without importing Typeshift
    public const string CallerInTheAgentsNamespace =
        """
        #pragma warning disable CS1591

        namespace Demo;

        public static class Caller
        {
            public static System.Threading.Tasks.Task<Microsoft.Extensions.AI.ChatResponse> AskAsync(
                global::Typeshift.AgentHost<SupportAgent> host) =>
                host.RunAsync("Where is my refund?");
        }
        """;

    // A nested type whose hint name could be the host members' file
    public const string NestedHost =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace Demo;

        public partial class SupportAgent : Agent
        {
            public SupportAgent() => WithChatOptions().WithModel("openai", "fast-model");

            public partial class Host : Toolset
            {
                public Host() => AddTool(Ping).WithDescription("Answers pong.");

                public static string Ping() => "pong";
            }
        }
        """;

    // Two agents whose namespaces joined with an underscore would read the same
    public const string UnderscoredNamespaces =
        """
        #pragma warning disable CS1591

        using Typeshift;

        namespace A.B_C
        {
            public partial class XAgent : Agent
            {
                public XAgent() => WithStorageKind("first");
            }
        }

        namespace A_B.C
        {
            public partial class XAgent : Agent
            {
                public XAgent() => WithStorageKind("second");
            }
        }
        """;

    public const string NegativeSchemaVersion =
        """
        using Typeshift;

        namespace Demo;

        internal partial class VersionedAgent : Agent
        {
            public VersionedAgent()
            {
                WithDescription("Has a version.");
                WithSchemaVersion(-2);
            }
        }
        """;

    public const string Support =
        """
        #pragma warning disable CS1591

        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        namespace Demo;

        public partial class SupportAgent : Agent
        {
            public SupportAgent()
            {
                WithDescription("Answers customers.");

                WithChatOptions()
                    .WithModel("openai", "fast-model")
                    .WithTemperature(0.3f)
                    .Configurable(o => o.ModelId)
                    .Configurable(o => o.Temperature)
                    .Configurable(o => o.StopSequences)
                    .Configurable(o => o.Reasoning);

                WithRunOptions()
                    .WithMaxToolIterations(10)
                    .Configurable(r => r.MaxToolIterations);

                UseToolset<TicketToolset>().Exclude(t => t.DeleteTicket);

                ConfigureSearchTickets()
                    .WithLimit(5);
            }

            protected override string GetInstructions() => "Answer.";
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    // Tools of the agent's own next to a toolset's: one that reads, with settings, and one that asks first
    public const string OwnTools =
        """
        #pragma warning disable CS1591

        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        namespace Demo;

        public record LookUpSettings
        {
            public int Limit { get; init; } = 3;
        }

        public partial class DeskAgent : Agent
        {
            public DeskAgent()
            {
                WithChatOptions()
                    .WithModel("openai", "fast-model");

                UseToolset<TicketToolset>().Include(t => t.CloseTicket);

                AddTool(LookUpCustomer)
                    .WithDescription("Looks a customer up.")
                    .ReadOnly()
                    .WithSettings<LookUpSettings>()
                    .Configurable(s => s.Limit);

                AddTool(RefundOrder)
                    .WithDescription("Refunds an order.")
                    .RequiresApproval();

                ConfigureLookUpCustomer()
                    .WithLimit(5);
            }

            /// <param name="email">The customer's email address.</param>
            private static string LookUpCustomer(string email, LookUpSettings settings) => email;

            private static void RefundOrder(int order)
            {
            }
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) => services.AddTypeshift();
        }
        """;

    // A tool named like a configurable option, a Configure method of the user's own, an agent nested in a class
    public const string Clashes =
        """
        using Typeshift;

        namespace Demo;

        internal record TemperatureSettings
        {
            public float Value { get; init; }
        }

        internal partial class WeatherToolset : Toolset
        {
            public WeatherToolset() =>
                AddTool(Temperature)
                    .WithDescription("Reads the temperature.")
                    .WithSettings<TemperatureSettings>()
                    .Configurable(s => s.Value);

            public static float Temperature(TemperatureSettings settings) => settings.Value;
        }

        internal static partial class Outer
        {
            internal partial class WeatherAgent : Agent
            {
                public WeatherAgent()
                {
                    WithChatOptions().Configurable(o => o.Temperature);

                    UseToolset<WeatherToolset>();

                    ConfigureTemperature();
                }

                private void ConfigureTemperature()
                {
                }
            }
        }
        """;

    public const string Private =
        """
        using Typeshift;

        namespace Demo;

        internal partial class Outer
        {
            private partial class HiddenAgent : Agent
            {
                public HiddenAgent() => WithChatOptions().WithModel("openai", "fast-model");
            }
        }
        """;

    // D75: an app without a container of its own
    public const string Runtime =
        """
        using Typeshift;
        using Microsoft.Extensions.AI;

        namespace Demo;

        internal static class Program
        {
            public static TypeshiftRuntime Start(IChatClient client) =>
                TypeshiftRuntime.Create(client, typeshift => typeshift.WithStore<InMemoryTypeshiftStore<int>>());
        }
        """;

    // D75: an app whose only call is TypeshiftRuntime.Create, through the given receiver, with no AddTypeshift()
    public static string RuntimeOnly(string receiver) =>
        $$"""
        #pragma warning disable CS1591

        using Typeshift;
        using Microsoft.Extensions.AI;

        namespace Demo;

        public partial class SupportAgent : Agent
        {
            public SupportAgent() => WithChatOptions().WithModel("openai", "fast-model");
        }

        internal static class Program
        {
            public static TypeshiftRuntime Start(IChatClient client) => {{receiver}}.Create(client);
        }
        """;

    // A Create method of the project's own, which the generator leaves alone
    public const string OwnCreate =
        """
        using Microsoft.Extensions.AI;

        namespace Demo;

        public static class Factory
        {
            public static IChatClient Create(IChatClient client) => client;
        }

        internal static class Program
        {
            public static IChatClient Start(IChatClient client) => Factory.Create(client);
        }
        """;

    // D75: a call bound at run time, which the library's body answers
    public const string DynamicRuntime =
        """
        using Typeshift;

        namespace Demo;

        internal static class Program
        {
            public static TypeshiftRuntime Start(dynamic client) => TypeshiftRuntime.Create(client);
        }
        """;
}
