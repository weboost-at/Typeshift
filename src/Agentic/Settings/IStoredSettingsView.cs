using System.Text.Json;
using Agentic.Tools;

namespace Agentic.Settings;

/// <summary>
/// A generated typed view over a tool's stored settings, created without reflection.
/// </summary>
public interface IStoredSettingsView<TSelf> where TSelf : StoredSettingsView
{
    /// <summary>
    /// The tool whose settings the view shows.
    /// </summary>
    static abstract ToolName Tool { get; }

    /// <summary>
    /// Creates the view over the stored settings.
    /// </summary>
    static abstract TSelf Create(StoredSettings stored, JsonSerializerOptions options);
}
