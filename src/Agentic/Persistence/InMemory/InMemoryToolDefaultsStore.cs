using System.Collections.Concurrent;
using Agentic.Settings;
using Agentic.Tools;

namespace Agentic.Persistence.InMemory;

/// <summary>
/// Keeps tool defaults in memory, for tests, samples and single-process hosts.
/// </summary>
public sealed class InMemoryToolDefaultsStore : IToolDefaultsStore
{
    private readonly ConcurrentDictionary<ToolName, ToolDefaults> _defaults = [];

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<ToolName, ToolDefaults>> LoadAsync(
        IReadOnlyCollection<ToolName> tools,
        CancellationToken ct)
    {
        IReadOnlyDictionary<ToolName, ToolDefaults> stored = tools
            .Where(_defaults.ContainsKey)
            .ToDictionary(tool => tool, tool => _defaults[tool]);

        return Task.FromResult(stored);
    }

    /// <inheritdoc />
    public Task SaveAsync(ToolDefaults defaults, CancellationToken ct)
    {
        _defaults[defaults.Tool] = defaults;
        return Task.CompletedTask;
    }
}
