namespace Typeshift.Generators.Tests.Diagnostics.Support;

// One shape each that the parsers accept without a diagnostic: the code generated for it compiles. A shape the
// parser newly accepts gets a field here, and one it rejects gets a field in DiagnosticSources
public static class ShapeSources
{
    public const string EveryParameterKind =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using Microsoft.Extensions.AI;
        using Typeshift;

        namespace Demo;

        internal enum Priority
        {
            Low,
            High
        }

        internal record Address(string Street, string City);

        internal readonly record struct Cell(int Row, int Column);

        internal record FilingSettings
        {
            public int Limit { get; init; } = 10;
        }

        internal partial class FilingToolset : Toolset
        {
            public FilingToolset() => AddTool(File).WithDescription("Files a letter.").WithSettings<FilingSettings>();

            public static string File(
                string text,
                int count,
                double? ratio,
                Priority priority,
                Priority? fallback,
                Guid id,
                DateTimeOffset due,
                string[] tags,
                List<int> numbers,
                IReadOnlyDictionary<string, string> labels,
                Address address,
                Address? previous,
                Cell cell,
                (int Row, int Column) corner,
                in int offset,
                FilingSettings settings,
                IServiceProvider services,
                AIFunctionArguments arguments,
                CancellationToken ct,
                bool urgent = false,
                params string[] recipients) =>
                text;
        }
        """;

    public const string EveryReturnKind =
        """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal record Reading(string Sensor, double Value);

        internal partial class SensorToolset : Toolset
        {
            public SensorToolset()
            {
                AddTool(Reset).WithDescription("Resets the sensors.");
                AddTool(ResetAsync).WithName("reset_later").WithDescription("Resets the sensors later.");
                AddTool(Flush).WithDescription("Flushes the readings.");
                AddTool(Read).WithDescription("Reads a sensor.");
                AddTool(ReadAsync).WithName("read_later").WithDescription("Reads a sensor later.");
                AddTool(Peek).WithDescription("Reads a sensor without waiting.");
                AddTool(Find).WithDescription("Finds a sensor's last reading.");
                AddTool(Count).WithDescription("Counts the sensors, if any answer.");
                AddTool(List).WithDescription("Lists the readings.");
                AddTool(Group).WithDescription("Groups the readings by sensor.");
            }

            public static void Reset()
            {
            }

            public static Task ResetAsync() => Task.CompletedTask;

            public static ValueTask Flush() => ValueTask.CompletedTask;

            public static Reading Read(string sensor) => new(sensor, 0);

            public static Task<Reading> ReadAsync(string sensor) => Task.FromResult(new Reading(sensor, 0));

            public static ValueTask<Reading> Peek(string sensor) => ValueTask.FromResult(new Reading(sensor, 0));

            public static Reading? Find(string sensor) => null;

            public static int? Count() => null;

            public static Reading[] List() => [];

            public static Task<Dictionary<string, List<Reading>>> Group() =>
                Task.FromResult(new Dictionary<string, List<Reading>>());
        }
        """;

    public const string StreamTool =
        """
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal partial class WordToolset : Toolset
        {
            public WordToolset()
            {
                AddTool(Split).WithDescription("Splits a text into its words.");
                AddTool(Spell).WithDescription("Spells a word.");
            }

            public static async IAsyncEnumerable<string> Split(
                string text,
                [EnumeratorCancellation] CancellationToken ct)
            {
                foreach (var word in text.Split(' '))
                {
                    await Task.Yield();
                    ct.ThrowIfCancellationRequested();
                    yield return word;
                }
            }

            public static async IAsyncEnumerable<char> Spell(string word)
            {
                foreach (var letter in word)
                {
                    await Task.Yield();
                    yield return letter;
                }
            }
        }
        """;

    public const string PublicInTheGlobalNamespace =
        """
        using Typeshift;

        /// <summary>
        /// Echoes what it is given.
        /// </summary>
        public partial class EchoToolset : Toolset
        {
            /// <summary>
            /// Declares the tools.
            /// </summary>
            public EchoToolset() => AddTool(Echo);

            /// <summary>
            /// Echoes a text.
            /// </summary>
            /// <param name="text">The text to echo.</param>
            /// <returns>The text.</returns>
            public static string Echo(string text) => text;
        }

        /// <summary>
        /// Echoes for a user.
        /// </summary>
        public partial class EchoAgent : Agent
        {
            /// <summary>
            /// Declares the agent.
            /// </summary>
            public EchoAgent() => UseToolset<EchoToolset>();
        }
        """;

    public const string NestedNamespacesAndClasses =
        """
        using Typeshift;

