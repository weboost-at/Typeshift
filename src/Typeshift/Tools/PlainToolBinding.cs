using Typeshift.Json;
using Typeshift.Settings;
using Typeshift.Toolsets;
using Microsoft.Extensions.AI;

namespace Typeshift.Tools;

internal sealed class PlainToolBinding(Func<ToolDefinition, AgentJson, AIFunction> createFunction) : ToolBinding
{
    internal override AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        createFunction(tool, json);
}
