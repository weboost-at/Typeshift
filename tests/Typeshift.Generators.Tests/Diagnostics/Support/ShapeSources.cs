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

    // The compiler reports each use of an obsolete symbol where the user wrote it; the generated files name the same
    // symbols and must not report them again
    public const string DeprecatedToolsetAndAgent =
        """
        using System;
        using Typeshift;

        namespace Demo;

        [Obsolete("Use NewTools")]
        internal partial class OldTools : Toolset
        {
            public OldTools() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        [Obsolete("Use NewAgent")]
        internal partial class OldAgent : Agent
        {
            public OldAgent()
            {
                WithDescription("Echoes.");
                UseToolset<OldTools>();
            }
        }
        """;

    public const string ObsoleteToolMethodAndParameterType =
        """
        using System;
        using Typeshift;

        namespace Demo;

        [Obsolete("Use New")]
        internal record Old(string Text);

        [Obsolete]
        internal record Older(string Text);

        internal partial class LegacyTools : Toolset
        {
            public LegacyTools()
            {
        #pragma warning disable CS0612, CS0618
                AddTool(Legacy).WithDescription("Kept for old conversations.");
                AddTool(TakeOld).WithDescription("Takes the old shape.");
                AddTool(GiveOlder).WithDescription("Gives the oldest shape.");
        #pragma warning restore CS0612, CS0618
            }

            [Obsolete("Old tool")]
            public static string Legacy(string text) => text;

        #pragma warning disable CS0612, CS0618
            public static string TakeOld(Old old) => old.Text;

            public static Older GiveOlder(string text) => new(text);
        #pragma warning restore CS0612, CS0618
        }
        """;

    // An [Experimental] symbol is an error under an id of its own, wherever it is named
    public const string ExperimentalToolTypesAndAgent =
        """
        using System.Collections.Generic;
        using System.Diagnostics.CodeAnalysis;
        using Typeshift;

        namespace Demo;

        [Experimental("DEMO001")]
        internal record Preview(string Text);

        internal partial class PreviewTools : Toolset
        {
            public PreviewTools()
            {
        #pragma warning disable DEMO001
                AddTool(Take).WithDescription("Takes a preview.");
                AddTool(Give).WithDescription("Gives a preview.");
                AddTool(List).WithDescription("Lists the previews.");
        #pragma warning restore DEMO001
            }

        #pragma warning disable DEMO001
            public static string Take(Preview preview) => preview.Text;

            public static Preview Give(string text) => new(text);

            public static Dictionary<string, Preview[]> List() => [];
        #pragma warning restore DEMO001
        }

        [Experimental("DEMO002")]
        internal partial class PreviewAgent : Agent
        {
            public PreviewAgent() => WithDescription("Previews.");
        }
        """;

    public const string ExperimentalToolsetSettingsAndProviderId =
        """
        using System.Diagnostics.CodeAnalysis;
        using Typeshift;

        namespace Demo;

        [Experimental("DEMO003")]
        internal enum Depth
        {
            Shallow,
            Deep
        }

        [Experimental("DEMO004")]
        internal enum Lab
        {
            Small,
            Large
        }

        [Experimental("DEMO005")]
        internal record TrialSettings
        {
        #pragma warning disable DEMO003
            public Depth Depth { get; init; }
        #pragma warning restore DEMO003

            [Experimental("DEMO006")]
            public int Limit { get; init; } = 3;
        }

        [Experimental("DEMO007")]
        internal partial class TrialTools : Toolset
        {
            public TrialTools()
            {
        #pragma warning disable DEMO005, DEMO008
                AddTool(Try).WithDescription("Tries.").WithConfigurableSettings<TrialSettings>();
        #pragma warning restore DEMO005, DEMO008
            }

        #pragma warning disable DEMO005
            [Experimental("DEMO008")]
            public static string Try(string text, TrialSettings settings) => text;
        #pragma warning restore DEMO005
        }

        internal partial class TrialAgent : Agent
        {
            public TrialAgent()
            {
                WithDescription("Tries.");

        #pragma warning disable DEMO004, DEMO006, DEMO007
                WithChatOptions()
                    .WithModel(Lab.Small, "trial-model")
                    .Configurable(o => o.ModelId);

                UseToolset<TrialTools>();
                ConfigureTry().WithLimit(5);
        #pragma warning restore DEMO004, DEMO006, DEMO007
            }
        }
        """;

    public const string NullDefaultsWithoutAnnotation =
        """
        using System;
        using System.Collections.Generic;
        using Typeshift;

        namespace Demo;

        internal partial class NullToolset : Toolset
        {
            public NullToolset()
            {
                AddTool(Search).WithDescription("Searches.");
                AddTool(Oblivious).WithDescription("Searches, in a file without nullable annotations.");
                AddTool(Open).WithDescription("Opens.");
            }

            public static string Search(string query = null!) => query;

        #nullable disable
            public static string Oblivious(string query = null, List<string> tags = null) => query;
        #nullable enable

            public static string Open(Uri uri = default!) => "";
        }
        """;

    public const string MemberOfAnExtensionBlock =
        """
        using Typeshift;

        namespace Demo;

        internal static class TextExtensions
        {
            extension(string)
            {
                public static string Shout(string text) => text.ToUpperInvariant();
            }
        }

        internal partial class ShoutTools : Toolset
        {
            public ShoutTools() => AddTool(string.Shout).WithDescription("Shouts.");
        }
        """;

    public const string ToolOfAClassClosedOverANullableType =
        """
        using Typeshift;

        namespace Demo;

        internal static class Finder<T>
        {
            public static string Find(T value) => "";
        }

        internal partial class FindTools : Toolset
        {
            public FindTools() => AddTool(Finder<string?>.Find).WithDescription("Finds.");
        }
        """;

    // The call fills a parameter by its type, nullable or not; the model never sees it
    public const string NullableParametersTheCallFills =
        """
        using System;
        using System.Threading;
        using Microsoft.Extensions.AI;
        using Typeshift;

        namespace Demo;

        internal record WaitSettings
        {
            public int Seconds { get; init; } = 1;
        }

        internal partial class WaitTools : Toolset
        {
            public WaitTools() => AddTool(Wait).WithDescription("Waits.").WithSettings<WaitSettings>();

            public static string Wait(
                string text,
                WaitSettings? settings,
                IServiceProvider? services,
                AIFunctionArguments? arguments,
                CancellationToken? ct = null) =>
                text;
        }
        """;

    public const string ResultByReference =
        """
        using Typeshift;

        namespace Demo;

        internal partial class CounterTools : Toolset
        {
            private static int _count;

            public CounterTools()
            {
                AddTool(Count).WithDescription("Counts.");
                AddTool(Peek).WithDescription("Reads the count.");
            }

            public static ref int Count() => ref _count;

            public static ref readonly int Peek() => ref _count;
        }
        """;

    // dynamic inside another type is object, which the JSON reads and writes
    public const string DynamicInsideAType =
        """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Typeshift;

        namespace Demo;

        internal partial class InspectTools : Toolset
        {
            public InspectTools()
            {
                AddTool(Inspect).WithDescription("Inspects.");
                AddTool(ListAsync).WithDescription("Lists.");
                AddTool(Stream).WithDescription("Streams.");
            }

            public static string Inspect(List<dynamic> values, dynamic[] more, Dictionary<string, dynamic> named) => "";

            public static Task<List<dynamic>> ListAsync() => Task.FromResult(new List<dynamic>());

            public static async IAsyncEnumerable<dynamic> Stream()
            {
                await Task.Yield();
                yield return 1;
            }
        }
        """;
}
