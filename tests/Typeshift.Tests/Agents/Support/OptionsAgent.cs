using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// Two calls of the meeting tool wait for each other, so both return only when they ran at the same time
public sealed class ToolMeeting
{
    private int _arrived;

    public TaskCompletionSource Met { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Arrive()
    {
        if (Interlocked.Increment(ref _arrived) == 2) Met.TrySetResult();
    }
}

public partial class MeetingToolset : Toolset
{
    public MeetingToolset()
    {
        AddTool(MeetAsync)
            .WithDescription("Waits until a second call of it arrives.");
    }

    /// <param name="caller">Who calls.</param>
    public static async Task<string> MeetAsync(string caller, IServiceProvider services, CancellationToken ct)
    {
        var meeting = services.GetRequiredService<ToolMeeting>();

        meeting.Arrive();
        await meeting.Met.Task.WaitAsync(ct);

        return caller;
    }
}

// Sets every chat option and every run option in code, each to a value no default holds
public partial class OptionsAgent : Agent
{
    public const string Model = "options-model";

    public const float Temperature = 0.25f;

    public const int MaxOutputTokens = 512;

    public const float TopP = 0.75f;

    public const int TopK = 24;

    public const float FrequencyPenalty = 0.5f;

    public const float PresencePenalty = 0.125f;

    public const long Seed = 4_711;

    public const string Stop = "STOP";

    public const string Property = "vendor_option";

    public const string PropertyValue = "on";

    public const int MaxToolIterations = 7;

    public const int MaxConsecutiveToolErrors = 2;

    public const int MaxInputTokens = 90_000;

    public const int TargetInputTokens = 30_000;

    public const string SummaryModel = "options-summary-model";

    public OptionsAgent()
    {
        WithChatOptions()
            .WithModel(ProbeAgent.ModelProvider, Model)
            .WithTemperature(Temperature)
            .WithMaxOutputTokens(MaxOutputTokens)
            .WithTopP(TopP)
            .WithTopK(TopK)
            .WithFrequencyPenalty(FrequencyPenalty)
            .WithPresencePenalty(PresencePenalty)
            .WithSeed(Seed)
            .WithReasoning(new() { Effort = ReasoningEffort.Low, Output = ReasoningOutput.Summary })
            .WithResponseFormat(ChatResponseFormat.Json)
            .WithStopSequences(Stop)
            .WithToolMode(ChatToolMode.RequireAny)
            .WithAllowMultipleToolCalls(true)
            .WithTools(new HostedCodeInterpreterTool())
            .WithAdditionalProperty(Property, PropertyValue);

        WithRunOptions()
            .WithMode(AgentMode.Auto)
            .WithMaxToolIterations(MaxToolIterations)
            .WithMaxConsecutiveToolErrors(MaxConsecutiveToolErrors)
            .WithConcurrentToolCalls(true)
            .WithMaxInputTokens(MaxInputTokens)
            .WithTargetInputTokens(TargetInputTokens)
            .WithSummaryModel(SummaryModel);

        UseToolset<MeetingToolset>();
    }
}
