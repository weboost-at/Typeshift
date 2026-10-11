using System.Collections.Immutable;
using System.Text.Json;
using Agentic.Settings;
using Agentic.Tools;

namespace Agentic.Agents;

/// <summary>
/// The base of the generated views over an agent's stored configuration: the model, then one nullable property
/// per allowlisted chat option and one view per tool with allowlisted settings.
/// </summary>
public abstract class AgentConfigurationView
{
    private readonly Dictionary<ToolName, StoredSettingsView> _tools = [];
    private readonly JsonSerializerOptions _options;
    private AgentConfiguration _configuration;

    /// <summary>
    /// Creates the view over the stored configuration.
    /// </summary>
    protected AgentConfigurationView(AgentConfiguration configuration, JsonSerializerOptions options)
    {
        _configuration = configuration;
        _options = options;
    }

    /// <summary>
    /// The model the agent runs on; null leaves the agent without one.
    /// </summary>
    public string? Model
    {
        get => _configuration.Model;
        set => _configuration = _configuration with { Model = value };
    }

    /// <summary>
    /// The stored configuration as changed through the view.
    /// </summary>
    public AgentConfiguration Stored =>
        _configuration with
        {
            ToolSettings = _tools.Aggregate(
                _configuration.ToolSettings.ToImmutableDictionary(),
                (settings, tool) => settings.SetItem(tool.Key, tool.Value.Stored))
        };

    /// <summary>
    /// The stored value of a chat option, or null when it inherits.
    /// </summary>
    protected T? GetChatOption<T>(string name) => _configuration.ChatOptions.GetValue<T>(name, _options);

    /// <summary>
    /// Stores a chat option, or removes it when the value is null so the option inherits again.
    /// </summary>
    protected void SetChatOption<T>(string name, T? value) =>
        _configuration = _configuration with
        {
            ChatOptions = _configuration.ChatOptions.WithValue(name, value, _options)
        };

    /// <summary>
    /// The view over this agent's stored settings of one tool.
    /// </summary>
    protected TView GetToolSettings<TView>() where TView : StoredSettingsView, IStoredSettingsView<TView>
    {
        if (_tools.TryGetValue(TView.Tool, out var view)) return (TView)view;

        var stored = _configuration.ToolSettings.GetValueOrDefault(TView.Tool) ?? StoredSettings.Empty;
        var created = TView.Create(stored, _options);

        _tools[TView.Tool] = created;
        return created;
    }
}