        namespace Demo
        {
            namespace Tools
            {
                internal static partial class Outer
                {
                    internal partial class Inner
                    {
                        internal record Limits
                        {
                            public int Most { get; init; } = 3;
                        }

                        internal partial class EchoToolset : Toolset
                        {
                            public EchoToolset() =>
                                AddTool(Echo)
                                    .WithDescription("Echoes.")
                                    .WithSettings<Limits>()
                                    .Configurable(s => s.Most);

                            public static string Echo(string text, Limits limits) => text;
                        }
                    }
                }
            }

            namespace Agents
            {
                using Demo.Tools;

                internal partial class EchoAgent : Agent
                {
                    public EchoAgent()
                    {
                        WithDescription("Echoes.");
                        UseToolset<Outer.Inner.EchoToolset>().Include(t => t.Echo);
                    }
                }
            }
        }
        """;

    public const string ToolOfAnotherClass =
        """
        using Typeshift;

        namespace Demo;

        internal static class Texts
        {
            public static string Upper(string text) => text.ToUpperInvariant();

            internal static class Counting
            {
                public static int Length(string text) => text.Length;
            }
        }

        internal partial class TextToolset : Toolset
        {
            public TextToolset()
            {
                AddTool(Texts.Upper).WithDescription("Writes a text in capitals.");
                AddTool(Texts.Counting.Length).WithDescription("Counts a text's characters.");
                AddTool(Demo.TextToolset.Lower).WithDescription("Writes a text in small letters.");
            }

            private static string Lower(string text) => text.ToLowerInvariant();
        }
        """;

    public const string EveryAgentDeclaration =
        """
        using Typeshift;

        namespace Demo;

        internal record Verdict(bool Approved, string Reason);

        internal record ArchiveSettings
        {
            public int Days { get; init; } = 30;
        }

        internal partial class MailToolset : Toolset
        {
            public MailToolset()
            {
                AddTool(Send).WithDescription("Sends a mail.").RequiresApproval();
                AddTool(Read).WithDescription("Reads a mail.").ReadOnly();
                AddTool(Delete).WithDescription("Deletes a mail.");
            }

            public static string Send(string to, string text) => to;

            public static string Read(int number) => "";

            public static void Delete(int number)
            {
            }
        }

        internal partial class CalendarToolset : Toolset
        {
            public CalendarToolset() => AddTool(Book).WithDescription("Books a meeting.");

            public static string Book(string title) => title;
        }

        internal partial class ReviewAgent : Agent
        {
            public ReviewAgent() => WithDescription("Reviews a draft.");
        }

        internal partial class BillingAgent : Agent
        {
            public BillingAgent() => WithDescription("Answers questions about invoices.");
        }

        internal partial class OfficeAgent : Agent
        {
            private const string Kind = "office";

            public OfficeAgent()
            {
                WithName("office_help");
                WithDescription("Helps around the office.");
                WithInstructions("Help.");
                WithStorageKind(Kind);
                WithSchemaVersion(3);

                WithChatOptions()
                    .WithModel("openai", "fast-model")
                    .WithTemperature(0.2f)
                    .WithMaxOutputTokens(500)
                    .Configurable(o => o.ModelId)
                    .Configurable(o => o.Temperature)
                    .Configurable(o => o.MaxOutputTokens)
                    .Configurable(o => o.TopP)
                    .Configurable(o => o.TopK)
                    .Configurable(o => o.FrequencyPenalty)
                    .Configurable(o => o.PresencePenalty)
                    .Configurable(o => o.Seed)
                    .Configurable(o => o.StopSequences)
                    .Configurable(o => o.Reasoning);

                WithRunOptions()
                    .WithMaxToolIterations(5)
                    .Configurable(r => r.MaxToolIterations)
                    .Configurable(r => r.MaxConsecutiveToolErrors)
                    .Configurable(r => r.MaxInputTokens)
                    .Configurable(r => r.TargetInputTokens);

                UseToolset<MailToolset>().Exclude(t => t.Delete);
                UseToolset<CalendarToolset>().Include(t => t.Book);
                UseAgent<ReviewAgent>().WithResult<Verdict>();
                UseHandoff<BillingAgent>();

                AddTool(Archive)
                    .WithDescription("Archives old mails.")
                    .WithConfigurableSettings<ArchiveSettings>();
            }

            private static int Archive(ArchiveSettings settings) => settings.Days;
        }
        """;

    public const string DescriptionOfMarkup =
        """
        using Typeshift;

        namespace Demo;

