using Agentic.Tools;

namespace Agentic.Settings;

/// <summary>
/// The stored defaults of a tool for every agent without code settings for it, one document per tool.
/// </summary>
public sealed record ToolDefaults(ToolName Tool)
{
    /// <summary>
    /// The stored values; only the tool's allowlisted members apply.
    /// </summary>
    public StoredSettings Settings { get; init; } = StoredSettings.Empty;

    /// <summary>
    /// When the defaults were last stored.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
