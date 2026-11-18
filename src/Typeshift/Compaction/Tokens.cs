using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D32: an estimate of a model call's input tokens from its characters, about four to a token; compaction calibrates
// it with the tokens the provider reports, so no tokenizer is needed
internal static class Tokens
{
    private const int CharactersPerToken = 4;

    // What an image or a file costs, whatever its size
    private const int PerAttachment = 1_000;

    // The lengths of the contents and tools whose text an estimate writes out to count, by reference: a run estimates
    // each model call over its whole history, so each is written out once a run, and its length kept as it was
    public static Dictionary<object, int> Lengths() => new(ReferenceEqualityComparer.Instance);

    public static int Estimate(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        Dictionary<object, int>? lengths = null)
    {
        var characters = options?.Instructions?.Length ?? 0;

        if (options?.Tools is { } tools)
        {
            for (var i = 0; i < tools.Count; i++)
            {
                characters += tools[i].Name.Length + tools[i].Description.Length + Schema(tools[i], lengths);
            }
        }

        foreach (var message in messages)
        {
            for (var i = 0; i < message.Contents.Count; i++)
            {
                characters += Characters(message.Contents[i], lengths);
            }
        }

        return characters / CharactersPerToken;
    }

    public static int Estimate(AIContent content, Dictionary<object, int>? lengths = null) =>
        Characters(content, lengths) / CharactersPerToken;

    private static int Characters(AIContent content, Dictionary<object, int>? lengths) =>
        content switch
        {
            TextContent text => text.Text.Length,
            TextReasoningContent reasoning => reasoning.Text.Length,
            FunctionCallContent call => Kept(call, lengths, static call => call.Name.Length + Json(call.Arguments)),
            FunctionResultContent { Result: string text } => text.Length,
            FunctionResultContent result => Kept(result, lengths, static result => Text(result.Result).Length),
            DataContent or UriContent or HostedFileContent => PerAttachment * CharactersPerToken,
            _ => 0
        };

    // A JSON string is the text it holds, so it counts the same however the JSON it was loaded from escaped it
    public static string Text(object? value) =>
        value switch
        {
            null => "",
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } text => text.GetString()!,
            JsonElement element => element.GetRawText(),
            IDictionary<string, object?> arguments =>
                string.Join(", ", arguments.Select(argument => $"{argument.Key}: {Text(argument.Value)}")),
            _ => value.ToString() ?? ""
        };

    private static int Schema(AITool tool, Dictionary<object, int>? lengths) =>
        tool is AIFunctionDeclaration function
            ? Kept(function, lengths, static function => function.JsonSchema.GetRawText().Length)
            : 0;

    private static int Json(IDictionary<string, object?>? arguments) =>
        arguments?.Sum(argument => argument.Key.Length + Text(argument.Value).Length) ?? 0;

    // The length of what was counted before, by reference: a content or a tool does not change once it is made
    private static int Kept<T>(T counted, Dictionary<object, int>? lengths, Func<T, int> count) where T : class
    {
        if (lengths is null) return count(counted);
        if (lengths.TryGetValue(counted, out var length)) return length;

        return lengths[counted] = count(counted);
    }
}
