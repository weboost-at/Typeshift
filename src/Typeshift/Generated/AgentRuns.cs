using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The runs and settings behind an agent's generated host members, with the ids the host was given.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AgentRuns
{
    // D41: a store that conflicts every time would keep UpdateSettingsAsync going forever
    private const int MaxUpdateAttempts = 10;

    /// <summary>
    /// Runs the agent on one message.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
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
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
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
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
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
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(input, settings, ct);

    /// <summary>
    /// Records answers to decisions of the host's conversation and, once no decision is left open, carries them out
    /// and continues the run.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="answers">The answers.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response; with decisions left open, one that lists them and called no model.</returns>
    /// <exception cref="DecisionClosedException">An answer names a decision that is not open.</exception>
    /// <exception cref="ArgumentException">An answer does not fit its decision, or there is none.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static Task<ChatResponse> AnswerAsync<TAgent>(
        AgentHost<TAgent> host,
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.AnswerCoreAsync(answers, settings, ct);

    /// <summary>
    /// Records answers to decisions of the host's conversation and, once no decision is left open, carries them out
    /// and continues the run, asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="answers">The answers.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result; with decisions left open, one that lists them and has none.</returns>
    /// <exception cref="DecisionClosedException">An answer names a decision that is not open.</exception>
    /// <exception cref="ArgumentException">An answer does not fit its decision, or there is none.</exception>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static Task<ChatResponse<TResult>> AnswerAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.AnswerCoreAsync<TResult>(answers, settings, ct);

    /// <summary>
    /// Records answers to decisions of the host's conversation and, once no decision is left open, carries them out
    /// and continues the run, streaming the updates. The answers are recorded when the updates are read.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="answers">The answers.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates; with decisions left open, one update that lists them.</returns>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static IAsyncEnumerable<ChatResponseUpdate> AnswerStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.AnswerStreamingCoreAsync(answers, settings, ct);

    /// <summary>
    /// Records answers to decisions of the host's conversation and, once no decision is left open, carries them out
    /// and continues the run, streaming the updates and asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// The answers are recorded when the updates are read.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="answers">The answers.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The run, which streams its updates and then holds the result.</returns>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static StreamingRun<TResult> AnswerStreamingAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        IReadOnlyList<DecisionAnswer> answers,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.AnswerStreamingCoreAsync<TResult>(answers, settings, ct);

    /// <summary>
    /// Continues the host's conversation without new input: its interrupted run from the last stored step, or the
    /// decisions your app answered. While decisions are still open it calls no model and returns them.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="NothingToResumeException">The conversation has nothing to continue.</exception>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
    public static Task<ChatResponse> ResumeAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(null, settings, ct);

    /// <summary>
    /// Continues the host's conversation without new input, streaming the updates: its interrupted run from the last
    /// stored step, or the decisions your app answered. While decisions are still open it calls no model and sends
    /// them in one update.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="settings">The run's settings, over the stored ones.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    /// <exception cref="InvalidOperationException">The host has no conversation.</exception>
    /// <exception cref="NothingToResumeException">The conversation has nothing to continue.</exception>
    /// <exception cref="ConversationBusyException">A run of the host's conversation is going on.</exception>
    /// <exception cref="ConversationStoreException">The store failed to append a step of the run.</exception>
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
    /// <param name="loadedVersion">
    /// The version <c>LoadSettingsAsync</c> or <c>UpdateSettingsAsync</c> returned, 0 when it loaded none.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new version, to pass to the next save.</returns>
    /// <exception cref="ArgumentException">
    /// The settings set a value no run can go with, such as a blank model or no round of tool calls; nothing is saved.
    /// </exception>
    public static Task<long> SaveSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        TSettings settings,
        long loadedVersion,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>
    {
        EnsureRuns(host.Agent, settings);

        return host.SettingsId is { } id
            ? host.Store.SaveAsync(id, settings, loadedVersion, ct)
            : host.Store.SaveAgentSettingsAsync(settings, loadedVersion, ct);
    }

    // D28, D32: a dry run of the layer over no values but its own, so it refuses what the settings themselves set
    // wrong, whatever the layers below hold. What goes wrong only between two layers fails the run that resolves it
    private static void EnsureRuns(Agent agent, object settings)
    {
        var options = new ResolvedChatOptions(new ChatOptions());
        options.RunOptions = agent.GeneratedDeclaration.ApplySettings(options, new RunOptions(), settings);

        if (options.GetInvalid() is { } invalid)
        {
            throw new ArgumentException(
                $"The settings of the agent '{agent.Name}' set {invalid.Option} {invalid.Value}, and it must "
                + $"{invalid.Must}. No run could go with them, so nothing was saved: set a value that holds, or null "
                + "to keep the layer below.",
                nameof(settings));
        }
    }

    /// <summary>
    /// Updates the agent's own settings, or those under the id the host was given: loads them, saves what
    /// <paramref name="update"/> returns, and loads and updates them again whenever another save came first, up to ten
    /// times.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TSettings">The agent's settings record.</typeparam>
    /// <param name="host">The host, with the run's services and ids.</param>
    /// <param name="update">Returns the changed settings; called again after a lost save.</param>
    /// <param name="ct">Cancels the update.</param>
    /// <returns>The settings saved and their version, which the next save takes.</returns>
    /// <exception cref="StoreConflictException">Another save came first ten times in a row.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="update"/> returned null; nothing is saved.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The changed settings set a value no run can go with; nothing is saved.
    /// </exception>
    public static async Task<Stored<TSettings>> UpdateSettingsAsync<TAgent, TSettings>(
        AgentHost<TAgent> host,
        Func<TSettings, TSettings> update,
        CancellationToken ct)
        where TAgent : Agent
        where TSettings : class, IStorable<TSettings>, new()
    {
        // D41: each conflict means another save won, so the loop ends once no other save comes between
        for (var attempt = 1; ; attempt++)
        {
            var stored = await LoadSettingsAsync<TAgent, TSettings>(host, ct);

            // A null would fail the save with an exception that names neither the agent nor the function
            var updated = update(stored.Data)
                ?? throw new InvalidOperationException(
                    $"The function passed to UpdateSettingsAsync of the agent '{host.Agent.Name}' returned no "
                    + "settings, so nothing was saved: return the settings it was given, changed or not.");

            try
            {
                return new Stored<TSettings>(updated, await SaveSettingsAsync(host, updated, stored.Version, ct));
            }
            catch (StoreConflictException) when (attempt < MaxUpdateAttempts)
            {
                ct.ThrowIfCancellationRequested();
            }
        }
    }
}
