using Agentic.Json;
using Agentic.Persistence;
using Agentic.Tools;

namespace Agentic.Settings;

/// <summary>
/// Loads and stores the global defaults of tools, through the registered <see cref="IToolDefaultsStore"/>.
/// </summary>
public sealed class ToolDefaultsManager
{
    private readonly IToolDefaultsStore? _store;
    private readonly AgentJson _json;
    private readonly TimeProvider _time;

    internal ToolDefaultsManager(IToolDefaultsStore? store, AgentJson json, TimeProvider time)
    {
        _store = store;
        _json = json;
        _time = time;
    }

    /// <summary>
    /// The stored defaults of the tool, empty while nothing is stored or no store is registered.
    /// </summary>
    public async Task<ToolDefaults> LoadAsync(ToolName tool, CancellationToken ct = default)
    {
        var stored = await LoadAsync([tool], ct);
        return stored.GetValueOrDefault(tool) ?? new ToolDefaults(tool);
    }

    /// <summary>
    /// Changes the stored defaults of the tool and stores them.
    /// </summary>
    public async Task UpdateAsync(
        ToolName tool,
        Func<StoredSettings, StoredSettings> update,
        CancellationToken ct = default)
    {
        if (_store is null)
            throw new InvalidOperationException(
                "No IToolDefaultsStore is registered: register one to store tool defaults.");

        var defaults = await LoadAsync(tool, ct);

        var updated = defaults with
        {
            Settings = update(defaults.Settings),
            UpdatedAt = _time.GetUtcNow()
        };

        await _store.SaveAsync(updated, ct);
    }

    /// <summary>
    /// Changes the stored defaults of the tool through the typed view of its allowlisted members.
    /// </summary>
    public Task ConfigureAsync<TSettings, TConfiguration>(
        ToolRef<TSettings, TConfiguration> tool,
        Action<TConfiguration> configure,
        CancellationToken ct = default)
        where TSettings : class, new()
        where TConfiguration : StoredSettingsView, IStoredSettingsView<TConfiguration> =>
        UpdateAsync(tool.Name, stored => Configure(stored, configure), ct);

    internal async Task<IReadOnlyDictionary<ToolName, ToolDefaults>> LoadAsync(
        IReadOnlyCollection<ToolName> tools,
        CancellationToken ct) =>
        _store is null ? new Dictionary<ToolName, ToolDefaults>() : await _store.LoadAsync(tools, ct);

    private StoredSettings Configure<TConfiguration>(StoredSettings stored, Action<TConfiguration> configure)
        where TConfiguration : StoredSettingsView, IStoredSettingsView<TConfiguration>
    {
        var view = TConfiguration.Create(stored, _json.SerializerOptions);
        configure(view);
        return view.Stored;
    }
}
