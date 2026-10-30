using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Web;

// begin-snippet: chat-contracts
internal sealed record ChatInput(string Text);

internal sealed record ChatReply(string Text, IReadOnlyList<PendingApproval> Approvals)
{
    public static ChatReply From(ChatResponse answer) =>
        new(answer.Text, [.. answer.ApprovalRequests.Select(PendingApproval.From).OfType<PendingApproval>()]);
}

// What the UI shows to ask, and sends back with the user's answer
internal sealed record PendingApproval(
    string RequestId,
    string CallId,
    string Tool,
    IDictionary<string, object?>? Arguments)
{
    public static PendingApproval? From(ToolApprovalRequestContent request) =>
        request.ToolCall is FunctionCallContent call
            ? new(request.RequestId, call.CallId, call.Name, call.Arguments)
            : null;
}
// end-snippet

// begin-snippet: approval-answer
internal sealed record ApprovalAnswer(
    string RequestId,
    string CallId,
    string Tool,
    bool Approved,
    bool ForConversation = false,
    string? Reason = null)
{
    // Typeshift runs the call it stored with the request, so the call here only names it
    public ToolApprovalResponseContent ToResponse()
    {
        var request = new ToolApprovalRequestContent(RequestId, new FunctionCallContent(CallId, Tool));

        return Approved && ForConversation
            ? request.ApproveForConversation()
            : request.CreateResponse(Approved, Reason);
    }
}
// end-snippet
