namespace Agentic.Generators.Parsing;

// Metadata names of the library types the generator recognizes by base type and by call target
internal static class KnownTypes
{
    public const string Toolset = "Agentic.Toolsets.Toolset";

    public const string ConfigurableSettingsPicker = "Agentic.Settings.ConfigurableSettingsPicker`1";

    public const string ServiceProvider = "System.IServiceProvider";

    public const string CancellationToken = "System.Threading.CancellationToken";

    public const string FunctionArguments = "Microsoft.Extensions.AI.AIFunctionArguments";

    public const string Task = "System.Threading.Tasks.Task";

    public const string TaskOfT = "System.Threading.Tasks.Task`1";

    public const string ValueTask = "System.Threading.Tasks.ValueTask";

    public const string ValueTaskOfT = "System.Threading.Tasks.ValueTask`1";
}
