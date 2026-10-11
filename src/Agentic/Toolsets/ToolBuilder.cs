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
    /// Names the tool; the name is a lower_snake_case constant.
    /// </summary>
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
    public ToolBuilder WithDescription(string description)
    {
        _definition.Description = description;
        return this;
    }

    /// <summary>
    /// Gives the tool a settings record, read with <c>GetToolSettings</c>, and picks its configurable members.
    /// </summary>
    public ToolBuilder WithSettings<TSettings>(Action<ConfigurableSettingsPicker<TSettings>>? configure = null)
        where TSettings : class, new()
    {
        var picker = new ConfigurableSettingsPicker<TSettings>();
        configure?.Invoke(picker);

        _definition.SettingsType = typeof(TSettings);
        _definition.ConfigurableSettings = picker.Picked;
        return this;
    }
}
