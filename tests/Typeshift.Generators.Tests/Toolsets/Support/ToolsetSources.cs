namespace Typeshift.Generators.Tests.Toolsets.Support;

public static class ToolsetSources
{
    public const string Tickets =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.AI;
        using Typeshift;

        namespace Demo;

        /// <summary>
        /// The settings of the ticket search.
        /// </summary>
        public record SearchTicketsSettings
        {
            /// <summary>
            /// The most tickets to return.
            /// </summary>
            public int Limit { get; init; } = 10;

            /// <summary>
            /// Whether closed tickets are found too.
            /// </summary>
            public bool IncludeClosed { get; init; }

            /// <summary>
            /// How long the search may take.
            /// </summary>
            public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// One ticket.
        /// </summary>
        public record Ticket(int Number, string Title);

        /// <summary>
        /// The customer's support tickets.
        /// </summary>
        public partial class TicketToolset : Toolset
        {
            private const string CloseDescription = "Closes a ticket for good.";

            /// <summary>
            /// Declares the tools.
            /// </summary>
            public TicketToolset()
            {
                WithDisplayName("Tickets");

                AddTool(SearchTicketsAsync)
                    .ReadOnly()
                    .WithSettings<SearchTicketsSettings>()
                    .Configurable(s => s.Limit)
                    .Configurable(s => s.IncludeClosed);

                AddTool(CloseTicket)
                    .WithDescription(CloseDescription)
                    .RequiresApproval();

                AddTool(Forget)
                    .WithName("forget_ticket");
            }

            /// <summary>
            /// Searches the customer's tickets, newest first.
            /// </summary>
            /// <param name="query">Words the ticket's title contains.</param>
            /// <param name="author">Only tickets of this author, or null for all.</param>
            public static Task<IReadOnlyList<Ticket>> SearchTicketsAsync(
                string query,
                string? author,
                SearchTicketsSettings settings,
                IServiceProvider services,
                CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<Ticket>>([]);

            /// <param name="number">The ticket's number.</param>
            /// <param name="reason">Why it is closed, as the customer will read it.</param>
            /// <param name="notify">Whether the customer is told.</param>
            public static void CloseTicket(int number, string reason, bool notify = true)
            {
            }

            /// <summary>
            /// Forgets what was read.
            /// </summary>
            public static ValueTask Forget(AIFunctionArguments arguments) => ValueTask.CompletedTask;
        }
        """;

    public const string DeniedSettings =
        """
        using Typeshift;

        namespace Demo;

        internal record SearchArticlesSettings
        {
            public string Language { get; init; } = "en";

            public int Limit { get; init; } = 5;

            public bool Fuzzy { get; init; }
        }

        internal partial class KnowledgeToolset : Toolset
        {
            public KnowledgeToolset()
            {
                AddTool(SearchArticles)
                    .WithConfigurableSettings<SearchArticlesSettings>()
                    .NotConfigurable(s => s.Limit);
            }

            /// <summary>
            /// Searches the help center.
            /// </summary>
            /// <param name="query">What to search for.</param>
            public static string[] SearchArticles(string query, SearchArticlesSettings settings) => [query];
        }
        """;

    // A default of each kind that needs care to come out as a literal of the parameter's own type
    public const string Defaults =
        """
        using System;
        using Typeshift;

        namespace Demo;

        internal enum Level
        {
            Low,
            High
        }

        internal partial class GreetingToolset : Toolset
        {
            public GreetingToolset() => AddTool(Greet).WithDescription("Greets.");

            public static string Greet(
                string greeting = "say \"hi\"",
                bool loud = true,
                char mark = '!',
                byte times = 2,
                short pause = -1,
                long budget = 9_000_000_000,
                float ratio = 0.5f,
                double nan = double.NaN,
                decimal price = 1.25m,
                Level level = Level.High,
                Level unnamed = (Level)7,
                DateTime when = default,
                int? optional = null,
                string? missing = null) =>
                greeting;
        }
        """;

    public const string Nested =
        """
        using Typeshift;

        namespace Demo;

        internal partial class Outer
        {
            internal partial class Tickets : Toolset
            {
                public Tickets() => AddTool(Find).WithDescription("Finds.");

                public static string Find(string query) => query;
            }
        }

        internal partial class Outer_Tickets
        {
        }
        """;

    public const string KeywordNames =
        """
        using Typeshift;

        namespace @class.@event;

        internal record @record
        {
            public int @int { get; init; }
        }

        internal partial class @object : Toolset
        {
            public @object() =>
                AddTool(@params).WithDescription("Runs.").WithSettings<@record>().Configurable(s => s.@int);

            public static string @params(string @string, @record settings) => @string;
        }
        """;

    public const string ReservedNames =
        """
        using Typeshift;

        namespace Demo;

        internal record EqualsSettings
        {
            public int ApplyTo { get; init; }

            public int EqualsOverrides { get; init; }
        }

        internal partial class ReservedToolset : Toolset
        {
            public ReservedToolset()
            {
                AddTool(Compare)
                    .WithName("equals")
                    .WithDescription("Compares.")
                    .WithSettings<EqualsSettings>()
                    .Configurable(s => s.ApplyTo)
                    .Configurable(s => s.EqualsOverrides);
            }

            public static bool Compare(string left, string right, EqualsSettings settings) => left == right;
        }
        """;

    public const string ObsoleteSetting =
        """
        using System;
        using Typeshift;

        namespace Demo;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;

            [Obsolete("Use Repeat.")]
            public int Times { get; init; }
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }
        """;

    public const string MultiLineDescription =
        """
        using Typeshift;

        namespace Demo;

        internal partial class NoteToolset : Toolset
        {
            public NoteToolset() => AddTool(Note);

            /// <summary>
            /// Takes a note that spans
            /// several lines, with <c>code</c> &amp; "quotes".
            /// </summary>
            /// <param name="text">The note's text,
            /// on two lines.</param>
            public static string Note(string text) => text;
        }
        """;

    public const string DocumentedTwice =
        """
        using Typeshift;

        namespace Demo;

        internal partial class NoteToolset : Toolset
        {
            public NoteToolset() => AddTool(Note).WithDescription("Takes a note.");

            /// <param name="text">The note's text.</param>
            /// <param name="text">A second description of the same parameter.</param>
            public static string Note(string text) => text;
        }
        """;

    public const string PrivateMethod =
        """
        using Typeshift;

        namespace Demo;

        internal partial class SecretToolset : Toolset
        {
            public SecretToolset() => AddTool(Reveal).WithDescription("Reveals.");

            private static string Reveal(string name) => name;
        }
        """;
}
