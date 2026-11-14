namespace Typeshift.Generators.Tests.Diagnostics.Support;

// One shape each that the parsers accept without a diagnostic: the code generated for it compiles. A shape the
// parser newly accepts gets a field here, and one it rejects gets a field in DiagnosticSources
public static class ShapeSources
{
    public const string EnumToolTypeInAContextOfStringEnums =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        internal enum Mode
        {
            Fast,
            Thorough
        }

        internal partial class PickToolset : Toolset
        {
            public PickToolset()
            {
                AddTool(Pick).WithDescription("Picks how to search.");
            }

            public static string Pick(Mode mode) => mode.ToString();
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
        [JsonSerializable(typeof(Mode))]
        internal partial class PickJson() : JsonSerializerContext(null)
        {
            public static PickJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    public const string EnumToolTypeWhoseContextNamesItsConverter =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        internal enum Mode
        {
            Fast,
            Thorough
        }

        internal partial class PickToolset : Toolset
        {
            public PickToolset()
            {
                AddTool(Pick).WithDescription("Picks how to search.");
            }

            public static string Pick(Mode mode) => mode.ToString();
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSourceGenerationOptions(Converters = [typeof(JsonStringEnumConverter<Mode>)])]
        [JsonSerializable(typeof(Mode))]
        internal partial class PickJson() : JsonSerializerContext(null)
        {
            public static PickJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

    public const string EnumToolTypeWithAConverterOfItsOwn =
        """
        using System.Text.Json.Serialization;
        using Typeshift;

        [JsonConverter(typeof(JsonStringEnumConverter<Mode>))]
        internal enum Mode
        {
            Fast,
            Thorough
        }

        internal partial class PickToolset : Toolset
        {
            public PickToolset()
            {
                AddTool(Pick).WithDescription("Picks how to search.");
            }

            public static string Pick(Mode mode) => mode.ToString();
        }

        // The System.Text.Json generator does not run here; what it would write is stubbed
        [JsonSerializable(typeof(Mode))]
        internal partial class PickJson() : JsonSerializerContext(null)
        {
            public static PickJson Default { get; } = new();

            protected override System.Text.Json.JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override System.Text.Json.Serialization.Metadata.JsonTypeInfo? GetTypeInfo(System.Type type) => null;
        }
        """;

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
                internal partial class EchoAgent : Agent
                {
                    public EchoAgent()
                    {
                        WithDescription("Echoes.");
                        UseToolset<Tools.Outer.Inner.EchoToolset>().Include(t => t.Echo);
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
                UseQuestions();

                AddTool(Archive)
                    .WithDescription("Archives old mails.")
                    .WithConfigurableSettings<ArchiveSettings>();
            }

            private static int Archive(ArchiveSettings settings) => settings.Days;
        }
        """;

    // A store of conversations and one of decisions, each alone, for an agent that asks; each source below adds the
    // registration that names both
    private const string ChatAndDecisionStores =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal sealed class ChatStore : IConversationStore<int>
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

        internal partial class DeskAgent : Agent
        {
            public DeskAgent() => UseQuestions();
        }

        """;

    public const string ConversationAndDecisionStoresInOneChain = ChatAndDecisionStores +
        """
        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<ChatStore>().WithStore<WaitingStore>();
        }
        """;

    public const string ConversationAndDecisionStoresOnOneBuilder = ChatAndDecisionStores +
        """
        internal static class Startup
        {
            public static void Register(IServiceCollection services)
            {
                var typeshift = services.AddTypeshift();

                typeshift.WithStore<ChatStore>();
                typeshift.WithStore<WaitingStore>();
            }
        }
        """;

    public const string StoreOfEverythingForAnAgentThatAsks =
        """
        using Typeshift;
        using Microsoft.Extensions.DependencyInjection;

        internal partial class DeskAgent : Agent
        {
            public DeskAgent() => UseQuestions();
        }

        internal static class Startup
        {
            public static void Register(IServiceCollection services) =>
                services.AddTypeshift().WithStore<InMemoryTypeshiftStore<int>>();
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

    // Nothing to configure: the agent gets no Configure{Tool}() for the tool
    public const string SettingsWithoutSettableMembers =
        """
        using Typeshift;

