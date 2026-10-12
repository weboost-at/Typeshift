using Agentic.Json;
using Agentic.Settings;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Tools;

internal class SettingsToolBinding<TSettings>(Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
    : ToolBinding
    where TSettings : class, new()
{
    internal override AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        createFunction(tool, Resolve(layers), json);

    // The agent's code settings act as a whole object, so stored defaults only reach a tool the agent leaves alone
    private static TSettings Resolve(ToolSettingsLayers layers)
    {
        var settings = layers.Agent as TSettings ?? Apply(layers.Defaults, new TSettings());
        return Apply(layers.Run, Apply(layers.AgentOverrides, settings));
    }

    private static TSettings Apply(IToolOverrides? overrides, TSettings settings) =>
        overrides is IToolOverrides<TSettings> typed ? typed.ApplyTo(settings) : settings;
}
