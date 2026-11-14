using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D36: the Typeshift source and meter, after OpenTelemetry's conventions for generative AI. A run is an invoke_agent
// span; Microsoft.Extensions.AI's OpenTelemetryChatClient writes the chat spans and the metrics, and the tool loop
// the execute_tool spans with its source
internal static class Telemetry
{
    public const string SourceName = "Typeshift";

    // Microsoft.Extensions.AI's mark on a span whose run's chat spans carry their messages
    private const string SensitiveData = "__EnableSensitiveData__";

    private static readonly ActivitySource Source = new(SourceName);

    public static Activity? StartRun(Agent agent, ChatOptions? options, IChatClient client)
    {
        var activity = Source.StartActivity($"invoke_agent {agent.Name}", ActivityKind.Internal);
        if (activity is null) return null;

        // Under a run's span the tool loop asks the span, not its client, whether to write arguments and results:
        // Microsoft.Extensions.AI's marker says so when the run's chat spans carry their messages
        if (client.GetService<OpenTelemetryChatClient>() is { EnableSensitiveData: true })
            activity.SetCustomProperty(SensitiveData, "true");

        activity.SetTag("gen_ai.operation.name", "invoke_agent");
        activity.SetTag("gen_ai.agent.name", agent.Name);
        activity.SetTag("gen_ai.agent.description", agent.Description);
        activity.SetTag("gen_ai.request.model", options?.ModelId);

        activity.SetTag(
            "gen_ai.conversation.id",
            options is ResolvedChatOptions { Conversation: { } conversation }
                ? conversation.ToString()
                : options?.ConversationId);

        return activity;
    }

    public static void Answered(Activity? activity, UsageDetails? usage)
    {
        if (activity is null || usage is null) return;

        activity.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount);
        activity.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount);
    }

    public static void Failed(Activity? activity, Exception exception)
    {
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    // D80: what the run asks your app, counted on its span and an event for each decision. A run that ends waiting
    // is no error; what it asked is here, and what became of it on the span of the run that settles it
    public static void Asked(Activity? activity, DecisionBatch batch)
    {
        if (activity is null) return;

        var asked = batch.Calls.Where(call => call.Ask is not null).ToList();
        var before = activity.GetTagItem("typeshift.decisions.asked") as int? ?? 0;

        activity.SetTag("typeshift.decisions.asked", before + asked.Count);

        foreach (var call in asked)
        {
            var tags = Describe(call);

            if (CapturesContent(activity)) tags["gen_ai.tool.call.arguments"] = call.Arguments.GetRawText();
            activity.AddEvent(new ActivityEvent("typeshift.decision.asked", tags: tags));
        }
    }

    // D80: what became of each call of a batch the run settles. A call of an answered batch goes by your app's
    // answer, one of an expired or a withdrawn batch did not run, and of one that another run started and left
    // nobody knows.
    // The text the model reads in the call's place is content, as an argument is
    public static void Settled(Activity? activity, DecisionBatch batch, IEnumerable<BatchCall> calls, bool repaired)
    {
        if (activity is null) return;

        foreach (var call in calls)
        {
            var verdict = DecisionResults.VerdictOf(call);
            var tags = Describe(call);

            tags["typeshift.decision.outcome"] = (batch.State, call, repaired) switch
            {
                (BatchState.Expired, _, _) => "expired",
                (BatchState.Cancelled, _, _) => "cancelled",
                (_, { Rejected: not null }, _) => "rejected",
                (_, { Ask: { Kind: AskKind.Question, Answer.Answers: not null } }, _) => "answered",
                (_, { Ask.Kind: AskKind.Question }, _) => "declined",
                (_, { Ask.Answer.Approved: false }, _) => "rejected",
                (_, { Ask.Kind: AskKind.Plan }, _) => "approved",
                (_, _, true) => "unknown",
                (_, { Ask: not null }, _) => "approved",
                _ => "run"
            };

            var ran = batch.State is not (BatchState.Expired or BatchState.Cancelled);

            if (CapturesContent(activity) && ran && verdict.Answer is { } answer)
                tags["gen_ai.tool.call.result"] = answer;

            activity.AddEvent(new ActivityEvent("typeshift.decision.settled", tags: tags));
        }
    }

    private static ActivityTagsCollection Describe(BatchCall call)
    {
        var tags = new ActivityTagsCollection
        {
            ["gen_ai.tool.name"] = call.Tool,
            ["gen_ai.tool.call.id"] = call.CallId
        };

        if (call.Ask is not { } ask) return tags;

        tags["typeshift.decision.id"] = ask.Id;
        tags["typeshift.decision.kind"] = DecisionJson.Name(ask.Kind);
        return tags;
    }

    private static bool CapturesContent(Activity activity) => activity.GetCustomProperty(SensitiveData) is "true";
}
