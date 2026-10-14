using Typeshift.Generators.Models;

namespace Typeshift.Generators.Emitting;

// A chat or run option a Configurable pick may name: its member in the agent's Settings, that member's type, how a
// set value is applied ({0} standing for it), and how the member is stored
internal sealed record OptionMember(string Option, string Setting, string Type, string Apply, StorageModel Storage);

internal static class OptionMembers
{
    private static readonly StorageModel Text = new(StorageKind.Scalar, "string");

    private static readonly StorageModel Flag = new(StorageKind.Scalar, "bool");

    private static readonly StorageModel Single = new(StorageKind.Number, "float");

    private static readonly StorageModel Integer = new(StorageKind.Number, "int");

    private static readonly StorageModel Mode = new(StorageKind.Enum, "global::Typeshift.AgentMode");

    // D28: the model is Model in Settings. Options of the run itself (ConversationId, Tools, AdditionalProperties) and
    // types settings cannot store (ResponseFormat, ToolMode) are left out
    public static readonly OptionMember[] Chat =
    [
        new("ModelId", "Model", "string?", "{0}", Text),
        new("Instructions", "Instructions", "string?", "{0}", Text),
        new("Temperature", "Temperature", "float?", "{0}", Single),
        new("MaxOutputTokens", "MaxOutputTokens", "int?", "{0}", Integer),
        new("TopP", "TopP", "float?", "{0}", Single),
        new("TopK", "TopK", "int?", "{0}", Integer),
        new("FrequencyPenalty", "FrequencyPenalty", "float?", "{0}", Single),
        new("PresencePenalty", "PresencePenalty", "float?", "{0}", Single),
        new("Seed", "Seed", "long?", "{0}", new(StorageKind.Number, "long")),
        new(
            "StopSequences",
            "StopSequences",
            "global::System.Collections.Generic.IReadOnlyList<string>?",
            "[.. {0}]",
            new(StorageKind.List, "global::System.Collections.Generic.IReadOnlyList<string>", Element: Text)),
        new("AllowMultipleToolCalls", "AllowMultipleToolCalls", "bool?", "{0}", Flag),
        new(
            "Reasoning",
            "Reasoning",
            "global::Microsoft.Extensions.AI.ReasoningOptions?",
            "{0}",
            new(StorageKind.Scalar, "global::Microsoft.Extensions.AI.ReasoningOptions"))
    ];

    public static readonly OptionMember[] Run =
    [
        new("Mode", "Mode", "global::Typeshift.AgentMode?", "{0}", Mode),
        new("MaxToolIterations", "MaxToolIterations", "int?", "{0}", Integer),
        new("MaxConsecutiveToolErrors", "MaxConsecutiveToolErrors", "int?", "{0}", Integer),
        new("ConcurrentToolCalls", "ConcurrentToolCalls", "bool?", "{0}", Flag),
        new("MaxInputTokens", "MaxInputTokens", "int?", "{0}", Integer),
        new("TargetInputTokens", "TargetInputTokens", "int?", "{0}", Integer),
        new("SummaryModel", "SummaryModel", "string?", "{0}", Text)
    ];
}
