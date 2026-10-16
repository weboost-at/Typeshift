using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D36: the Typeshift source and meter, after OpenTelemetry's conventions for generative AI. A run is an invoke_agent
// span; Microsoft.Extensions.AI's OpenTelemetryChatClient writes the chat spans and the metrics, and the tool loop
// the execute_tool spans with its source
internal static class Telemetry
{
    public const string SourceName = "Typeshift";

    private static readonly ActivitySource Source = new(SourceName);

    public static Activity? StartRun(Agent agent, ChatOptions? options)
    {
        var activity = Source.StartActivity($"invoke_agent {agent.Name}", ActivityKind.Internal);
        if (activity is null) return null;

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
}
