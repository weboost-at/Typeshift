using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The runs and settings behind an agent's generated host members, with the ids the host was given.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AgentRuns
{
    // D41: a store that conflicts every time would keep ConfigureAsync going forever
    private const int MaxConfigureAttempts = 10;

    /// <summary>
    /// Runs the agent on one message.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    public static Task<ChatResponse> RunAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(input, settings, ct);

    /// <summary>
    /// Runs the agent on one message, asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    public static Task<ChatResponse<TResult>> RunAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync<TResult>(input, settings, ct);

    /// <summary>
    /// Runs the agent on one message, streaming the updates and asking for JSON of <typeparamref name="TResult"/>'s
    /// schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    public static StreamingRun<TResult> RunStreamingAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync<TResult>(input, settings, ct);

    /// <summary>
    /// Runs the agent on one message, streaming the updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    public static IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(input, settings, ct);

    /// <summary>
    /// Continues the interrupted run of the host's conversation from its last stored step, without new input.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public static Task<ChatResponse> ResumeAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(null, settings, ct);

    /// <summary>
    /// Continues the interrupted run of the host's conversation from its last stored step, without new input,
    /// streaming the updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    public static IAsyncEnumerable<ChatResponseUpdate> ResumeStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(null, settings, ct);

    /// <summary>
    /// Resolves the options of a run: the agent's code, its stored settings, the run's settings, then its tools.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the resolution.</param>
    /// <returns>The options, for <see cref="AgentHost{TAgent}.CreateChatClientAsync"/>.</returns>
    public static Task<ChatOptions> ResolveOptionsAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.ResolveOptionsCoreAsync(host.SettingsId, settings, ct);

    /// <summary>
    /// Loads the agent's own settings, which every run applies, or those under the id the host was given.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The settings and their version; empty settings at version 0 when none are stored.</returns>
    public static async Task<Stored<TSettings>> LoadSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new() =>
        (host.SettingsId is { } id
            ? await host.Store.LoadAsync<TSettings>(id, ct)
            : await host.Store.LoadAgentSettingsAsync<TSettings>(ct))
        ?? new Stored<TSettings>(new TSettings(), 0);

    /// <summary>
    /// Saves the agent's own settings, or those under the id the host was given, if the stored version is still the one
    /// loaded.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="loadedVersion">The version <c>LoadSettingsAsync</c> returned, 0 when it loaded none.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new version, to pass to the next save.</returns>
    public static Task<long> SaveSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        TSettings settings,
        long loadedVersion,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings> =>
        host.SettingsId is { } id
            ? host.Store.SaveAsync(id, settings, loadedVersion, ct)
            : host.Store.SaveAgentSettingsAsync(settings, loadedVersion, ct);

    /// <summary>
    /// Changes the agent's own settings, or those under the id the host was given, reloading and changing them again
    /// whenever another save came first, up to ten times.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="configure">Returns the changed settings; called again after a lost save.</param>
    /// <param name="ct">Cancels the change.</param>
    /// <returns>The settings saved.</returns>
    /// <exception cref="StoreConflictException">Another save came first ten times in a row.</exception>
    public static async Task<TSettings> ConfigureAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        Func<TSettings, TSettings> configure,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new()
    {
        // D41: each conflict means another save won, so the loop ends once no other save comes between
        for (var attempt = 1; ; attempt++)
        {
            var stored = await LoadSettingsAsync<TAgent, TSettings>(host, ct);
            var configured = configure(stored.Data);

            try
            {
                await SaveSettingsAsync(host, configured, stored.Version, ct);
                return configured;
            }
            catch (StoreConflictException) when (attempt < MaxConfigureAttempts)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
