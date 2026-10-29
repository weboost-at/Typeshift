using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The runs and settings behind an agent's generated host members. Ids are of the build's id type, boxed.
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
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="conversationId">The id of the conversation to resume and store the run in, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    public static Task<ChatResponse> RunAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(input, settingsId, conversationId, settings, ct);

    /// <summary>
    /// Runs the agent on one message, asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="conversationId">The id of the conversation to resume and store the run in, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    public static Task<ChatResponse<TResult>> RunAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync<TResult>(input, settingsId, conversationId, settings, ct);

    /// <summary>
    /// Runs the agent on one message, streaming the updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="conversationId">The id of the conversation to resume and store the run in, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    public static IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settingsId,
        object? conversationId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(input, settingsId, conversationId, settings, ct);

    /// <summary>
    /// Continues a conversation's interrupted run from its last stored step, without new input.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="conversationId">The conversation's id.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    public static Task<ChatResponse> ResumeAsync<TAgent>(
        AgentHost<TAgent> host,
        object conversationId,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(null, settingsId, conversationId, settings, ct);

    /// <summary>
    /// Continues a conversation's interrupted run from its last stored step, without new input, streaming the
    /// updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="conversationId">The conversation's id.</param>
    /// <param name="settingsId">The id of the stored settings to apply, or null.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    public static IAsyncEnumerable<ChatResponseUpdate> ResumeStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        object conversationId,
        object? settingsId,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(null, settingsId, conversationId, settings, ct);

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
    /// <returns>The settings and their version; empty settings at version 0 when none are stored.</returns>
    public static async Task<Stored<TSettings>> LoadSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new() =>
        await host.Store.LoadAsync<TSettings>(settingsId, ct) ?? new Stored<TSettings>(new TSettings(), 0);

    /// <summary>
    /// Saves settings under an id if the stored version is still the one loaded.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="loadedVersion">The version <c>LoadSettingsAsync</c> returned, 0 when it loaded none.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new version, to pass to the next save.</returns>
    public static Task<long> SaveSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        TSettings settings,
        long loadedVersion,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings> =>
        host.Store.SaveAsync(settingsId, settings, loadedVersion, ct);

    /// <summary>
    /// Changes the settings stored under an id, reloading and changing them again whenever another save came first, up
    /// to ten times.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settingsId">The id.</param>
    /// <param name="configure">Returns the changed settings; called again after a lost save.</param>
    /// <param name="ct">Cancels the change.</param>
    /// <returns>The settings saved.</returns>
    /// <exception cref="StoreConflictException">Another save came first ten times in a row.</exception>
    public static async Task<TSettings> ConfigureAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        object settingsId,
        Func<TSettings, TSettings> configure,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new()
    {
        // D41: each conflict means another save won, so the loop ends once no other save comes between
        for (var attempt = 1; ; attempt++)
        {
            var stored = await LoadSettingsAsync<TAgent, TSettings>(host, settingsId, ct);
            var configured = configure(stored.Data);

            try
            {
                await host.Store.SaveAsync(settingsId, configured, stored.Version, ct);
                return configured;
            }
            catch (StoreConflictException) when (attempt < MaxConfigureAttempts)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
