namespace Typeshift.Generators.Emitting;

// A chat or run option a Configurable pick may name: its member in the agent's Settings, that member's type, and how
// a set value is applied, {0} standing for it
internal sealed record OptionMember(string Option, string Setting, string Type, string Apply);

internal static class OptionMembers
{
    // D28: the model is Model in Settings. Options of the run itself (ConversationId, Tools, AdditionalProperties) and
    // types settings cannot store (ResponseFormat, ToolMode) are left out
    public static readonly OptionMember[] Chat =
    [
        new("ModelId", "Model", "string?", "{0}"),
        new("Instructions", "Instructions", "string?", "{0}"),
        new("Temperature", "Temperature", "float?", "{0}"),
        new("MaxOutputTokens", "MaxOutputTokens", "int?", "{0}"),
        new("TopP", "TopP", "float?", "{0}"),
        new("TopK", "TopK", "int?", "{0}"),
        new("FrequencyPenalty", "FrequencyPenalty", "float?", "{0}"),
        new("PresencePenalty", "PresencePenalty", "float?", "{0}"),
        new("Seed", "Seed", "long?", "{0}"),
        new("StopSequences", "StopSequences", "global::System.Collections.Generic.IReadOnlyList<string>?", "[.. {0}]"),
        new("AllowMultipleToolCalls", "AllowMultipleToolCalls", "bool?", "{0}"),
        new("Reasoning", "Reasoning", "global::Microsoft.Extensions.AI.ReasoningOptions?", "{0}")
    ];

    public static readonly OptionMember[] Run =
    [
        new("MaxToolIterations", "MaxToolIterations", "int?", "{0}"),
        new("MaxConsecutiveToolErrors", "MaxConsecutiveToolErrors", "int?", "{0}"),
        new("ConcurrentToolCalls", "ConcurrentToolCalls", "bool?", "{0}")
    ];
}
