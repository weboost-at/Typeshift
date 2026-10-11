using Agentic.Agents;
using Microsoft.Extensions.AI;

namespace Agentic.StoredAgents;

/// <summary>
/// The chat options a stored agent supports: a stable storage contract, unlike <see cref="ChatOptions"/>.
/// </summary>
public sealed record StoredAgentOptions
{
    /// <summary>
    /// The system instructions.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// The sampling temperature.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// The nucleus sampling probability.
    /// </summary>
    public float? TopP { get; init; }

    /// <summary>
    /// The number of most probable tokens to sample from.
    /// </summary>
    public int? TopK { get; init; }

    /// <summary>
    /// The most tokens a response may have.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// The penalty for tokens by how often they appeared.
    /// </summary>
    public float? FrequencyPenalty { get; init; }

    /// <summary>
    /// The penalty for tokens that appeared at all.
    /// </summary>
    public float? PresencePenalty { get; init; }

    /// <summary>
    /// The seed for reproducible sampling.
    /// </summary>
    public long? Seed { get; init; }

    /// <summary>
    /// Whether the model may call several tools in one response.
    /// </summary>
    public bool? AllowMultipleToolCalls { get; init; }

    /// <summary>
    /// The sequences that end a response.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    // Every option of a stored agent is data, so each one may change at runtime
    internal static IReadOnlyList<ChatOptionProperty> Configurable { get; } =
    [
        ChatOptionProperty.Create(
            nameof(Instructions),
            options => options.Instructions,
            (options, value) => options.Instructions = value),
        ChatOptionProperty.Create(
            nameof(Temperature),
            options => options.Temperature,
            (options, value) => options.Temperature = value),
        ChatOptionProperty.Create(
            nameof(TopP),
            options => options.TopP,
            (options, value) => options.TopP = value),
        ChatOptionProperty.Create(
            nameof(TopK),
            options => options.TopK,
            (options, value) => options.TopK = value),
        ChatOptionProperty.Create(
            nameof(MaxOutputTokens),
            options => options.MaxOutputTokens,
            (options, value) => options.MaxOutputTokens = value),
        ChatOptionProperty.Create(
            nameof(FrequencyPenalty),
            options => options.FrequencyPenalty,
            (options, value) => options.FrequencyPenalty = value),
        ChatOptionProperty.Create(
            nameof(PresencePenalty),
            options => options.PresencePenalty,
            (options, value) => options.PresencePenalty = value),
        ChatOptionProperty.Create(
            nameof(Seed),
            options => options.Seed,
            (options, value) => options.Seed = value),
        ChatOptionProperty.Create(
            nameof(AllowMultipleToolCalls),
            options => options.AllowMultipleToolCalls,
            (options, value) => options.AllowMultipleToolCalls = value),
        ChatOptionProperty.Create(
            nameof(StopSequences),
            options => options.StopSequences,
            (options, value) => options.StopSequences = value)
    ];

    internal static StoredAgentOptions From(ChatOptions options) =>
        new()
        {
            Instructions = options.Instructions,
            Temperature = options.Temperature,
            TopP = options.TopP,
            TopK = options.TopK,
            MaxOutputTokens = options.MaxOutputTokens,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            Seed = options.Seed,
            AllowMultipleToolCalls = options.AllowMultipleToolCalls,
            StopSequences = options.StopSequences is { } sequences ? [.. sequences] : null
        };

    internal ChatOptions ToChatOptions() =>
        new()
        {
            Instructions = Instructions,
            Temperature = Temperature,
            TopP = TopP,
            TopK = TopK,
            MaxOutputTokens = MaxOutputTokens,
            FrequencyPenalty = FrequencyPenalty,
            PresencePenalty = PresencePenalty,
            Seed = Seed,
            AllowMultipleToolCalls = AllowMultipleToolCalls,
            StopSequences = StopSequences is { } sequences ? [.. sequences] : null
        };
}
