using Agentic.Agents;

namespace Agentic.Samples.PackageConsumer;

/// <summary>
/// Greets whoever it talks to.
/// </summary>
public partial class GreeterAgent : AgentDefinition
{
    /// <summary>
    /// Declares the agent and its tools.
    /// </summary>
    public GreeterAgent()
    {
        WithName("greeter_agent");
        UseToolset<GreetingToolset>();
    }
}
