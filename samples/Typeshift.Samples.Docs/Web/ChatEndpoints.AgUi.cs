using System.Text.Json;
using System.Text.Json.Nodes;
using AGUI.Abstractions;
using AGUI.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

internal static partial class ChatEndpoints
{
    // begin-snippet: ag-ui-options
    // AG-UI's types first, then Microsoft.Extensions.AI's and the content that carries a decision
    internal static readonly JsonSerializerOptions AgUiJson = CreateAgUiJson();

    private static readonly AGUIStreamOptions AgUiStream = new AGUIStreamOptions().MapInterrupt(Interrupt);

    private static JsonSerializerOptions CreateAgUiJson()
    {
        var json = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { WriteIndented = false };
        json.TypeInfoResolverChain.Insert(0, AGUIJsonUtilities.DefaultTypeInfoResolver);
        AIJsonUtilities.AddAIContentType<DecisionRequestContent>(json, "decision");
        json.MakeReadOnly();
        return json;
    }

    // A decision ends the run as an interrupt with the decision's id, and the decision's JSON as its metadata
    private static AGUIInterrupt? Interrupt(AIContent content) =>
        content is DecisionRequestContent { Decision: var decision }
            ? new AGUIInterrupt
            {
                Id = decision.Id,
                Reason = "decision",
                Metadata = JsonSerializer.SerializeToElement(decision, AgUiJson)
            }
            : null;
    // end-snippet

    private static void MapAgUi(RouteGroupBuilder chat)
    {
        // begin-snippet: ag-ui-endpoint
        chat.MapPost("/ag-ui", async Task<IResult> (
            int chatId,
            HttpRequest request,
            AgentHost<SupportAgent> support,
            ICurrentCustomer customer,
            CancellationToken ct) =>
        {
            var input = (await request.ReadFromJsonAsync<RunAgentInput>(AgUiJson, ct))!;
            var context = input.ToChatRequestContext(AgUiJson, AgUiStream);
            var host = support.WithSettingsId(customer.Id).WithConversationId(chatId);

            // The conversation holds the chat, so a run takes the user's last message or the answers to interrupts
            var updates = input.Resume is { Count: > 0 } resume
                ? host.AnswerStreamingAsync([.. resume.Select(Answer)], ct)
                : host.RunStreamingAsync(context.Messages[^1], ct);

            try
            {
                var events = (await StartedAsync(updates, ct))
                    .AsAGUIEventStreamAsync(context, ct)
                    .Select(@event => JsonSerializer.Serialize(@event, AgUiJson));

                return TypedResults.ServerSentEvents(events);
            }
            catch (DecisionClosedException)
            {
                // Answered already, in another tab or by a retry: the UI drops the interrupt
                return TypedResults.Conflict();
            }
        });
        // end-snippet
    }

    // begin-snippet: ag-ui-answer
    // A resolved interrupt's payload is the answer endpoint's body without the decision's id: {"approved":true}
    private static DecisionAnswer Answer(AGUIResume resume)
    {
        if (resume is not { Status: ResumeStatus.Resolved, Payload: { } payload })
            return DecisionAnswer.Reject(resume.InterruptId);

        var body = JsonObject.Create(payload)!;
        body["decisionId"] = resume.InterruptId;
        return body.Deserialize<DecisionAnswer>(AgUiJson)!;
    }
    // end-snippet
}
