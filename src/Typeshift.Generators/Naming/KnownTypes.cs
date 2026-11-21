namespace Typeshift.Generators.Naming;

// Metadata names of the types the generator recognizes by base type and by call target, and the version of what
// generated code binds to
internal static class KnownTypes
{
    // D7: raised whenever the stamps change shape or a member generated code binds to changes; a reference with
    // another version is TSH020
    public const int StampVersion = 22;

    public const string Toolset = "Typeshift.Toolset";

    public const string ToolBuilder = "Typeshift.ToolBuilder";

    public const string SettingsBuilder = "Typeshift.ToolBuilder`1";

    public const string ConfigurableSettingsBuilder = "Typeshift.ConfigurableSettingsBuilder`1";

    public const string Agent = "Typeshift.Agent";

    public const string AgentHost = "Typeshift.AgentHost`1";

    public const string ToolsetUse = "Typeshift.ToolsetUse`1";

    public const string ToolInclusion = "Typeshift.ToolInclusion`1";

    public const string ToolExclusion = "Typeshift.ToolExclusion`1";

    public const string ChatOptionsBuilder = "Typeshift.ChatOptionsBuilder";

    public const string RunOptionsBuilder = "Typeshift.RunOptionsBuilder";

    public const string LibraryAssembly = "Typeshift";

    public const string ServiceCollectionExtensions =
        "Microsoft.Extensions.DependencyInjection.TypeshiftServiceCollectionExtensions";

    public const string RegistrationMethod = "AddTypeshift";

    public const string RuntimeName = "TypeshiftRuntime";

    public const string Runtime = "Typeshift." + RuntimeName;

    public const string RuntimeMethod = "Create";

    // D76: what Create returns, the interface over the library's runtime
    public const string RuntimeInterface = "Typeshift.ITypeshiftRuntime";

    public const string GeneratedModuleAttribute = "Typeshift.Generated.GeneratedModuleAttribute";

    public const string ModuleToolsetAttribute = "Typeshift.Generated.ModuleToolsetAttribute";

    public const string GeneratedToolAttribute = "Typeshift.Generated.GeneratedToolAttribute";

    public const string ModuleAgentAttribute = "Typeshift.Generated.ModuleAgentAttribute";

    public const string TypeshiftBuilder = "Typeshift.TypeshiftBuilder";

    public const string SettingsStore = "Typeshift.ISettingsStore`1";

    public const string ConversationStore = "Typeshift.IConversationStore`1";

    public const string DecisionStore = "Typeshift.IDecisionStore`1";

    public const string AgentSettingsStore = "Typeshift.IAgentSettingsStore";

    public const string JsonSerializerContext = "System.Text.Json.Serialization.JsonSerializerContext";

    public const string JsonSerializableAttribute = "System.Text.Json.Serialization.JsonSerializableAttribute";

    // The attributes by which a type says how JSON reads it, whatever its kind
    public static readonly string[] JsonShapes =
    [
        "System.Text.Json.Serialization.JsonConverterAttribute",
        "System.Text.Json.Serialization.JsonPolymorphicAttribute",
        "System.Text.Json.Serialization.JsonDerivedTypeAttribute"
    ];

    public const string SchemaAnnotationsAttribute = "Weboost.SchemaAnnotations.SchemaAnnotationsAttribute";

    public const string ServiceProvider = "System.IServiceProvider";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string FunctionArguments = "Microsoft.Extensions.AI.AIFunctionArguments";

    public const string Task = "System.Threading.Tasks.Task";

    public const string TaskOfT = "System.Threading.Tasks.Task`1";

    public const string ValueTask = "System.Threading.Tasks.ValueTask";

    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";

    public const string AsyncEnumerableOfT = "System.Collections.Generic.IAsyncEnumerable`1";

    public const string EnumerableOfT = "System.Collections.Generic.IEnumerable`1";

    public const string Content = "Microsoft.Extensions.AI.AIContent";

    public const string Obsolete = "System.ObsoleteAttribute";

    // D14: what Microsoft.Extensions.AI reads for a description, which a tool's XML docs override
    public const string Description = "System.ComponentModel.DescriptionAttribute";

    public const string Experimental = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";
}
