using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D59: a provider may number its calls per response, so a later response reuses an id the conversation holds. The
// tool loop and the conversation key calls and approval requests by id, so a reused id is renamed before the
// checkpoints store the response and the tool loop reads it. A provider that keeps the conversation itself, which a
// response with a conversation id shows, keeps its ids, as it matches the results to them
internal sealed class UniqueCallIdsChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var call = messages.ToList();
        var used = Used(call, options);
        var response = await base.GetResponseAsync(call, options, cancellationToken);

        if (response.ConversationId is not null) return response;

        var renamed = new Dictionary<string, string>();

        foreach (var message in response.Messages)
        {
            message.Contents = Renamed(message.Contents, used, renamed);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var call = messages.ToList();
        var used = Used(call, options);
        var renamed = new Dictionary<string, string>();
        var kept = false;

        await foreach (var update in base.GetStreamingResponseAsync(call, options, cancellationToken))
        {
            kept |= update.ConversationId is not null;
            if (!kept) update.Contents = Renamed(update.Contents, used, renamed);

            yield return update;
        }
    }

    public static IEnumerable<string> Ids(IEnumerable<ChatMessage> messages) =>
        messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                FunctionCallContent call => call.CallId,
                FunctionResultContent result => result.CallId,
                ToolApprovalRequestContent { ToolCall: { } call } => call.CallId,
                ToolApprovalResponseContent { ToolCall: { } call } => call.CallId,
                _ => null
            })
            .OfType<string>();

    // The ids of the call and of the conversation, those its history leaves out included
    private static HashSet<string> Used(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        HashSet<string> used = [.. Ids(messages)];
        if (options is ResolvedChatOptions { ConversationRun: { } run }) run.AddCallIdsTo(used);
        return used;
    }

    // Each call keeps its id unless the conversation or an earlier call of the response holds it, and a result the
    // provider returned with a renamed call follows it; a list with nothing renamed is returned as it is
    private static IList<AIContent> Renamed(
        IList<AIContent> contents,
        HashSet<string> used,
        Dictionary<string, string> renamed)
    {
        List<AIContent>? copy = null;

        for (var i = 0; i < contents.Count; i++)
        {
            AIContent? replaced = contents[i] switch
            {
                FunctionCallContent call when !used.Add(call.CallId) => Rename(call, used, renamed),
                FunctionResultContent result when renamed.TryGetValue(result.CallId, out var id) => Copy(result, id),
                _ => null
            };

            if (replaced is null) continue;

            copy ??= [.. contents];
            copy[i] = replaced;
        }

        return copy ?? contents;
    }

    // The provider's raw item names the old id, which an adapter would send back as it is
    private static FunctionCallContent Rename(
        FunctionCallContent call,
        HashSet<string> used,
        Dictionary<string, string> renamed)
    {
        var id = Enumerable.Range(2, int.MaxValue - 2).Select(number => $"{call.CallId}_{number}").First(used.Add);
        renamed[call.CallId] = id;

        return new FunctionCallContent(id, call.Name, call.Arguments)
        {
            Exception = call.Exception,
            InformationalOnly = call.InformationalOnly,
            AdditionalProperties = call.AdditionalProperties,
            Annotations = call.Annotations
        };
    }

    private static FunctionResultContent Copy(FunctionResultContent result, string id) =>
        new(id, result.Result)
        {
            Exception = result.Exception,
            AdditionalProperties = result.AdditionalProperties,
            Annotations = result.Annotations
        };
}
