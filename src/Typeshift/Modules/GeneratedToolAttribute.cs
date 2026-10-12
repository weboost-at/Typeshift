namespace Typeshift.Modules;

/// <summary>
/// Describes one generated tool on its toolset, so other compilations read the tools from metadata.
/// </summary>
/// <param name="name">The tool's name.</param>
/// <param name="settings">The settings record the tool reads, if any.</param>
/// <param name="configurable">The settings members that may change at runtime.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GeneratedToolAttribute(string name, Type? settings, params string[] configurable) : Attribute
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The settings record the tool reads, if any.
    /// </summary>
    public Type? Settings { get; } = settings;

    /// <summary>
    /// The settings members that may change at runtime; the tool has generated overrides when there are any.
    /// </summary>
    public IReadOnlyList<string> Configurable { get; } = configurable;
}
