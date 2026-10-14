using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The runs and settings behind an agent's generated host members. Ids are of the build's id type, boxed.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AgentRuns
{
    /// <summary>
    /// Runs the agent on one message.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    public static Task<ChatResponse> RunAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(input, settingsId, settings, ct);

    /// <summary>
    /// Runs the agent on one message, asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    public static Task<ChatResponse<TResult>> RunAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync<TResult>(input, settingsId, settings, ct);

    /// <summary>
    /// Runs the agent on one message, streaming the updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    public static IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(input, settingsId, settings, ct);

    /// <summary>
    /// Resolves the options of a run: the agent's code, its stored settings, the run's settings, then its tools.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the resolution.</param>
    /// <returns>The options, for <see cref="AgentHost{TAgent}.CreateChatClientAsync"/>.</returns>
    public static Task<ChatOptions> ResolveOptionsAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.ResolveOptionsCoreAsync(settingsId, settings, ct);

    /// <summary>
    /// Loads the settings stored under an id.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The settings and their revision; empty settings at revision 0 when none are stored.</returns>
    public static async Task<Stored<TSettings>> LoadSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new() =>
        await host.Store.LoadAsync<TSettings>(settingsId, ct) ?? new Stored<TSettings>(new TSettings(), 0);

    /// <summary>
    /// Saves settings under an id if the stored revision is still the one loaded.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="revision">The revision <c>LoadSettingsAsync</c> returned.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new revision.</returns>
    public static Task<long> SaveSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        TSettings settings,
        long revision,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings> =>
        host.Store.SaveAsync(settingsId, settings, revision, ct);

    /// <summary>
    /// Changes the settings stored under an id, reloading and changing them again whenever another save came first.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id.</param>
    /// <param name="configure">Returns the changed settings; called again after a lost save.</param>
    /// <param name="ct">Cancels the change.</param>
    /// <returns>The settings saved.</returns>
    public static async Task<TSettings> ConfigureAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        Func<TSettings, TSettings> configure,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new()
    {
        // D41: each conflict means another save won, so the loop ends once no other save comes between
        while (true)
        {
            var stored = await LoadSettingsAsync<TAgent, TSettings>(host, settingsId, ct);
            var configured = configure(stored.Data);

            try
            {
                await host.Store.SaveAsync(settingsId, configured, stored.Revision, ct);
                return configured;
            }
            catch (StoreConflictException)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
