using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// Stands in for a model when a demo runs without a network: a function decides each answer from the messages and
/// the tools offered, and the client reports the usage a provider would, estimated at four characters a token, so the
/// stored usage and the point where compaction starts look as they would with a provider.
/// </summary>
public sealed class ScriptedChatClient(Func<ScriptedCall, ChatMessage> respond) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var history = messages.ToList();
        var answer = respond(new ScriptedCall(history, options));
        answer.MessageId ??= Guid.NewGuid().ToString("N");

        return Task.FromResult(new ChatResponse(answer)
        {
            ModelId = options?.ModelId,
            Usage = Usage(history, options, answer)
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        var message = response.Messages[0];

        // Text arrives word by word, as a provider streams it; calls arrive whole
        foreach (var content in message.Contents)
        {
            if (content is TextContent text)
            {
                foreach (var word in text.Text.Split(' '))
                {
                    yield return new ChatResponseUpdate(message.Role, word + " ")
                    {
                        ModelId = response.ModelId,
                        MessageId = message.MessageId
                    };
                }
            }
            else
            {
                yield return new ChatResponseUpdate(message.Role, [content])
                {
                    ModelId = response.ModelId,
                    MessageId = message.MessageId
                };
            }
        }

        yield return new ChatResponseUpdate(message.Role, [new UsageContent(response.Usage!)])
        {
            ModelId = response.ModelId,
            MessageId = message.MessageId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    // A provider counts the instructions and the tools offered as input, and tool calls and their results like text
    private static UsageDetails Usage(List<ChatMessage> history, ChatOptions? options, ChatMessage answer)
    {
        var characters = (options?.Instructions?.Length ?? 0)
            + (options?.Tools ?? []).Sum(tool => Characters(tool))
            + history.Sum(message => Characters(message));

        var input = characters / 4;
        var output = Characters(answer) / 4;

        return new UsageDetails { InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output };
    }

    private static int Characters(AITool tool) =>
        tool.Name.Length
        + tool.Description.Length
        + (tool is AIFunctionDeclaration function ? function.JsonSchema.GetRawText().Length : 0);

    private static int Characters(ChatMessage message) =>
        message.Contents.Sum(content => content switch
        {
            TextContent text => text.Text.Length,
            FunctionCallContent call => call.Name.Length + (call.Arguments?.Sum(argument => Characters(argument)) ?? 0),
            FunctionResultContent result => result.Result?.ToString()?.Length ?? 0,
            _ => 0
        });

    private static int Characters(KeyValuePair<string, object?> argument) =>
        argument.Key.Length + (argument.Value?.ToString()?.Length ?? 0);
}

/// <summary>
/// One model call as the script sees it.
/// </summary>
/// <param name="Messages">The messages of the call.</param>
/// <param name="Options">The options, with the tools offered.</param>
public sealed record ScriptedCall(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)
{
    public IReadOnlySet<string> Tools { get; } =
        (Options?.Tools ?? []).Select(tool => tool.Name).ToHashSet();

    public bool Offers(string tool) => Tools.Contains(tool);

    public ChatMessage Last => Messages[^1];

    public string LastUserText =>
        Messages.LastOrDefault(message => message.Role == ChatRole.User && message.Text.Length > 0)?.Text ?? "";

    public bool LastIsToolResult => Last.Role == ChatRole.Tool;

    public IEnumerable<FunctionResultContent> LastResults => Last.Contents.OfType<FunctionResultContent>();

    public string? LastResultOf(string tool)
    {
        var calls = Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()
            .Where(call => call.Name == tool)
            .Select(call => call.CallId)
            .ToHashSet();

        return Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .LastOrDefault(result => calls.Contains(result.CallId))
            ?.Result?.ToString();
    }

    public int CallsOf(string tool) =>
        Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Count(call => call.Name == tool);

    public static ChatMessage Text(string text) => new(ChatRole.Assistant, text);

    public static ChatMessage Call(string tool, params (string Name, object? Value)[] arguments) =>
        new(ChatRole.Assistant, [CallContent(tool, arguments)]);

    public static ChatMessage Calls(params FunctionCallContent[] calls) => new(ChatRole.Assistant, [.. calls]);

    public static FunctionCallContent CallContent(string tool, params (string Name, object? Value)[] arguments) =>
        new(
            $"call_{Guid.NewGuid():N}",
            tool,
            arguments.ToDictionary(argument => argument.Name, argument => AsJson(argument.Value)));

    // Arguments arrive as JSON, as a provider client delivers them
    private static object? AsJson(object? value) => value is null ? null : JsonSerializer.SerializeToElement(value);
}