        namespace Demo;

        internal record Nothing
        {
            public int Computed => 1;
        }

        internal partial class NothingToolset : Toolset
        {
            public NothingToolset() => AddTool(Echo).WithDescription("Echoes.").WithSettings<Nothing>();

            public static string Echo(string text, Nothing nothing) => text;
        }

        internal partial class NothingAgent : Agent
        {
            public NothingAgent()
            {
                WithDescription("Nothing.");
                UseToolset<NothingToolset>();
            }
        }
        """;

    // XML has no form for a control character, so the doc comments leave it out
    public const string ControlCharactersInADescription =
        """
        using Typeshift;

        namespace Demo;

        internal partial class ControlToolset : Toolset
        {
            public ControlToolset() => AddTool(Echo).WithDescription("ctl \0 \u0001 \uFFFE \u2028 end");

            public static string Echo(string text) => text;
        }

        internal partial class ControlAgent : Agent
        {
            public ControlAgent()
            {
                WithDescription("ctl \0 \u0001 \uFFFE \u2028 end");
                UseToolset<ControlToolset>();
            }
        }
        """;

    public const string DefaultsDeclaredByAttribute =
        """
        using System;
        using System.Runtime.CompilerServices;
        using System.Runtime.InteropServices;
        using Typeshift;

        namespace Demo;

        internal partial class WhenToolset : Toolset
        {
            public WhenToolset() => AddTool(When).WithDescription("Tells when.");

            public static string When(
                [Optional, DateTimeConstant(5)] DateTime at,
                [Optional, DecimalConstant(2, 0, 0, 0, 125)] decimal price,
                [Optional, DefaultParameterValue(7)] long count,
                [Optional, DefaultParameterValue("soon")] object note,
                [Optional] string zone,
                sbyte step = -1,
                ushort port = 80) =>
                "";
        }
        """;

    // An agent's Settings would hide what every record inherits from object, so these members get a suffix
    public const string ToolsNamedLikeMembersOfObject =
        """
        using Typeshift;

        namespace Demo;

        internal record Limits
        {
            public int Max { get; init; } = 10;
        }

        internal partial class TypeToolset : Toolset
        {
            public TypeToolset()
            {
                AddTool(TypeOf).WithName("get_type").WithDescription("Gets.").WithConfigurableSettings<Limits>();
                AddTool(Clone).WithName("memberwise_clone").WithDescription("Clones.").WithSettings<Limits>();
                AddTool(Same).WithName("reference_equals").WithDescription("Is.").WithConfigurableSettings<Limits>();
                AddTool(Hash).WithName("get_hash_code").WithDescription("Hashes.").WithConfigurableSettings<Limits>();
                AddTool(Text).WithName("to_string").WithDescription("Writes.").WithConfigurableSettings<Limits>();
            }

            public static string TypeOf(string text, Limits limits) => text;

            public static string Clone(string text, Limits limits) => text;

            public static string Same(string text, Limits limits) => text;

            public static string Hash(string text, Limits limits) => text;

            public static string Text(string text, Limits limits) => text;
        }

        internal partial class TypeAgent : Agent
        {
            public TypeAgent()
            {
                WithDescription("Types.");

                UseToolset<TypeToolset>()
                    .Include(t => t.GetTypeTool)
                    .Include(t => t.MemberwiseCloneTool)
                    .Include(t => t.ReferenceEqualsTool)
                    .Include(t => t.GetHashCodeTool)
                    .Include(t => t.ToStringTool);
            }
        }
        """;

    // The generated ApplyTo and JSON members have parameters and locals of these names
    public const string SettingsMembersNamedLikeGeneratedLocals =
        """
        #pragma warning disable CS8981

        using Typeshift;

        namespace Demo;

        internal record nested
        {
            public int writer { get; init; } = 1;

            public int value { get; init; } = 1;

            public int element { get; init; } = 1;

            public int defaults { get; init; } = 1;

            public int member0 { get; init; } = 1;
        }

        internal record Lower
        {
            public int settings { get; init; } = 1;

            public int writer { get; init; } = 1;

            public int element { get; init; } = 1;

            public int value { get; init; } = 1;

