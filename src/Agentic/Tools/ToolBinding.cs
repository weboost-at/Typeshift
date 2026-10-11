using Agentic.Json;
using Agentic.Settings;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Tools;

/// <summary>
/// What the generator binds a tool to: the function that runs it and, if it has settings, their binder.
/// </summary>
public abstract class ToolBinding
{
    private protected ToolBinding()
    {
    }

    /// <summary>
    /// Binds a tool without settings.
    /// </summary>
    public static ToolBinding Create(Func<ToolDefinition, AgentJson, AIFunction> createFunction) =>
        new PlainToolBinding(createFunction);

    /// <summary>
    /// Binds a tool that reads a settings record.
    /// </summary>
    public static ToolBinding Create<TSettings>(
        ToolSettingsBinder<TSettings> settings,
        Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
        where TSettings : class, new() =>
        new SettingsToolBinding<TSettings>(settings, createFunction);

    internal abstract AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json);

    internal abstract object? ImportSettings(StoredSettings stored, AgentJson json);

    internal abstract StoredSettings ExportSettings(object settings, AgentJson json);
}
