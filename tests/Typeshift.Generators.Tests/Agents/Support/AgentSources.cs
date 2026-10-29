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

    // An agent whose model and summary model are configurable, in a build of the given provider id type
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

        public sealed class Clients : IChatClientProvider
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
}
