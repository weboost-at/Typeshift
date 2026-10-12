using Agentic.Tools;

namespace Agentic.Toolsets;

/// <summary>
/// Declares a tool's name, description and settings, in a chain the generator reads at compile time.
/// </summary>
public sealed class ToolBuilder
{
    private readonly ToolDefinition _definition;

    internal ToolBuilder(ToolDefinition definition) => _definition = definition;

    /// <summary>
    /// Names the tool, as the model, the stores and the agents address it.
    /// </summary>
    /// <param name="name">A lower_snake_case constant, unique across everything the host can see.</param>
    /// <returns>The same builder, to describe and configure the tool further.</returns>
    public ToolBuilder WithName(string name)
    {
        _definition.Name = new ToolName(name);
        return this;
    }

    /// <summary>
    /// Describes the tool to the model; without it the method's XML summary is used.
    /// </summary>
    /// <param name="description">A compile-time constant.</param>
    /// <returns>The same builder, to name and configure the tool further.</returns>
    public ToolBuilder WithDescription(string description)
    {
        _definition.Description = description;
        return this;
    }

    /// <summary>
    /// Gives the tool a settings record. The tool method receives the resolved settings through a parameter of
    /// their type, which the model never sees.
    /// </summary>
    /// <typeparam name="TSettings">The settings record; its parameterless constructor sets the code defaults.</typeparam>
    /// <returns>A builder that marks the members that may change at runtime; without it none may.</returns>
    public ToolBuilder<TSettings> WithSettings<TSettings>() where TSettings : class, new()
    {
        _definition.SettingsType = typeof(TSettings);
        return new ToolBuilder<TSettings>();
    }
}

/// <summary>
/// Marks the members of a tool's settings that may change at runtime, in a chain the generator reads at compile
/// time: <c>Configurable</c> allows the members it names, <c>AllConfigurable</c> allows every member but those
/// <c>NotConfigurable</c> names. Without either, no member may change.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ToolBuilder<TSettings> where TSettings : class, new()
{
    internal ToolBuilder()
    {
    }

    /// <summary>
    /// Allows a member to change at runtime, through stored defaults, an agent's stored settings or a run; every
    /// other member stays as the code sets it. The generator reads the pick; the lambda never runs.
    /// </summary>
    /// <param name="member">The member, <c>s => s.Limit</c>; it needs a type settings can store (AGT014).</param>
    /// <returns>A chain that allows further members.</returns>
    public ConfigurableMembers<TSettings> Configurable(Func<TSettings, object?> member) => new();

    /// <summary>
    /// Allows every settable member to change at runtime, but those <c>NotConfigurable</c> names and obsolete ones.
    /// Each must have a type settings can store (AGT014).
    /// </summary>
    /// <returns>A chain that keeps members out.</returns>
    public AllConfigurableMembers<TSettings> AllConfigurable() => new();
}

/// <summary>
/// The members of a tool's settings that may change at runtime, named one by one.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ConfigurableMembers<TSettings> where TSettings : class, new()
{
    internal ConfigurableMembers()
    {
    }

    /// <summary>
    /// Allows one more member to change at runtime. The generator reads the pick; the lambda never runs.
    /// </summary>
    /// <param name="member">The member, <c>s => s.Limit</c>; it needs a type settings can store (AGT014).</param>
    /// <returns>The same chain, to allow further members.</returns>
    public ConfigurableMembers<TSettings> Configurable(Func<TSettings, object?> member) => this;
}

/// <summary>
/// Every member of a tool's settings may change at runtime, but those named here.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class AllConfigurableMembers<TSettings> where TSettings : class, new()
{
    internal AllConfigurableMembers()
    {
    }

    /// <summary>
    /// Keeps a member as the code sets it. The generator reads the pick; the lambda never runs.
    /// </summary>
    /// <param name="member">The member, <c>s => s.Timeout</c>.</param>
    /// <returns>The same chain, to keep further members out.</returns>
    public AllConfigurableMembers<TSettings> NotConfigurable(Func<TSettings, object?> member) => this;
}
