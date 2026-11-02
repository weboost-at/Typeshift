using Microsoft.Extensions.AI;

namespace Typeshift;

// D24, D60: the approval requests of one run still open, read message by message in the order they were stored: a
// request opens when it is stored without an answer, and closes when it is answered or once the agent answers
// steering input stored after it, as a new message closes it. A background agent's answer closes none, since the run
// that asked waits for it
internal sealed class OpenRequests
{
    private readonly HashSet<string> _open = [];
    private readonly HashSet<string> _steered = [];
    private readonly HashSet<string> _replied = [];

    public bool Contains(string request) => _open.Contains(request);

    public IReadOnlyCollection<string> Ids => _open;

    public void Add(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            Add(message);
        }
    }

    private void Add(ChatMessage message)
    {
        if (IsAnswer(message))
        {
            _open.ExceptWith(_steered);
            _steered.Clear();
        }

        if (IsSteering(message)) _steered.UnionWith(_open);

        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case ToolApprovalResponseContent answer:
                    _replied.Add(answer.RequestId);
                    _open.Remove(answer.RequestId);
                    break;

                case ToolApprovalRequestContent request when !_replied.Contains(request.RequestId):
                    _open.Add(request.RequestId);
                    break;
            }
        }
    }

    // Anything of the agent's own but the requests it left and the errors no model saw
    private static bool IsAnswer(ChatMessage message) =>
        message.Role == ChatRole.Assistant
        && message.Contents.Any(content => content is not (ToolApprovalRequestContent or ErrorContent));

    // Input that reached the run, beyond answers to its requests; a background agent's answer carries its call
    private static bool IsSteering(ChatMessage message) =>
        message.Role == ChatRole.User
        && message.AdditionalProperties?.ContainsKey(ConversationRun.BackgroundCall) is not true
        && message.Contents.Any(content => content is not ToolApprovalResponseContent);
}
