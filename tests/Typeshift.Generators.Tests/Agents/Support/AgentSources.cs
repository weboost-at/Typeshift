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
                    .WithModel("fast-model")
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
                public HiddenAgent() => WithChatOptions().WithModel("fast-model");
            }
        }
        """;
}
