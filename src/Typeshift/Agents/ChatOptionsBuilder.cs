using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The chat options an agent starts every run from, and those that may change at runtime.
/// </summary>
public sealed class ChatOptionsBuilder
{
    private readonly ChatOptions _options;

    internal ChatOptionsBuilder(ChatOptions options) => _options = options;

    /// <summary>
    /// Sets the model, as the host's <see cref="IChatClientProvider"/> names it. <c>Configurable(o => o.ModelId)</c>
    /// lets stored settings or a run choose another.
    /// </summary>
    /// <param name="model">The model's name.</param>
    /// <returns>The same chain.</returns>
    public ChatOptionsBuilder WithModel(string model) => Set(options => options.ModelId = model);

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
        set(_options);
        return this;
    }
}
