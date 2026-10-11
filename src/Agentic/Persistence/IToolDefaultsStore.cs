using Agentic.Settings;
using Agentic.Tools;

namespace Agentic.Persistence;

/// <summary>
/// Stores global tool defaults; optional, register an implementation as a singleton to persist them.
/// </summary>
public interface IToolDefaultsStore
{
    /// <summary>
    /// The stored defaults of those of the given tools that have any.
    /// </summary>
    Task<IReadOnlyDictionary<ToolName, ToolDefaults>> LoadAsync(
        IReadOnlyCollection<ToolName> tools,
        CancellationToken ct);

    /// <summary>
    /// Stores the defaults, replacing the ones stored for the same tool.
    /// </summary>
    Task SaveAsync(ToolDefaults defaults, CancellationToken ct);
}
