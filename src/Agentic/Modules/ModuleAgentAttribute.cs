namespace Agentic.Modules;

/// <summary>
/// Lists an agent and its unique name on its assembly's generated module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleAgentAttribute(Type agent, string uniqueName) : Attribute
{
    /// <summary>
    /// The agent definition class.
    /// </summary>
    public Type Agent { get; } = agent;

    /// <summary>
    /// The agent's unique name.
    /// </summary>
    public string UniqueName { get; } = uniqueName;
}
