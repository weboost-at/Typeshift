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
    /// The most model calls one run's tool loop makes; 40 unless set.
    /// </summary>
    /// <param name="iterations">The calls.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder WithMaxToolIterations(int iterations) =>
        Set(options => options with { MaxToolIterations = iterations });

    /// <summary>
    /// The most tool calls in a row that may fail before the run ends; 3 unless set.
    /// </summary>
    /// <param name="errors">The failed calls.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder WithMaxConsecutiveToolErrors(int errors) =>
        Set(options => options with { MaxConsecutiveToolErrors = errors });

    /// <summary>
    /// Runs the tool calls of one model response at the same time instead of one after the other.
    /// </summary>
    /// <param name="concurrent">Whether they run at the same time.</param>
    /// <returns>The same chain.</returns>
    public RunOptionsBuilder WithConcurrentToolCalls(bool concurrent) =>
        Set(options => options with { ConcurrentToolCalls = concurrent });

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
    /// The most model calls one run's tool loop makes.
    /// </summary>
    public int MaxToolIterations { get; init; } = 40;

    /// <summary>
    /// The most tool calls in a row that may fail before the run ends.
    /// </summary>
    public int MaxConsecutiveToolErrors { get; init; } = 3;

    /// <summary>
    /// Whether the tool calls of one model response run at the same time.
    /// </summary>
    public bool ConcurrentToolCalls { get; init; }
}

/// <summary>
/// How a run treats the tools it may not run unasked.
/// </summary>
public enum AgentMode
{
    /// <summary>
    /// Tools that require approval ask the host, unless a standing approval or a rule decides.
    /// </summary>
    Default,

    /// <summary>
    /// Only tools marked read-only are offered. The model researches, then presents a plan with
    /// <c>exit_plan_mode</c>, which asks the host; approving it continues in the mode the host chooses.
    /// </summary>
    Plan,

    /// <summary>
    /// Tools that require approval run without asking, unless a rule rejects the call.
    /// </summary>
    Auto
}
