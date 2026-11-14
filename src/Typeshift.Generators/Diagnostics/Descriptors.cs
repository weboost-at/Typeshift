using Microsoft.CodeAnalysis;

namespace Typeshift.Generators.Diagnostics;

// D49: numbered afresh for the rewrite; after the first release an id never changes
internal static class Descriptors
{
    private const string Category = "Typeshift";

    private const string HelpUrl = "https://weboost-at.github.io/Typeshift/guide/diagnostics#";

    public static readonly DiagnosticDescriptor NotPartial = Error(
        "TSH001",
        "Toolset or agent is not partial",
        "'{0}' must be partial: the generator adds members to it");

    public static readonly DiagnosticDescriptor NotConstant = Error(
        "TSH002",
        "Declared value is not a constant",
        "The {0} must be a constant: the generator reads it at compile time");

    public static readonly DiagnosticDescriptor NotStaticMethodGroup = Error(
        "TSH003",
        "Tool is not a static method group",
        "'{0}' cannot be a tool: {1}");

    public static readonly DiagnosticDescriptor NotBindable = Error(
        "TSH004",
        "Tool parameter or result cannot be bound",
        "The {0} of the tool '{1}' cannot be {2}: {3}");

    public static readonly DiagnosticDescriptor SettingsNotRecord = Error(
        "TSH005",
        "Settings type is not a record with a parameterless constructor",
        "The settings type '{0}' must be a record: each layer applies to a copy of the code defaults");

    public static readonly DiagnosticDescriptor SettingsLessAccessible = Error(
        "TSH006",
        "Settings are less accessible than what exposes them",
        "'{0}' must be as accessible as '{1}', which exposes its settings");

    public static readonly DiagnosticDescriptor DuplicateToolName = Error(
        "TSH007",
        "Tool name is not unique",
        "Another tool is already named '{0}', or maps to the same member '{1}'");

    public static readonly DiagnosticDescriptor NotLowerSnakeCase = Error(
        "TSH008",
        "Tool name is not lower_snake_case",
        "The tool name '{0}' is not lower_snake_case; name the tool with WithName(\"...\")");

    public static readonly DiagnosticDescriptor MissingDescription = Warning(
        "TSH009",
        "Tool has no description",
        "The tool '{0}' has no description: add a <summary> to its method or WithDescription(...)");

    public static readonly DiagnosticDescriptor UnreadSettings = Warning(
        "TSH010",
        "Tool does not take its settings",
        "The tool '{0}' takes no parameter of its settings type '{1}', so its settings reach nothing");

    public static readonly DiagnosticDescriptor DocumentationOff = Warning(
        "TSH011",
        "XML docs are not generated",
        "{0} takes its descriptions from XML docs, which the compiler reads only with GenerateDocumentationFile");

    public static readonly DiagnosticDescriptor NotStorable = Error(
        "TSH012",
        "Configurable member has a type settings cannot store",
        "'{0}' cannot be configurable: settings cannot store it");

    public static readonly DiagnosticDescriptor NotSettable = Error(
        "TSH013",
        "Configurable member cannot be set",
        "The configurable member '{0}' has no public set or init accessor");

    public static readonly DiagnosticDescriptor OutsideConstructor = Error(
        "TSH014",
        "Declaration outside the constructor",
        "'{0}' is read by the generator only directly in the constructor");

    public static readonly DiagnosticDescriptor UnreadableChain = Error(
        "TSH015",
        "Chain the generator cannot read",
        "The generator cannot read this {0}: keep the declaration in one chain and pick members as s => s.Member");

    public static readonly DiagnosticDescriptor Unreachable = Error(
        "TSH016",
        "Toolset or agent is out of the generated code's reach",
        "'{0}' is out of the generated code's reach: {1}");

    public static readonly DiagnosticDescriptor NotDirectlyDerived = Error(
        "TSH017",
        "Toolset or agent does not derive from its base directly",
        "'{0}' {1}: {2}");

    public static readonly DiagnosticDescriptor GeneratedNameClash = Error(
        "TSH018",
        "Member clashes with a generated member",
        "{0} clashes with a member the generator writes for '{1}'");

    public static readonly DiagnosticDescriptor NotGenerated = Error(
        "TSH019",
        "Toolset was not compiled with the generator",
        "'{0}' {1}: {2}");

    public static readonly DiagnosticDescriptor UnstampedReference = Warning(
        "TSH020",
        "Referenced assembly was built with another Typeshift version or without the generator",
        "'{0}' declares toolsets or agents but {1}, so AddTypeshift() cannot register them: {2}");

    public static readonly DiagnosticDescriptor SharedKind = Error(
        "TSH022",
        "Two agents share a settings kind",
        "'{0}' stores its settings as '{1}', like '{2}': give one of them a WithStorageKind of its own");

