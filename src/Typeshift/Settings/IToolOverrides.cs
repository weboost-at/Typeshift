using Typeshift.Tools;

namespace Typeshift.Settings;

/// <summary>
/// Values that override the configurable members of one tool's settings; the generator writes one record per tool
/// whose settings have configurable members, named after the tool, <c>FindCompaniesOverrides</c> for example.
/// </summary>
public interface IToolOverrides
{
    /// <summary>
    /// The tool whose settings these values override.
    /// </summary>
    ToolName Tool { get; }
}

/// <summary>
/// Values that override the configurable members of a tool's <typeparamref name="TSettings"/>; a null value keeps
/// what the layer below sets.
/// </summary>
/// <typeparam name="TSettings">The settings record of the tool.</typeparam>
public interface IToolOverrides<TSettings> : IToolOverrides where TSettings : class
{
    /// <summary>
    /// Applies the values that are set onto a copy of <paramref name="settings"/>.
    /// </summary>
    /// <param name="settings">The settings of the layer below.</param>
    /// <returns>A copy of the settings with every value that is set applied.</returns>
    TSettings ApplyTo(TSettings settings);
}
