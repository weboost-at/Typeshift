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

    public static int Estimate(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var characters = (options?.Instructions?.Length ?? 0)
            + (options?.Tools ?? []).Sum(tool => tool.Name.Length + tool.Description.Length + Schema(tool))
            + messages.Sum(message => message.Contents.Sum(Characters));

        return characters / CharactersPerToken;
    }

    public static int Estimate(AIContent content) => Characters(content) / CharactersPerToken;

    private static int Characters(AIContent content) =>
        content switch
        {
            TextContent text => text.Text.Length,
            TextReasoningContent reasoning => reasoning.Text.Length,
            FunctionCallContent call => call.Name.Length + Json(call.Arguments),
            FunctionResultContent result => Text(result.Result).Length,
            DataContent or UriContent or HostedFileContent => PerAttachment * CharactersPerToken,
            _ => 0
        };

    public static string Text(object? value) =>
        value switch
        {
            null => "",
            string text => text,
            JsonElement element => element.GetRawText(),
            _ => value.ToString() ?? ""
        };

    private static int Schema(AITool tool) =>
        tool is AIFunctionDeclaration function ? function.JsonSchema.GetRawText().Length : 0;

    private static int Json(IDictionary<string, object?>? arguments) =>
        arguments?.Sum(argument => argument.Key.Length + Text(argument.Value).Length) ?? 0;
}
