using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Typeshift;

// D89: a model policy is a value of rules, written inline as Polly's Policy.Handle(...) is, and compared by its rules
// and its timeout, so an agent's code declares one and a call site hands a run another without a class of its own
/// <summary>
/// What a run does when a model call fails before any of its answer reached your app: the failures each rule
/// handles, and whether the call is tried again, moves to another model or another provider, or times out. Start one
/// with <see cref="Handle"/>, add rules with <see cref="Or"/>, and declare it with
/// <c>WithRunOptions().WithModelPolicy(policy)</c> in an agent's constructor or hand it to a run with
/// <c>host.WithModelPolicy(policy)</c>. The first rule that handles a failure and is not used up applies; a failure no
/// rule handles reaches your app as the client threw it. Policies with the same rules, in the same order, and the same
/// timeout are equal.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/models#the-model-policy">The model policy</see>.
/// </summary>
[Experimental(ExperimentalIds.ModelPolicy, UrlFormat = ExperimentalIds.ModelPolicyUrl)]
public sealed record ModelPolicy
{
    // D89: the longest wait a timer of the TimeProvider takes, which a retry's delay and the timeout stay within
    internal static readonly TimeSpan LongestWait = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly ImmutableArray<ModelRule> _rules;

    private ModelPolicy(ImmutableArray<ModelRule> rules, TimeSpan untilFirstUpdate)
    {
        _rules = rules;
        UntilFirstUpdate = untilFirstUpdate;
    }

    /// <summary>
    /// The policy without rules or timeout: one attempt for each model call, which waits as long as the client does.
    /// An agent runs with it unless its code or its run names another.
    /// </summary>
    public static ModelPolicy None { get; } = new([], System.Threading.Timeout.InfiniteTimeSpan);

    // The rules in their order, and how long an attempt waits for its first update; infinite for no timeout
    internal ImmutableArray<ModelRule> Rules => _rules;

    internal TimeSpan UntilFirstUpdate { get; }

    /// <summary>
    /// Starts a policy with its first rule: the failures it handles, then what it does about them, as in
    /// <c>ModelPolicy.Handle(ModelFailure.Transient).Retry(2)</c>.
    /// </summary>
    /// <param name="failures">The kinds of failure the rule handles, one or more.</param>
    /// <returns>The rule, which a call of it turns into the policy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failures"/> names no kind of failure.</exception>
    public static Rule Handle(ModelFailure failures) => None.Or(failures);