    public static readonly DiagnosticDescriptor NotUsableAgent = Error(
        "TSH023",
        "Agent cannot be a tool or a handoff target",
        "'{0}' cannot be {1}: {2}");

    public static readonly DiagnosticDescriptor SubAgentNeedsApproval = Warning(
        "TSH024",
        "Sub-agent offers tools that need approval",
        "'{0}' offers tools that need approval ({1}), and a sub-agent cannot ask the host: in the default mode those "
        + "calls are rejected unless a rule or a standing approval allows them; add a rule, or hand off to the agent");

    public static readonly DiagnosticDescriptor AgentCircle = Error(
        "TSH026",
        "Agents call each other in a circle",
        "'{0}' calls '{1}' through UseAgent, and '{1}' calls '{0}' back: sub-agents may not form a circle");

    public static readonly DiagnosticDescriptor OfferedTwice = Error(
        "TSH025",
        "Tool is offered twice",
        "{0} is offered twice: use each toolset once, pick each tool once, and leave out one of two tools of a name");

    public static readonly DiagnosticDescriptor NoJsonMetadata = Error(
        "TSH027",
        "Tool type has no JSON metadata for Native AOT or a trimmed app",
        "'{0}' of '{1}' has no JSON metadata, which an app published with Native AOT or trimmed needs: list it in a "
        + "JsonSerializerContext of this assembly, [JsonSerializable(typeof({0}))]");

    public static readonly DiagnosticDescriptor NoStore = Error(
        "TSH028",
        "Type is no store",
        "'{0}' is no store: implement ISettingsStore<TId>, IConversationStore<TId>, IDecisionStore<TId> or "
        + "IAgentSettingsStore");

    public static readonly DiagnosticDescriptor UnusableProviderIdType = Error(
        "TSH029",
        "Provider id type cannot be used",
        "The provider's type '{0}' is one the agent's settings cannot store as one value: name the provider by a "
        + "string, a number, a Guid or an enum");

    public static readonly DiagnosticDescriptor NoChatClientProvider = Error(
        "TSH030",
        "Type is no chat client provider",
        "'{0}' is no chat client provider: implement IChatClientProvider<TProviderId>");

    public static readonly DiagnosticDescriptor LanguageVersionTooLow = Create(
        "TSH031",
        "C# language version is below 14",
        "Typeshift's generated code needs C# 14 or later, and this project compiles as C# {0}: remove <LangVersion> "
        + "from the project, or raise it",
        DiagnosticSeverity.Error,
        WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor ReadDifferently = Warning(
        "TSH032",
        "Generator read the declaration differently",
        "The generator read '{0}' differently from what the code says ({1}): it cannot see code that source "
        + "generators write, its own included; declare it with constants, methods and types of your own code");

    public static readonly DiagnosticDescriptor NumericEnum = Warning(
        "TSH033",
        "Enum of a tool is a number for Native AOT or a trimmed app",
        "'{0}' reads and writes the enum '{1}' of the tool '{2}' as a number in an app published with Native AOT or "
        + "trimmed: add [JsonSourceGenerationOptions(UseStringEnumConverter = true)] to the context, so the model "
        + "reads and sends the enum's names");

    public static readonly DiagnosticDescriptor SubAgentAsksUser = Warning(
        "TSH034",
        "Sub-agent cannot ask the user",
        "'{0}' declares UseQuestions(), and a sub-agent cannot ask the user: as a sub-agent it is not offered "
        + "ask_user; declare UseQuestions() on the agent that calls it, or hand off to the agent");

    public static readonly DiagnosticDescriptor ReservedToolName = Error(
        "TSH035",
        "Tool name is reserved",
        "The tool name '{0}' is reserved for {1}: name the tool with WithName(\"...\")");

    public static readonly DiagnosticDescriptor ConversationsWithoutDecisions = Warning(
        "TSH036",
        "Conversation store keeps no decisions",
        "'{0}' keeps conversations under {1} ids, and no store of this chain keeps their decisions, so a run of "
        + "'{2}' that asks your app fails: implement IDecisionStore<{1}> in the store, or add a store that does with "
        + "WithStore<T>()");

    public static readonly DiagnosticDescriptor ConfigurableModeBesideApproval = Warning(
        "TSH037",
        "Configurable mode can turn approval off",
        "'{0}' makes its mode configurable and offers tools that require approval ({1}), so settings that are stored "
        + "or posted can put a run in auto mode, where those calls run unasked: keep Mode out of the agent's "
        + "settings, or reject what must never run in an approval rule");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        Create(id, title, message, DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        Create(id, title, message, DiagnosticSeverity.Warning);

    // A tag such as CompilationEnd tells an IDE that the diagnostic is reported for the compilation as a whole
    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        DiagnosticSeverity severity,
        params string[] tags) =>
        new(
            id,
            title,
            message,
            Category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: HelpUrl + id.ToLowerInvariant(),
            customTags: tags);
}
