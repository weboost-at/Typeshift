namespace Agentic.Generators.Parsing;

// Metadata names of the library types the generator recognizes by base type and by call target
internal static class KnownTypes
{
    public const string LibraryAssembly = "Agentic";

    public const string Toolset = "Agentic.Toolsets.Toolset";

    public const string AgentDefinition = "Agentic.Agents.AgentDefinition";

    public const string ServiceCollectionExtensions =
        "Microsoft.Extensions.DependencyInjection.AgenticServiceCollectionExtensions";

    public const string RegistrationMethod = "AddAgentic";

    public const string GeneratedModuleAttribute = "Agentic.Modules.GeneratedModuleAttribute";

    public const string ModuleToolsetAttribute = "Agentic.Modules.ModuleToolsetAttribute";

    public const string ModuleAgentAttribute = "Agentic.Modules.ModuleAgentAttribute";

    public const string GeneratedToolAttribute = "Agentic.Modules.GeneratedToolAttribute";

    public const string ChatOptions = "Microsoft.Extensions.AI.ChatOptions";

    public const string ConfigurableSettingsPicker = "Agentic.Settings.ConfigurableSettingsPicker`1";

    public const string ServiceProvider = "System.IServiceProvider";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string FunctionArguments = "Microsoft.Extensions.AI.AIFunctionArguments";

    public const string Task = "System.Threading.Tasks.Task";

    public const string TaskOfT = "System.Threading.Tasks.Task`1";

    public const string ValueTask = "System.Threading.Tasks.ValueTask";

    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";
}
