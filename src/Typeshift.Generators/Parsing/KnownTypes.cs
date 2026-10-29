namespace Typeshift.Generators.Parsing;

// Metadata names of the types the generator recognizes by base type and by call target
internal static class KnownTypes
{
    public const string Toolset = "Typeshift.Toolset";

    public const string ToolBuilder = "Typeshift.ToolBuilder";

    public const string SettingsBuilder = "Typeshift.ToolBuilder`1";

    public const string ConfigurableSettingsBuilder = "Typeshift.ConfigurableSettingsBuilder`1";

    public const string Agent = "Typeshift.Agent";

    public const string ToolsetUse = "Typeshift.ToolsetUse`1";

    public const string ToolInclusion = "Typeshift.ToolInclusion`1";

    public const string ToolExclusion = "Typeshift.ToolExclusion`1";

    public const string ChatOptionsBuilder = "Typeshift.ChatOptionsBuilder";

    public const string RunOptionsBuilder = "Typeshift.RunOptionsBuilder";

    public const string LibraryAssembly = "Typeshift";

    // D37: where the library's built-in toolsets live, as their stamps name them
    public const string BuiltInNamespace = "global::Typeshift.Toolsets.";

    public const string ServiceCollectionExtensions =
        "Microsoft.Extensions.DependencyInjection.TypeshiftServiceCollectionExtensions";

    public const string RegistrationMethod = "AddTypeshift";

    public const string GeneratedModuleAttribute = "Typeshift.Generated.GeneratedModuleAttribute";

    public const string ModuleToolsetAttribute = "Typeshift.Generated.ModuleToolsetAttribute";

    public const string GeneratedToolAttribute = "Typeshift.Generated.GeneratedToolAttribute";

    public const string ModuleAgentAttribute = "Typeshift.Generated.ModuleAgentAttribute";

    public const string TypeshiftBuilder = "Typeshift.TypeshiftBuilder";

    public const string SettingsStore = "Typeshift.ISettingsStore`1";

    public const string ConversationStore = "Typeshift.IConversationStore`1";

    public const string ChatClientProvider = "Typeshift.IChatClientProvider`1";

    public const string JsonSerializerContext = "System.Text.Json.Serialization.JsonSerializerContext";

    public const string JsonSerializableAttribute = "System.Text.Json.Serialization.JsonSerializableAttribute";

    public const string SchemaAnnotationsAttribute = "Weboost.SchemaAnnotations.SchemaAnnotationsAttribute";

    public const string ServiceProvider = "System.IServiceProvider";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string FunctionArguments = "Microsoft.Extensions.AI.AIFunctionArguments";

    public const string Task = "System.Threading.Tasks.Task";

    public const string TaskOfT = "System.Threading.Tasks.Task`1";

    public const string ValueTask = "System.Threading.Tasks.ValueTask";

    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";

    public const string AsyncEnumerableOfT = "System.Collections.Generic.IAsyncEnumerable`1";

    public const string Obsolete = "System.ObsoleteAttribute";
}
