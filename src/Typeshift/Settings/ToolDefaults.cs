using Typeshift.Persistence;
using Typeshift.Tools;

namespace Typeshift.Settings;

/// <summary>
/// Stores the defaults of a tool's configurable settings. They apply to every agent that runs under the same id
/// and does not set the tool's settings in code.
/// </summary>
public sealed class ToolDefaults
{
    private readonly IServiceProvider _services;

    internal ToolDefaults(IServiceProvider services) => _services = services;

    /// <summary>
    /// Loads the defaults stored for a tool.
    /// </summary>
    /// <typeparam name="TSettings">The settings record of the tool.</typeparam>
    /// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id the defaults were saved under.</param>
    /// <param name="tool">The tool, as its toolset's <c>Tools</c> class names it.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The stored defaults, or empty overrides when none are stored.</returns>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    public async Task<TOverrides> LoadAsync<TSettings, TOverrides, TId>(
        TId id,
        ToolRef<TSettings, TOverrides> tool,
        CancellationToken ct = default)
        where TSettings : class, new()
        where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
        where TId : notnull =>
        await Stores.Get<TId>(_services).LoadAsync<TOverrides>(id, ct) ?? new TOverrides();

    /// <summary>
    /// Saves the defaults of a tool, replacing what was stored under the id.
    /// </summary>
    /// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id to save the defaults under.</param>
    /// <param name="defaults">The defaults; a null value leaves the code default in place.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    public Task SaveAsync<TOverrides, TId>(TId id, TOverrides defaults, CancellationToken ct = default)
        where TOverrides : class, IToolOverrides, IStorable<TOverrides>
        where TId : notnull =>
        Stores.Get<TId>(_services).SaveAsync(id, defaults, ct);

    /// <summary>
    /// Changes the defaults stored for a tool and saves them.
    /// </summary>
    /// <typeparam name="TSettings">The settings record of the tool.</typeparam>
    /// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
    /// <typeparam name="TId">The id type of the registered store.</typeparam>
    /// <param name="id">The id the defaults are stored under.</param>
    /// <param name="tool">The tool, as its toolset's <c>Tools</c> class names it.</param>
    /// <param name="configure">Returns the changed defaults, <c>defaults => defaults with { Limit = 20 }</c>.</param>
    /// <param name="ct">Cancels the load and the save.</param>
    /// <returns>The saved defaults.</returns>
    /// <exception cref="InvalidOperationException">
    /// No store with ids of <typeparamref name="TId"/> is registered.
    /// </exception>
    public async Task<TOverrides> ConfigureAsync<TSettings, TOverrides, TId>(
        TId id,
        ToolRef<TSettings, TOverrides> tool,
        Func<TOverrides, TOverrides> configure,
        CancellationToken ct = default)
        where TSettings : class, new()
        where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
        where TId : notnull
    {
        var configured = configure(await LoadAsync(id, tool, ct));
        await SaveAsync(id, configured, ct);
        return configured;
    }
}
