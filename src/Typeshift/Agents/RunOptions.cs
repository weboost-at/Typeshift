using System.Diagnostics.CodeAnalysis;

namespace Typeshift;

/// <summary>
/// The run options an agent starts every run from, and those that may change at runtime.
/// </summary>
public sealed class RunOptionsBuilder
{
    private readonly Agent _agent;

    internal RunOptionsBuilder(Agent agent) => _agent = agent;

    /// <summary>
    /// The mode runs start in; <see cref="AgentMode.Default"/> unless set.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder WithMode(AgentMode mode) => Set(options => options with { Mode = mode });

    /// <summary>
    /// The most rounds of tool calls the agent makes in one run; 40 unless set. An agent the run is handed off to
    /// counts its own, and so does each run of a sub-agent it calls, so the limit bounds no run's sub-agents.
    /// </summary>
    /// <param name="iterations">The rounds, 1 or more.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No run can go with the value.</exception>
    public RunOptionsBuilder WithMaxToolIterations(int iterations) =>
        SetLimit(options => options with { MaxToolIterations = iterations }, nameof(iterations), iterations);

    /// <summary>
    /// The most rounds of tool calls in a row that may fail before the failing tool's exception ends the run and
    /// reaches your code; 3 unless set.
    /// </summary>
    /// <param name="errors">The failed rounds, 0 or more.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No run can go with the value.</exception>
    public RunOptionsBuilder WithMaxConsecutiveToolErrors(int errors) =>
        SetLimit(options => options with { MaxConsecutiveToolErrors = errors }, nameof(errors), errors);

    /// <summary>
    /// Runs the tool calls of one model response at the same time instead of one after the other.
    /// </summary>
    /// <param name="concurrent">Whether they run at the same time.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder WithConcurrentToolCalls(bool concurrent) =>
        Set(options => options with { ConcurrentToolCalls = concurrent });

    /// <summary>
    /// Turns compaction on: before each model call whose input reaches this many tokens, the history the model sees
    /// shrinks to <see cref="WithTargetInputTokens"/>.
    /// </summary>
    /// <param name="tokens">The input tokens, above 0 and above the target.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No run can go with the value.</exception>
    public RunOptionsBuilder WithMaxInputTokens(int tokens) =>
        SetLimit(options => options with { MaxInputTokens = tokens }, nameof(tokens), tokens);

    /// <summary>
    /// How far compaction shrinks the history; half of <see cref="WithMaxInputTokens"/> unless set.
    /// </summary>
    /// <param name="tokens">The input tokens, above 0 and below the maximum.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No run can go with the value.</exception>
    public RunOptionsBuilder WithTargetInputTokens(int tokens) =>
        SetLimit(options => options with { TargetInputTokens = tokens }, nameof(tokens), tokens);

    /// <summary>
    /// The model that summarizes what clearing tool results did not free, served by the run's provider; without one,
    /// the run's model summarizes. Stored settings or a run may name another provider for it.
    /// </summary>
    /// <param name="model">The model, as the run's provider names it.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> is null or white space.</exception>
    public RunOptionsBuilder WithSummaryModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return Set(options => options with { SummaryModel = model });
    }

    /// <summary>
    /// The instructions the summary model receives, in place of Typeshift's, which ask in English for the task, the
    /// decisions and why, the facts learned, the files and names touched, and the work left. Write your own for an
    /// app in another language or another domain; the model's history follows as one message after them.
    /// </summary>
    /// <param name="instructions">The instructions.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentException"><paramref name="instructions"/> is null or white space.</exception>
    public RunOptionsBuilder WithSummaryInstructions(string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        return Set(options => options with { SummaryInstructions = instructions });
    }

    // D89: a value, as WithReasoning takes one, so the chain stays flat and the generator reads nothing new
    /// <summary>
    /// Sets what the agent's runs do when a model call fails before any of its answer reached your app: try it again,
    /// move to another model or provider, or time out, as in
    /// <c>WithModelPolicy(ModelPolicy.Handle(ModelFailure.Transient).Retry(2).Timeout(TimeSpan.FromSeconds(30)))</c>.
    /// <see cref="ModelPolicy.None"/>, one attempt for each call, unless set. The call site's
    /// <c>host.WithModelPolicy(policy)</c> hands a run another, and settings cannot store one.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/models#the-model-policy">The model policy</see>.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The same chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    [Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
    public RunOptionsBuilder WithModelPolicy(ModelPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Set(options => options with { ModelPolicy = policy });
    }

    // D5: the generator reads the pick; at runtime the call declares nothing
    /// <summary>
    /// Lets a run option change at runtime. The lambda never runs.
    /// </summary>
    /// <typeparam name="TOption">The option's type.</typeparam>
    /// <param name="option">The option, <c>r => r.MaxToolIterations</c>.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder Configurable<TOption>(Func<RunOptions, TOption> option) => this;

    private RunOptionsBuilder Set(Func<RunOptions, RunOptions> set)
    {
        _agent.RunOptions = set(_agent.RunOptions);
        return this;
    }

    // D32: a value no run can go with is refused where the agent declares it
    private RunOptionsBuilder SetLimit(Func<RunOptions, RunOptions> set, string parameter, int value)
    {
        if (set(_agent.RunOptions).GetInvalid() is { } invalid)
            throw new ArgumentOutOfRangeException(parameter, value, $"{invalid.Option} must {invalid.Must}.");

        return Set(set);
    }
}

