namespace Typeshift.Tests.Agents.Support;

// Its model and every limit of a run are configurable, so a test's settings set what no run can go with. The other
// constructors declare a test's own values, which the chains refuse
public partial class LimitsAgent : Agent
{
    public const string AgentName = "limits_agent";

    public const string ModelProvider = "limits-provider";

    public const string Model = "limits-model";

    public LimitsAgent()
    {
        WithChatOptions()
            .WithModel(ModelProvider, Model)
            .Configurable(o => o.ModelId);

        WithRunOptions()
            .Configurable(r => r.MaxToolIterations)
            .Configurable(r => r.MaxConsecutiveToolErrors)
            .Configurable(r => r.MaxInputTokens)
            .Configurable(r => r.TargetInputTokens)
            .Configurable(r => r.SummaryModel)
            .Configurable(r => r.SummaryInstructions);
    }

    public LimitsAgent(int iterations, int errors, int maximum, int target) =>
        WithRunOptions()
            .WithMaxToolIterations(iterations)
            .WithMaxConsecutiveToolErrors(errors)
            .WithMaxInputTokens(maximum)
            .WithTargetInputTokens(target);

    public LimitsAgent(string provider, string model) => WithChatOptions().WithModel(provider, model);

    public LimitsAgent(string summaryModel) => WithRunOptions().WithSummaryModel(summaryModel);

    // The settings that set one limit
    public static Settings Limit(string option, int value) =>
        option switch
        {
            nameof(RunOptions.MaxToolIterations) => new() { MaxToolIterations = value },
            nameof(RunOptions.MaxConsecutiveToolErrors) => new() { MaxConsecutiveToolErrors = value },
            nameof(RunOptions.MaxInputTokens) => new() { MaxInputTokens = value },
            _ => new() { TargetInputTokens = value }
        };
}
