using Agentic.Json;
using Agentic.Settings;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Tools;

internal sealed class SettingsToolBinding<TSettings>(
    ToolSettingsBinder<TSettings> binder,
    Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction) : ToolBinding
    where TSettings : class, new()
{
    internal override AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        createFunction(tool, binder.Resolve(layers, json.SerializerOptions), json);
}
