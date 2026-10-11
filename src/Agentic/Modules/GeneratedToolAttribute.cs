namespace Agentic.Modules;

/// <summary>
/// Describes one generated tool on its toolset, so other compilations read the tools from metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GeneratedToolAttribute(string name, string method, Type? settings, params string[] configurable)
    : Attribute
{
    /// <summary>
    /// The tool name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The name of the method the tool runs.
    /// </summary>
    public string Method { get; } = method;

    /// <summary>
    /// The settings record the tool reads, if any.
    /// </summary>
    public Type? Settings { get; } = settings;

    /// <summary>
    /// The settings members that may change at runtime.
    /// </summary>
    public IReadOnlyList<string> Configurable { get; } = configurable;
}