    /// <summary>
    /// Adds a rule after this policy's rules: the failures it handles, then what it does about them. A failure goes
    /// to the first rule that handles it and is not used up, so a rule after another handles what the first left.
    /// </summary>
    /// <param name="failures">The kinds of failure the rule handles, one or more.</param>
    /// <returns>The rule, which a call of it turns into the policy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failures"/> names no kind of failure.</exception>
    public Rule Or(ModelFailure failures)
    {
        if (failures == ModelFailure.None || (failures & ~ModelFailure.Any) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failures),
                failures,
                "A rule handles one or more kinds of ModelFailure, such as ModelFailure.Transient.");
        }

        return new(this, failures);
    }

    /// <summary>
    /// Returns this policy with a timeout in place of its own: how long an attempt waits from its start for the first
    /// update of a streaming call, or for the response of a call that does not stream. An attempt that waits longer
    /// fails as <see cref="ModelFailure.Timeout"/>, which a rule may handle. An answer that started keeps coming,
    /// however long it takes.
    /// </summary>
    /// <param name="untilFirstUpdate">
    /// The time, above zero, or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> for no timeout.
    /// </param>
    /// <returns>The policy, with the same rules.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="untilFirstUpdate"/> is zero, negative but infinite, or longer than a timer waits.
    /// </exception>
    public ModelPolicy Timeout(TimeSpan untilFirstUpdate)
    {
        if (untilFirstUpdate != System.Threading.Timeout.InfiniteTimeSpan
            && (untilFirstUpdate <= TimeSpan.Zero || untilFirstUpdate > LongestWait))
        {
            throw new ArgumentOutOfRangeException(
                nameof(untilFirstUpdate),
                untilFirstUpdate,
                $"A model call's timeout is above zero and at most {LongestWait}, or Timeout.InfiniteTimeSpan.");
        }

        return new(_rules, untilFirstUpdate);
    }

    /// <summary>
    /// Whether the other policy has the same rules, in the same order, and the same timeout.
    /// </summary>
    /// <param name="other">The other policy.</param>
    /// <returns>Whether they are equal.</returns>
    public bool Equals(ModelPolicy? other) =>
        other is not null && UntilFirstUpdate == other.UntilFirstUpdate && _rules.SequenceEqual(other._rules);

    /// <summary>
    /// A hash of the rules and the timeout.
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(UntilFirstUpdate);

        foreach (var rule in _rules)
        {
            hash.Add(rule);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// The policy as code writes it, such as <c>Handle(Transient).Retry(2, 00:00:01, 2).Timeout(00:00:30)</c>, the
    /// same in every culture. A provider document is named by its <c>Name</c>, else its <c>Kind</c>, never its key.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        if (_rules.IsEmpty && UntilFirstUpdate == System.Threading.Timeout.InfiniteTimeSpan) return nameof(None);

        var text = new StringBuilder();

        for (var i = 0; i < _rules.Length; i++)
        {
            text.Append(i == 0 ? "Handle(" : ".Or(").Append(_rules[i].Failures).Append(").").Append(_rules[i]);
        }

        if (UntilFirstUpdate != System.Threading.Timeout.InfiniteTimeSpan)
        {
            text.Append(text.Length == 0 ? "None." : ".")
                .Append(CultureInfo.InvariantCulture, $"Timeout({UntilFirstUpdate})");
        }

        return text.ToString();
    }

    private ModelPolicy With(ModelRule rule) => new(_rules.Add(rule), UntilFirstUpdate);

    /// <summary>
    /// A rule of a policy, which names the failures it handles and takes what it does about them from one of its
    /// calls. Take one from <see cref="Handle"/> or <see cref="Or"/>.
    /// </summary>
    public readonly struct Rule
    {
        private readonly ModelPolicy? _policy;
        private readonly ModelFailure _failures;

        internal Rule(ModelPolicy policy, ModelFailure failures)
        {
            _policy = policy;
            _failures = failures;
        }

        /// <summary>
        /// Tries the model call again on the same provider and model, up to <paramref name="times"/> for each model
        /// call of the run and each pair of provider and model, waiting before each try. Client libraries try calls
        /// again on their own, and the tries multiply, so set one of the two.
        /// </summary>
        /// <param name="times">How often the call is tried again, 1 or more.</param>
        /// <param name="delay">The wait before the first try again; none unless set.</param>
        /// <param name="backoff">What each later wait is multiplied by, 1 or more; 2 unless set.</param>
        /// <returns>The policy with the rule.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="times"/> is below 1, <paramref name="delay"/> is negative or longer than a timer waits,
        /// or <paramref name="backoff"/> is below 1 or no finite number.
        /// </exception>
        public ModelPolicy Retry(int times, TimeSpan delay = default, double backoff = 2)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(delay, LongestWait);

            if (!double.IsFinite(backoff) || backoff < 1)
                throw new ArgumentOutOfRangeException(nameof(backoff), backoff, "A backoff is finite and 1 or more.");

            return Add(new(_failures, ModelRuleKind.Retry) { Times = times, Delay = delay, Backoff = backoff });
        }

        /// <summary>
        /// Moves the run to another model of the provider it is on, for the failed call and the rest of the run. The
        /// rule applies once in a run.
        /// </summary>
        /// <param name="model">The model, as the provider's API names it.</param>
        /// <returns>The policy with the rule.</returns>
        /// <exception cref="ArgumentException"><paramref name="model"/> is null or white space.</exception>
        public ModelPolicy FallbackToModel(string model)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(model);
            return Add(new(_failures, ModelRuleKind.Fallback) { Model = model });
        }

        /// <summary>
        /// Moves the run to another provider, which asks it for the model the run is on, for the failed call and the
        /// rest of the run: the client registered under the key, or the client your connector builds for a
        /// <see cref="ChatProvider"/>, which is taken as the document it is. A run checks that something serves the
        /// provider when it starts. The rule applies once in a run.
        /// </summary>
        /// <param name="provider">The provider's key, or a provider document.</param>
        /// <typeparam name="TProviderId">The key's type, any type the app registered a client under.</typeparam>
        /// <returns>The policy with the rule.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="provider"/> is text that is white space, or a document whose <c>Kind</c> is.
        /// </exception>
        public ModelPolicy FallbackToProvider<TProviderId>(TProviderId provider) where TProviderId : notnull =>
            Add(new(_failures, ModelRuleKind.Fallback) { Provider = ProviderOf(provider) });

        /// <summary>
        /// Moves the run to another provider and another model, for the failed call and the rest of the run, as for
        /// a provider that names the model otherwise. A provider is a key or a <see cref="ChatProvider"/>, as
        /// <see cref="FallbackToProvider{TProviderId}"/> takes it. The rule applies once in a run.
        /// </summary>
        /// <param name="provider">The provider's key, or a provider document.</param>
        /// <param name="model">The model, as that provider's API names it.</param>
        /// <typeparam name="TProviderId">The key's type, any type the app registered a client under.</typeparam>
        /// <returns>The policy with the rule.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="provider"/> is text that is white space, or a document whose <c>Kind</c> is, or
        /// <paramref name="model"/> is null or white space.
        /// </exception>
        public ModelPolicy FallbackTo<TProviderId>(TProviderId provider, string model) where TProviderId : notnull
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(model);
            return Add(new(_failures, ModelRuleKind.Fallback) { Provider = ProviderOf(provider), Model = model });
        }

        // D88: a record derived from ChatProvider binds to the generic overload, so a document is told apart here
        private static ProviderId ProviderOf<TProviderId>(TProviderId provider) where TProviderId : notnull
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (provider is ChatProvider document)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(document.Kind, nameof(provider));
                return new ProviderDocument(document);
            }

            if (provider is string text) ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(provider));
            return new ProviderId<TProviderId>(provider);
        }

        private ModelPolicy Add(ModelRule rule) => (_policy ?? None).With(rule);
    }
}

