using Typeshift.Json;
using Typeshift.Persistence;
using Typeshift.Settings;
using Typeshift.Toolsets;
using Microsoft.Extensions.AI;

namespace Typeshift.Tools;

/// <summary>
/// What the generator binds a tool to: the function that runs it and, when it has settings, how they resolve.
/// </summary>
public abstract class ToolBinding
{
    private protected ToolBinding()
    {
    }

    /// <summary>
    /// Binds a tool without settings.
    /// </summary>
    /// <param name="createFunction">Creates the tool's function.</param>
    /// <returns>The binding.</returns>
    public static ToolBinding Create(Func<ToolDefinition, AgentJson, AIFunction> createFunction) =>
        new PlainToolBinding(createFunction);

    /// <summary>
    /// Binds a tool whose settings have no configurable members, so only code sets them.
    /// </summary>
    /// <typeparam name="TSettings">The settings record of the tool.</typeparam>
    /// <param name="createFunction">Creates the tool's function, running with the resolved settings.</param>
    /// <returns>The binding.</returns>
    public static ToolBinding Create<TSettings>(Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
        where TSettings : class, new() =>
        new SettingsToolBinding<TSettings>(createFunction);

    /// <summary>
    /// Binds a tool whose settings have configurable members, overridden through <typeparamref name="TOverrides"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings record of the tool.</typeparam>
    /// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
    /// <param name="createFunction">Creates the tool's function, running with the resolved settings.</param>
    /// <returns>The binding.</returns>
    public static ToolBinding Create<TSettings, TOverrides>(
        Func<ToolDefinition, TSettings, AgentJson, AIFunction> createFunction)
        where TSettings : class, new()
        where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new() =>
        new OverridableToolBinding<TSettings, TOverrides>(createFunction);

    internal abstract AIFunction CreateFunction(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json);

    // Stored defaults exist only for tools with configurable members
    internal virtual Task<IToolOverrides?> LoadDefaultsAsync<TId>(
        ITypeshiftStore<TId> store,
        TId id,
        CancellationToken ct)
        where TId : notnull =>
        Task.FromResult<IToolOverrides?>(null);
}
