using Microsoft.Extensions.AI;

namespace Typeshift.Agents;

/// <summary>
/// Sets the chat options an agent runs with and marks those that may change at runtime, in a chain its constructor
/// starts with <c>WithChatOptions()</c>. The agent's instructions come from <c>GetInstructions()</c>.
/// </summary>
public sealed class ChatOptionsBuilder
{
    private readonly ChatOptions _options;

    internal ChatOptionsBuilder(ChatOptions options) => _options = options;

    /// <summary>
    /// Sets the model the agent runs on by default, as the registered chat client provider names it.
    /// </summary>
    /// <param name="model">The model's name; the agent's settings may override it.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithModel(string model) => With(options => options.ModelId = model);

    /// <summary>
    /// Sets the sampling temperature.
    /// </summary>
    /// <param name="temperature">The temperature.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithTemperature(float temperature) => With(options => options.Temperature = temperature);

    /// <summary>
    /// Sets the most tokens a response may hold.
    /// </summary>
    /// <param name="tokens">The most tokens.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithMaxOutputTokens(int tokens) => With(options => options.MaxOutputTokens = tokens);

    /// <summary>
    /// Sets the nucleus sampling probability.
    /// </summary>
    /// <param name="topP">The probability mass to sample from.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithTopP(float topP) => With(options => options.TopP = topP);

    /// <summary>
    /// Sets how many of the most likely tokens are sampled from.
    /// </summary>
    /// <param name="topK">The number of tokens.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithTopK(int topK) => With(options => options.TopK = topK);

    /// <summary>
    /// Sets the penalty for tokens by how often they already appear.
    /// </summary>
    /// <param name="penalty">The penalty.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithFrequencyPenalty(float penalty) =>
        With(options => options.FrequencyPenalty = penalty);

    /// <summary>
    /// Sets the penalty for tokens that already appear.
    /// </summary>
    /// <param name="penalty">The penalty.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithPresencePenalty(float penalty) => With(options => options.PresencePenalty = penalty);

    /// <summary>
    /// Sets the seed for repeatable sampling.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithSeed(long seed) => With(options => options.Seed = seed);

    /// <summary>
    /// Sets how much the model reasons before it answers.
    /// </summary>
    /// <param name="reasoning">The reasoning options.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithReasoning(ReasoningOptions reasoning) =>
        With(options => options.Reasoning = reasoning);

    /// <summary>
    /// Sets the format of the responses, such as JSON.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithResponseFormat(ChatResponseFormat format) =>
        With(options => options.ResponseFormat = format);

    /// <summary>
    /// Sets the sequences that end a response.
    /// </summary>
    /// <param name="sequences">The sequences.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithStopSequences(params string[] sequences) =>
        With(options => options.StopSequences = [.. sequences]);

    /// <summary>
    /// Sets whether and which tools the model must call.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithToolMode(ChatToolMode mode) => With(options => options.ToolMode = mode);

    /// <summary>
    /// Sets whether the model may call several tools in one response.
    /// </summary>
    /// <param name="allow">Whether it may.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithAllowMultipleToolCalls(bool allow) =>
        With(options => options.AllowMultipleToolCalls = allow);

    /// <summary>
    /// Adds tools that belong to no toolset, such as a <see cref="HostedWebSearchTool"/>; they come before the tools
    /// of the agent's toolsets.
    /// </summary>
    /// <param name="tools">The tools.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithTools(params AITool[] tools) =>
        With(options => options.Tools = [.. options.Tools ?? [], .. tools]);

    /// <summary>
    /// Sets a provider-specific option.
    /// </summary>
    /// <param name="key">The option's key.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder WithAdditionalProperty(string key, object? value) =>
        With(options => (options.AdditionalProperties ??= [])[key] = value);

    /// <summary>
    /// Allows an option to change at runtime, through the agent's stored settings or a run's; every other option
    /// stays as the code sets it. The generator reads the pick into the agent's <c>Settings</c>; the lambda never
    /// runs.
    /// </summary>
    /// <param name="option">The option, <c>o => o.Temperature</c>; it needs a type settings can store (TSH014).</param>
    /// <returns>The same builder.</returns>
    public ChatOptionsBuilder Configurable(Func<ChatOptions, object?> option) => this;

    private ChatOptionsBuilder With(Action<ChatOptions> set)
    {
        set(_options);
        return this;
    }
}