/// <summary>
/// How an agent's runs go; the options <see cref="RunOptionsBuilder"/> sets, as configurable picks name them.
/// </summary>
public sealed record RunOptions
{
    /// <summary>
    /// The mode runs start in.
    /// </summary>
    public AgentMode Mode { get; init; }

    /// <summary>
    /// The most rounds of tool calls the agent makes in one run. An agent the run is handed off to counts its own,
    /// and so does each run of a sub-agent it calls.
    /// </summary>
    public int MaxToolIterations { get; init; } = 40;

    /// <summary>
    /// The most rounds of tool calls in a row that may fail before the failing tool's exception ends the run.
    /// </summary>
    public int MaxConsecutiveToolErrors { get; init; } = 3;

    /// <summary>
    /// Whether the tool calls of one model response run at the same time.
    /// </summary>
    public bool ConcurrentToolCalls { get; init; }

    /// <summary>
    /// The input tokens at which the history is compacted; null leaves it whole.
    /// </summary>
    public int? MaxInputTokens { get; init; }

    /// <summary>
    /// The input tokens compaction shrinks the history to; null for half of <see cref="MaxInputTokens"/>.
    /// </summary>
    public int? TargetInputTokens { get; init; }

    /// <summary>
    /// The model that summarizes the oldest turns, served by the run's provider unless settings name another; null for
    /// the run's model.
    /// </summary>
    public string? SummaryModel { get; init; }

    /// <summary>
    /// The instructions the summary model receives; null for Typeshift's.
    /// </summary>
    public string? SummaryInstructions { get; init; }

    /// <summary>
    /// What the runs do when a model call fails: the policy of the agent's code, or the one the call site handed the
    /// run. Settings cannot store it.
    /// </summary>
    [Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
    public ModelPolicy ModelPolicy { get; init; } = ModelPolicy.None;

    // D32: the first value no run can go with, and what it must do instead; null when every value holds
    internal (string Option, object Value, string Must)? GetInvalid() =>
        this switch
        {
            { MaxToolIterations: < 1 } => (nameof(MaxToolIterations), MaxToolIterations, "be 1 or more"),
            { MaxConsecutiveToolErrors: < 0 } =>
                (nameof(MaxConsecutiveToolErrors), MaxConsecutiveToolErrors, "be 0 or more"),
            { MaxInputTokens: <= 0 } => (nameof(MaxInputTokens), MaxInputTokens, "be above 0"),
            { TargetInputTokens: <= 0 } => (nameof(TargetInputTokens), TargetInputTokens, "be above 0"),
            { MaxInputTokens: { } max, TargetInputTokens: { } target } when target >= max =>
                (nameof(TargetInputTokens), target, $"be below {nameof(MaxInputTokens)}, {max}"),
            { SummaryModel: { } model } when string.IsNullOrWhiteSpace(model) =>
                (nameof(SummaryModel), $"'{model}'", "name a model"),
            { SummaryInstructions: { } instructions } when string.IsNullOrWhiteSpace(instructions) =>
                (nameof(SummaryInstructions), $"'{instructions}'", "hold instructions"),
            _ => null
        };
}

/// <summary>
/// How a run treats the tools it may not run unasked.
/// </summary>
public enum AgentMode
{
    /// <summary>
    /// Tools that require approval ask your app, unless a standing approval or a rule decides.
    /// </summary>
    Default,

    /// <summary>
    /// Only tools marked read-only are offered. The model researches, then presents a plan with
    /// <c>exit_plan_mode</c>, which asks your app; approving it continues in the mode your app chooses.
    /// </summary>
    Plan,

    /// <summary>
    /// Tools that require approval run without asking, unless a rule rejects the call.
    /// </summary>
    Auto
}
