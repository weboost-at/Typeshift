using System.ComponentModel;

namespace Typeshift.Generated;

/// <summary>
/// Names the module that registers an assembly's toolsets and agents.
/// </summary>
/// <param name="module">The module.</param>
/// <param name="version">D7: the stamps' version, which generators of other versions check.</param>
/// <param name="idType">D11: the id type the assembly was built with; null for an assembly without agents.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GeneratedModuleAttribute(Type module, int version, Type? idType) : Attribute
{
    /// <summary>
    /// The module.
    /// </summary>
    public Type Module { get; } = module;

    /// <summary>
    /// The stamps' version.
    /// </summary>
    public int Version { get; } = version;

    /// <summary>
    /// The id type the assembly was built with; null for an assembly without agents.
    /// </summary>
    public Type? IdType { get; } = idType;
}

/// <summary>
/// Names an agent a module registers, the kind its stored settings go by, and what the agents of other assemblies
/// check when they use it.
/// </summary>
/// <param name="agent">The agent.</param>
/// <param name="kind">D39: the kind.</param>
/// <param name="name">D22: the agent's name, which it goes by as a tool or a handoff target.</param>
/// <param name="approvalTools">D33: the tools the agent offers that ask the host before they run.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleAgentAttribute(
    Type agent, string kind, string name, params string[] approvalTools) : Attribute
{
    /// <summary>
    /// The agent.
    /// </summary>
    public Type Agent { get; } = agent;

    /// <summary>
    /// The kind its stored settings go by.
    /// </summary>
    public string Kind { get; } = kind;

    /// <summary>
    /// The agent's name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The tools the agent offers that ask the host before they run.
    /// </summary>
    public IReadOnlyList<string> ApprovalTools { get; } = approvalTools;

    /// <summary>
    /// Whether the agent has a description, which it needs as a tool or a handoff target.
    /// </summary>
    public bool Described { get; init; }
}

/// <summary>
/// Names a toolset a module registers.
/// </summary>
/// <param name="toolset">The toolset.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleToolsetAttribute(Type toolset) : Attribute
{
    /// <summary>
    /// The toolset.
    /// </summary>
    public Type Toolset { get; } = toolset;
}

/// <summary>
/// Describes a tool of a toolset, for the generator of an agent in another assembly.
/// </summary>
/// <param name="name">The tool's name.</param>
/// <param name="property">The tool's property on the toolset's tool list.</param>
/// <param name="factory">The static method that creates the tool.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GeneratedToolAttribute(string name, string property, string factory) : Attribute
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The tool's property on the toolset's tool list.
    /// </summary>
    public string Property { get; } = property;

    /// <summary>
    /// The static method that creates the tool.
    /// </summary>
    public string Factory { get; } = factory;

    /// <summary>
    /// The tool's settings record, if it has one.
    /// </summary>
    public Type? Settings { get; init; }

    /// <summary>
    /// The record of the tool's configurable settings, if it has any.
    /// </summary>
    public Type? Overrides { get; init; }

    /// <summary>
    /// Whether the tool asks the host before it runs.
    /// </summary>
    public bool RequiresApproval { get; init; }
}
