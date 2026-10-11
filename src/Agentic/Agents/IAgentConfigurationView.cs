using System.Text.Json;

namespace Agentic.Agents;

/// <summary>
/// A generated typed view over an agent's stored configuration, created without reflection.
/// </summary>
public interface IAgentConfigurationView<TSelf> where TSelf : AgentConfigurationView
{
    /// <summary>
    /// Creates the view over the stored configuration.
    /// </summary>
    static abstract TSelf Create(AgentConfiguration configuration, JsonSerializerOptions options);
}
