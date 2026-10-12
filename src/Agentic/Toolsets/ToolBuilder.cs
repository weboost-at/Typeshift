using Agentic.Settings;
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
    /// <exception cref="ArgumentException">The name is not lower_snake_case.</exception>
    public ToolBuilder WithName(string name)
    {
        if (!LowerSnakeCase.Matches(name))
            throw new ArgumentException($"'{name}' is not a lower_snake_case name.", nameof(name));

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
    /// Gives the tool a settings record, read inside the tool with <c>GetToolSettings</c>.
    /// </summary>
    /// <typeparam name="TSettings">The settings record; its parameterless constructor sets the code defaults.</typeparam>
    /// <param name="configure">
    /// Picks the members that may change at runtime, <c>settings => settings.Configurable(s => s.Limit)</c>. The
    /// generator reads the picks into the tool's overrides; at runtime the picker is never called.
    /// </param>
    /// <returns>The same builder, to name and describe the tool further.</returns>
    public ToolBuilder WithSettings<TSettings>(Action<ConfigurableSettingsPicker<TSettings>>? configure = null)
        where TSettings : class, new()
    {
        _definition.SettingsType = typeof(TSettings);
        return this;
    }
}
