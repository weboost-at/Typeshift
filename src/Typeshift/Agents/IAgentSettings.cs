using Typeshift.Settings;
using Typeshift.Tools;
using Microsoft.Extensions.AI;

namespace Typeshift.Agents;

/// <summary>
/// The settings of an agent that may change at runtime. The generator writes one record per agent, nested in it as
/// <c>Settings</c>, holding the model, the configurable chat options and the configurable settings of its tools.
/// </summary>
public interface IAgentSettings
{
    /// <summary>
    /// Applies the model and the chat options that are set onto <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options of the layer below, changed in place.</param>
    void ApplyTo(ChatOptions options);

    /// <summary>
    /// The overrides of one of the agent's tools.
    /// </summary>
    /// <param name="tool">The tool's name.</param>
    /// <returns>The tool's overrides, or null when the tool has no configurable settings.</returns>
    IToolOverrides? GetToolOverrides(ToolName tool);
}
