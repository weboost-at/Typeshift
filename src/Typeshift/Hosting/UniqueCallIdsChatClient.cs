using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D59: a provider may number its calls per response, so a later response reuses an id the conversation holds. The
// tool loop and the conversation key calls and approval requests by id, so a reused id is renamed before the
// checkpoints store the response and the tool loop reads it
internal sealed class UniqueCallIdsChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var call = messages.ToList();
        var used = Used(call);
        var response = await base.GetResponseAsync(call, options, cancellationToken);

        foreach (var message in response.Messages)
        {
            Rename(message.Contents, used);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var call = messages.ToList();
        var used = Used(call);

        await foreach (var update in base.GetStreamingResponseAsync(call, options, cancellationToken))
        {
            Rename(update.Contents, used);
            yield return update;
        }
    }

    private static HashSet<string> Used(IEnumerable<ChatMessage> messages) =>
    [
        .. messages
            .SelectMany(message => message.Contents)
            .Select(content => content switch
            {
                FunctionCallContent call => call.CallId,
                FunctionResultContent result => result.CallId,
                ToolApprovalRequestContent { ToolCall: { } call } => call.CallId,
                _ => null
            })
            .OfType<string>()
    ];

    // Each call keeps its id unless the conversation or an earlier call of the response holds it already
    private static void Rename(IList<AIContent> contents, HashSet<string> used)
    {
        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i] is not FunctionCallContent call || used.Add(call.CallId)) continue;

            var id = Enumerable.Range(2, int.MaxValue - 2)
                .Select(number => $"{call.CallId}_{number}")
                .First(used.Add);

            contents[i] = new FunctionCallContent(id, call.Name, call.Arguments)
            {
                Exception = call.Exception,
                InformationalOnly = call.InformationalOnly,
                AdditionalProperties = call.AdditionalProperties,
                Annotations = call.Annotations,
                RawRepresentation = call.RawRepresentation
            };
        }
    }
}