internal enum ModelRuleKind
{
    Retry,
    Fallback
}

// D89: one rule, which its policy compares by value: a retry's tries, delay and backoff, or a fallback's provider and
// model, each null for the one the run is on
internal sealed record ModelRule(ModelFailure Failures, ModelRuleKind Kind)
{
    public int Times { get; init; }

    public TimeSpan Delay { get; init; }

    public double Backoff { get; init; }

    public ProviderId? Provider { get; init; }

    public string? Model { get; init; }

    // The wait before the given try again, the first being 1, within what a timer waits
    public TimeSpan DelayBefore(int retry)
    {
        var ticks = Delay.Ticks * Math.Pow(Backoff, retry - 1);
        return TimeSpan.FromTicks((long)Math.Min(ticks, ModelPolicy.LongestWait.Ticks));
    }

    public override string ToString() =>
        (Kind, Provider, Model) switch
        {
            (ModelRuleKind.Retry, _, _) => string.Create(
                CultureInfo.InvariantCulture,
                $"Retry({Times}, {Delay}, {Backoff})"),
            (_, null, _) => $"FallbackToModel('{Model}')",
            (_, _, null) => $"FallbackToProvider('{Provider}')",
            _ => $"FallbackTo('{Provider}', '{Model}')"
        };
}
