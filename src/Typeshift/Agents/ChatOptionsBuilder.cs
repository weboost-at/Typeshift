using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The chat options an agent starts every run from, and those that may change at runtime.
/// </summary>
public sealed class ChatOptionsBuilder
{
    private readonly Agent _agent;

    internal ChatOptionsBuilder(Agent agent) => _agent = agent;

    /// <summary>
    /// Names the provider that serves the agent's runs, by its key: the client registered under the key, as
    /// <c>services.AddKeyedChatClient(key, client)</c> registers one, serves them. Without it the default provider
    /// does. The call site's <c>host.WithProvider(...)</c> names another, and so do the settings' <c>Provider</c>
    /// once <c>Configurable(o => o.ModelId)</c> makes them configurable.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#the-provider-id-type">The provider id type</see>.
    /// </summary>
    /// <typeparam name="TProviderId">
    /// The agent's provider id type, such as <see cref="string"/> or an enum of yours. When its <c>Settings</c> store
    /// the provider, a string, a number, a <see cref="Guid"/> or an enum (TSH029).
    /// </typeparam>
    /// <param name="provider">The provider's key.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="provider"/> is text that is white space.</exception>
    public ChatOptionsBuilder WithProvider<TProviderId>(TProviderId provider) where TProviderId : notnull
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider is string text) ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(provider));

        // D86: typed here, where the provider's type is known, so no type is made at run time
        _agent.Provider = new ProviderId<TProviderId>(provider);
        return this;
    }

    /// <summary>
    /// Sets the model, as the API of the provider that serves the run names it. Who serves it is a choice of its own:
    /// <see cref="WithProvider{TProviderId}"/>, the settings, the call site or the default provider.
    /// <c>Configurable(o => o.ModelId)</c> lets stored settings or a run choose another model, or name a provider.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> is null or white space.</exception>
    public ChatOptionsBuilder WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return Set(options => options.ModelId = model);
    }

    /// <summary>
    /// Sets the sampling temperature.
    /// </summary>
    /// <param name="temperature">The temperature.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithTemperature(float temperature) =>
        Set(options => options.Temperature = temperature);

    /// <summary>
    /// Sets the most tokens a response may hold.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithMaxOutputTokens(int tokens) => Set(options => options.MaxOutputTokens = tokens);

    /// <summary>
    /// Sets nucleus sampling.
    /// </summary>
    /// <param name="topP">The probability mass to sample from.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithTopP(float topP) => Set(options => options.TopP = topP);

    /// <summary>
    /// Sets how many of the likeliest tokens are sampled from.
    /// </summary>
    /// <param name="topK">The tokens.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithTopK(int topK) => Set(options => options.TopK = topK);

    /// <summary>
    /// Sets the penalty on tokens by how often they appeared.
    /// </summary>
    /// <param name="penalty">The penalty.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithFrequencyPenalty(float penalty) =>
        Set(options => options.FrequencyPenalty = penalty);

    /// <summary>
    /// Sets the penalty on tokens that appeared at all.
    /// </summary>
    /// <param name="penalty">The penalty.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithPresencePenalty(float penalty) => Set(options => options.PresencePenalty = penalty);

    /// <summary>
    /// Sets the seed of providers that sample reproducibly.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithSeed(long seed) => Set(options => options.Seed = seed);

    /// <summary>
    /// Sets how much the model reasons and what of it the response shows.
    /// </summary>
    /// <param name="reasoning">The reasoning options.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithReasoning(ReasoningOptions reasoning) =>
        Set(options => options.Reasoning = reasoning);

    /// <summary>
    /// Sets the format of the response: text, any JSON or JSON of a schema.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithResponseFormat(ChatResponseFormat format) =>
        Set(options => options.ResponseFormat = format);

    /// <summary>
    /// Sets the sequences that end a response.
    /// </summary>
    /// <param name="sequences">The sequences.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithStopSequences(params string[] sequences) =>
        Set(options => options.StopSequences = [.. sequences]);

    /// <summary>
    /// Sets whether and which tools the model must call.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithToolMode(ChatToolMode mode) => Set(options => options.ToolMode = mode);

    /// <summary>
    /// Sets whether one response may call several tools.
    /// </summary>
    /// <param name="allow">Whether it may.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithAllowMultipleToolCalls(bool allow) =>
        Set(options => options.AllowMultipleToolCalls = allow);

    /// <summary>
    /// Adds tools of no toolset, such as a <see cref="HostedWebSearchTool"/> or a <see cref="HostedMcpServerTool"/>.
    /// </summary>
    /// <param name="tools">The tools.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithTools(params AITool[] tools) =>
        Set(options => options.Tools = [.. options.Tools ?? [], .. tools]);

    /// <summary>
    /// Adds tools of no toolset that only read, such as a <see cref="HostedWebSearchTool"/>, as
    /// <see cref="WithTools"/> does, and marks them so plan mode offers them. The provider receives each tool as it
    /// is given.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/tool-approval#plan-mode">Plan mode</see>.
    /// </summary>
    /// <param name="tools">The tools.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithReadOnlyTools(params AITool[] tools)
    {
        // D52: the agent knows the tools themselves, so no wrapper hides a hosted tool from the provider's client
        _agent.ReadOnlyTools.UnionWith(tools);
        return WithTools(tools);
    }

    /// <summary>
    /// Sets a provider-specific option.
    /// </summary>
    /// <typeparam name="TValue">The option's type.</typeparam>
    /// <param name="key">The option's key.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithAdditionalProperty<TValue>(string key, TValue value) =>
        Set(options => (options.AdditionalProperties ??= [])[key] = value);

    // D5: the generator reads the pick; at runtime the call declares nothing
    /// <summary>
    /// Lets an option change at runtime, whether the code sets it or not. The lambda never runs.
    /// </summary>
    /// <typeparam name="TOption">The option's type.</typeparam>
    /// <param name="option">The option, <c>o => o.Temperature</c>.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder Configurable<TOption>(Func<ChatOptions, TOption> option) => this;

    private ChatOptionsBuilder Set(Action<ChatOptions> set)
    {
        set(_agent.ChatOptions);
        return this;
    }
}
