using Typeshift.Tools;

namespace Typeshift.Toolsets;

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
    public ToolBuilder WithDescription(string description) => this;

    /// <summary>
    /// Gives the tool a settings record whose members stay as the code sets them, but those <c>Configurable</c>
    /// names. The tool method receives the resolved settings through a parameter of their type, which the model never
    /// sees.
    /// </summary>
    /// <typeparam name="TSettings">The settings record; its parameterless constructor sets the code defaults.</typeparam>
    /// <returns>A builder that allows members to change at runtime.</returns>
    public ToolBuilder<TSettings> WithSettings<TSettings>() where TSettings : class, new() => new();

    /// <summary>
    /// Gives the tool a settings record whose members may all change at runtime, but obsolete ones and those
    /// <c>NotConfigurable</c> names. Each must have a type settings can store (TSH014). The tool method receives the
    /// resolved settings through a parameter of their type, which the model never sees.
    /// </summary>
    /// <typeparam name="TSettings">The settings record; its parameterless constructor sets the code defaults.</typeparam>
    /// <returns>A builder that keeps members as the code sets them.</returns>
    public ConfigurableSettingsBuilder<TSettings> WithConfigurableSettings<TSettings>()
        where TSettings : class, new() =>
        new();
}

/// <summary>
/// Allows members of a tool's settings to change at runtime, in a chain the generator reads at compile time; members
/// it does not name stay as the code sets them.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ToolBuilder<TSettings> where TSettings : class, new()
{
    internal ToolBuilder()
    {
    }

    /// <summary>
    /// Allows a member to change at runtime, through stored defaults, an agent's stored settings or a run. The
    /// generator reads the pick; the lambda never runs.
    /// </summary>
    /// <param name="member">The member, <c>s => s.Limit</c>; it needs a type settings can store (TSH014).</param>
    /// <returns>The same builder, to allow further members.</returns>
    public ToolBuilder<TSettings> Configurable(Func<TSettings, object?> member) => this;
}

/// <summary>
/// Keeps members of a tool's settings as the code sets them, in a chain the generator reads at compile time; members
/// it does not name may change at runtime.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ConfigurableSettingsBuilder<TSettings> where TSettings : class, new()
{
    internal ConfigurableSettingsBuilder()
    {
    }

    /// <summary>
    /// Keeps a member as the code sets it. The generator reads the pick; the lambda never runs.
    /// </summary>
    /// <param name="member">The member, <c>s => s.Timeout</c>.</param>
    /// <returns>The same builder, to keep further members.</returns>
    public ConfigurableSettingsBuilder<TSettings> NotConfigurable(Func<TSettings, object?> member) => this;
}
