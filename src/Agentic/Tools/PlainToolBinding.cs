using Agentic.Json;
using Agentic.Settings;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Tools;

internal sealed class PlainToolBinding(Func<ToolDefinition, AgentJson, AIFunction> createFunction) : ToolBinding
{
    internal override AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        createFunction(tool, json);

    internal override object? ImportSettings(StoredSettings stored, AgentJson json) => null;

    internal override StoredSettings ExportSettings(object settings, AgentJson json) => StoredSettings.Empty;
}
