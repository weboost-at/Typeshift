namespace Typeshift.Generators.Parsing;

// Metadata names of the library types the generator recognizes by base type and by call target
internal static class KnownTypes
{
    public const string LibraryAssembly = "Typeshift";

    public const string Toolset = "Typeshift.Toolsets.Toolset";

    public const string AgentDefinition = "Typeshift.Agents.AgentDefinition";

    public const string ServiceCollectionExtensions =
        "Microsoft.Extensions.DependencyInjection.TypeshiftServiceCollectionExtensions";

    public const string RegistrationMethod = "AddTypeshift";

    public const string GeneratedModuleAttribute = "Typeshift.Modules.GeneratedModuleAttribute";

    public const string ModuleToolsetAttribute = "Typeshift.Modules.ModuleToolsetAttribute";

    public const string ModuleAgentAttribute = "Typeshift.Modules.ModuleAgentAttribute";

    public const string GeneratedToolAttribute = "Typeshift.Modules.GeneratedToolAttribute";

    public const string ChatOptionsBuilder = "Typeshift.Agents.ChatOptionsBuilder";

    public const string ServiceProvider = "System.IServiceProvider";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string FunctionArguments = "Microsoft.Extensions.AI.AIFunctionArguments";

    public const string Task = "System.Threading.Tasks.Task";

    public const string TaskOfT = "System.Threading.Tasks.Task`1";

    public const string ValueTask = "System.Threading.Tasks.ValueTask";

    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";
}