            public int member0 { get; init; } = 1;

            public int member1 { get; init; } = 1;

            public nested nested { get; init; } = new();
        }

        internal partial class LowerToolset : Toolset
        {
            public LowerToolset() => AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<Lower>();

            public static string Echo(string text, Lower lower) => text;
        }

        internal partial class LowerAgent : Agent
        {
            public LowerAgent()
            {
                WithDescription("Lower.");
                UseToolset<LowerToolset>();
                ConfigureEcho().Withvalue(2).Withsettings(3);
            }
        }
        """;

    // A class cannot hold a member of its own name, so this agent gets no Configure{Tool}() for the tool
    public const string AgentNamedLikeAConfigureMethod =
        """
        using Typeshift;

        namespace Demo;

        internal record EchoSettings
        {
            public int Repeat { get; init; } = 1;
        }

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.").WithConfigurableSettings<EchoSettings>();

            public static string Echo(string text, EchoSettings settings) => text;
        }

        internal partial class ConfigureEcho : Agent
        {
            public ConfigureEcho()
            {
                WithDescription("Echoes.");
                UseToolset<EchoToolset>();
            }
        }
        """;

    // The tool list's CreateReport property and the toolset's CreateReport factory live in two classes
    public const string ToolsWhoseGeneratedNamesMeetAcrossClasses =
        """
        using Typeshift;

        namespace Demo;

        internal partial class ReportToolset : Toolset
        {
            public ReportToolset()
            {
                AddTool(Find).WithName("report").WithDescription("Finds a report.");
                AddTool(Make).WithName("create_report").WithDescription("Creates a report.");
            }

            public static string Find(string title) => title;

            public static string Make(string title) => title;
        }

        internal partial class ReportAgent : Agent
        {
            public ReportAgent()
            {
                WithDescription("Reports.");
                UseToolset<ReportToolset>().Include(t => t.Report).Include(t => t.CreateReport);
            }
        }
        """;

    // The picks are in scope in every file, whatever it imports: the agents' file has no using for the toolset's
    // namespace
    public const string ToolsetNamedInFullOrByAnAlias =
        """
        using Typeshift;
        using Tickets = Tools.TicketToolset;

        namespace Tools
        {
            internal partial class TicketToolset : Toolset
            {
                public TicketToolset()
                {
                    AddTool(Search).WithDescription("Searches.");
                    AddTool(Close).WithDescription("Closes.");
                }

                public static string Search(string text) => text;

                public static string Close(int number) => "";
            }
        }

        namespace Agents
        {
            internal partial class AliasAgent : Agent
            {
                public AliasAgent()
                {
                    WithDescription("Names the toolset through an alias.");
                    UseToolset<Tickets>().Include(t => t.Search);
                }
            }

            internal partial class QualifiedAgent : Agent
            {
                public QualifiedAgent()
                {
                    WithDescription("Names the toolset in full.");
                    UseToolset<Tools.TicketToolset>().Exclude(t => t.Close);
                }
            }
        }
        """;

    // The tool lists live in the global namespace under names of their own, so no type beside a toolset meets one
    public const string TypesNamedLikeAToolsetsToolList =
        """
        using Typeshift;

        namespace Demo;

        internal partial class Echo : Toolset
        {
            public Echo() => AddTool(Say).WithDescription("Echoes.");

            public static string Say(string text) => text;
        }

        internal class EchoToolList;

        internal partial class Outer
        {
            internal partial class Tools : Toolset
            {
                public Tools() => AddTool(Repeat).WithDescription("Repeats.");

                public static string Repeat(string text) => text;
            }
        }

        internal partial class Outer_Tools : Toolset
        {
            public Outer_Tools() => AddTool(Shout).WithDescription("Shouts.");

            public static string Shout(string text) => text;
        }

        internal partial class EchoAgent : Agent
        {
            public EchoAgent()
            {
                WithDescription("Echoes.");
                UseToolset<Echo>().Include(t => t.Say);
                UseToolset<Outer.Tools>().Include(t => t.Repeat);
                UseToolset<Outer_Tools>().Include(t => t.Shout);
            }
        }
        """;

    // kind gets a suffix in the agent's Settings, which have a Kind of their own, and would then meet kind_tool
    public const string ToolsThatMeetInSettingsThroughTheSuffix =
        """
        using Typeshift;

