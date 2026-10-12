namespace Agentic.Modules;

/// <summary>
/// Lists an agent on its assembly's generated module.
/// </summary>
/// <param name="agent">The agent definition class.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleAgentAttribute(Type agent) : Attribute
{
    /// <summary>
    /// The agent definition class.
    /// </summary>
    public Type Agent { get; } = agent;
}