        /// <summary>
        /// What <see cref="MarkupToolset.Strip"/> keeps.
        /// </summary>
        public record MarkupSettings
        {
            /// <summary>
            /// Whether <c>&amp;</c> and "quotes" are kept, as in {braces}.
            /// </summary>
            public bool Keep { get; init; }
        }

        /// <summary>
        /// Tools whose descriptions hold markup.
        /// </summary>
        public partial class MarkupToolset : Toolset
        {
            /// <summary>
            /// Declares the tools.
            /// </summary>
            public MarkupToolset() =>
                AddTool(Strip)
                    .WithDescription("Strips <b>tags</b> & \"quotes\", {braces}, a \\ and a */ from a text.")
                    .WithConfigurableSettings<MarkupSettings>();

            /// <param name="text">The text, with <i>tags</i> &amp; more.</param>
            public static string Strip(string text, MarkupSettings settings) => text;
        }
        """;

    public const string ParametersNamedLikeGeneratedLocals =
        """
        using Typeshift;

        namespace Demo;

        internal record RouteSettings
        {
            public int Hops { get; init; } = 3;
        }

        internal partial class RouteToolset : Toolset
        {
            public RouteToolset() =>
                AddTool(Route).WithDescription("Routes.").WithSettings<RouteSettings>().Configurable(s => s.Hops);

            public static string Route(
                string arguments,
                string services,
                string ct,
                string result,
                string value,
                string function,
                string context,
                string toolset,
                string overrides,
                string name,
                RouteSettings settings) =>
                arguments;
        }
        """;

    public const string SameNamesInTwoNamespaces =
        """
        using Typeshift;

        namespace Demo.Billing
        {
            internal partial class LookupToolset : Toolset
            {
                public LookupToolset() => AddTool(FindInvoice).WithDescription("Finds an invoice.");

                public static string FindInvoice(int number) => "";
            }
        }

        namespace Demo.Shipping
        {
            internal partial class LookupToolset : Toolset
            {
                public LookupToolset() => AddTool(FindParcel).WithDescription("Finds a parcel.");

                public static string FindParcel(int number) => "";
            }
        }

        namespace Demo
        {
            using Demo.Billing;
            using Demo.Shipping;

            internal partial class DeskAgent : Agent
            {
                public DeskAgent()
                {
                    WithDescription("Finds invoices and parcels.");
                    UseToolset<Billing.LookupToolset>().Include(t => t.FindInvoice);
                    UseToolset<Shipping.LookupToolset>().Include(t => t.FindParcel);
                }
            }
        }
        """;

    // The compiler tells the classes apart by case; a generator's hint names are compared without it
    public const string NamesThatDifferByCase =
        """
        using Typeshift;

        namespace Demo;

        internal partial class TicketTools : Toolset
        {
            public TicketTools() => AddTool(Open).WithDescription("Opens a ticket.");

            public static string Open(string title) => title;
        }

        internal partial class Tickettools : Toolset
        {
            public Tickettools() => AddTool(Close).WithDescription("Closes a ticket.");

            public static string Close(int number) => "";
        }

        internal partial class HelpAgent : Agent
        {
            public HelpAgent()
            {
                WithDescription("Opens tickets.");
                UseToolset<TicketTools>().Include(t => t.Open);
            }
        }

        internal partial class Helpagent : Agent
        {
            public Helpagent()
            {
                WithDescription("Closes tickets.");
                UseToolset<Tickettools>().Include(t => t.Close);
            }
        }
        """;

    public const string ConstructorWithOtherStatements =
        """
        using System.Collections.Generic;
        using Typeshift;

        namespace Demo;

        internal partial class ClockToolset : Toolset
        {
            private readonly List<string> _zones = [];

            public ClockToolset()
            {
                const string description = "Tells the time.";

                _zones.Add("utc");
                AddTool(Tell).WithDescription(description);
                _zones.Sort();
            }

            public static string Tell() => "now";
        }
        """;

    public const string GenericSettings =
        """
        using Typeshift;

        namespace Demo;

        internal record Paged<T>
        {
            public T? First { get; init; }

            public int Size { get; init; } = 20;
        }

        internal partial class PageToolset : Toolset
        {
            public PageToolset() =>
                AddTool(Page).WithDescription("Pages.").WithSettings<Paged<string>>().Configurable(s => s.Size);

            public static string Page(string query, Paged<string> paging) => query;
        }

        internal partial class PageAgent : Agent
        {
            public PageAgent()
            {
                WithDescription("Pages.");
                UseToolset<PageToolset>();
                ConfigurePage().WithSize(10);
            }
        }
        """;
}