        namespace Demo;

        internal record Limits
        {
            public int Max { get; init; } = 10;
        }

        internal partial class KindToolset : Toolset
        {
            public KindToolset()
            {
                AddTool(One).WithName("kind").WithDescription("One.").WithConfigurableSettings<Limits>();
                AddTool(Two).WithName("kind_tool").WithDescription("Two.").WithConfigurableSettings<Limits>();
            }

            public static string One(string text, Limits limits) => text;

            public static string Two(string text, Limits limits) => text;
        }

        internal partial class KindAgent : Agent
        {
            public KindAgent()
            {
                WithDescription("Kinds.");
                UseToolset<KindToolset>();
            }
        }
        """;

    // Parentheses and the ! operator are syntax without an operation of their own: the picks are still read
    public const string PicksOnAWrappedReceiver =
        """
        using Typeshift;

        namespace Demo;

        internal partial class FirstToolset : Toolset
        {
            public FirstToolset()
            {
                AddTool(OpenFirst).WithDescription("Opens.");
                AddTool(CloseFirst).WithDescription("Closes.");
            }

            public static string OpenFirst(int number) => "open";

            public static string CloseFirst(int number) => "closed";
        }

        internal partial class SecondToolset : Toolset
        {
            public SecondToolset()
            {
                AddTool(OpenSecond).WithDescription("Opens.");
                AddTool(CloseSecond).WithDescription("Closes.");
            }

            public static string OpenSecond(int number) => "open";

            public static string CloseSecond(int number) => "closed";
        }

        internal partial class ThirdToolset : Toolset
        {
            public ThirdToolset()
            {
                AddTool(OpenThird).WithDescription("Opens.");
                AddTool(CloseThird).WithDescription("Closes.");
            }

            public static string OpenThird(int number) => "open";

            public static string CloseThird(int number) => "closed";
        }

        internal partial class PickAgent : Agent
        {
            public PickAgent()
            {
                WithDescription("Picks.");
                (UseToolset<FirstToolset>()).Include(t => t.OpenFirst);
                UseToolset<SecondToolset>()!.Include(t => t.OpenSecond);
                ((UseToolset<ThirdToolset>())!).Exclude(t => t.OpenThird);
            }
        }
        """;

    // Methods of the project's own that are named like the library's, called in every way the analyzer has a rule
    // for when the library's are: in a local function, on a variable, outside a constructor
    public const string MethodsNamedLikeTheLibrarys =
        """
        using System.Collections.Generic;
        using System.Text;

        namespace Demo;

        internal sealed class Registry
        {
            private readonly List<string> _names = [];

            public Registry AddTool(string name)
            {
                _names.Add(name);
                return this;
            }

            public Registry WithName(string name) => AddTool(name);

            public Registry WithStore<T>() => this;

            public string Describe()
            {
                void Add() => AddTool("local").WithName("named").WithStore<int>();

                Add();

                var registry = WithStore<string>();
                registry.WithName("later");

                return new StringBuilder().Append(_names.Count).ToString();
            }
        }
        """;

    // A context written by hand has no Default unless it declares one, so the module has nothing to register
    public const string JsonContextWithoutDefault =
        """
        using System;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using Typeshift;

        namespace Demo;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal class HandWritten : JsonSerializerContext
        {
            public HandWritten() : base(null) { }

            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
        }
        """;

    public const string JsonContextWithADefaultField =
        """
        using System;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Text.Json.Serialization.Metadata;
        using Typeshift;

        namespace Demo;

        internal partial class EchoToolset : Toolset
        {
            public EchoToolset() => AddTool(Echo).WithDescription("Echoes.");

            public static string Echo(string text) => text;
        }

        internal class HandWritten : JsonSerializerContext
        {
            public static readonly JsonSerializerContext Default = new HandWritten();

            private HandWritten() : base(null) { }

            protected override JsonSerializerOptions? GeneratedSerializerOptions => null;

            public override JsonTypeInfo? GetTypeInfo(Type type) => null;
        }
        """;
}
