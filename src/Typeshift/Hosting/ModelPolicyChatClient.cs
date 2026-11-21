using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D89, D29: the innermost client of a run, under compaction, in place of one instrumented client. Each model call
// goes to the pair of provider and model the run is on, through that pair's own instrumented client (D36), and a
// failure before any output reached the caller goes to the policy's rules: the first that handles its kind and is not
// used up applies, and without one the exception is thrown as the client threw it. Below the checkpoints, a failed
// attempt stores nothing and the input is stored once (D50). The summary's calls go through it too (D32)
internal sealed class ModelPolicyChatClient : IChatClient
{
    private readonly ModelPolicy _policy;
    private readonly RunClients _clients;
    private readonly InstrumentedClients _instrumented;
    private readonly TimeProvider _time;
    private readonly Func<Exception, ModelFailure?>? _classify;
    private readonly UnknownFailures _unknown;
    private readonly ILogger _logger;

    // The run's own pair, which nearly every call goes to, and any other a fallback or a summary takes
    private readonly (ProviderId? Provider, string? Model) _ownPair;
    private readonly IChatClient _own;
    private readonly ConcurrentDictionary<(ProviderId? Provider, string? Model), IChatClient> _pairs = [];

    public ModelPolicyChatClient(
        ModelPolicy policy,
        RunClients clients,
        string? model,
        InstrumentedClients instrumented,
        TimeProvider time,
        Func<Exception, ModelFailure?>? classify,
        UnknownFailures unknown,
        ILogger logger)
    {
        _policy = policy;
        _clients = clients;
        _instrumented = instrumented;
        _time = time;
        _classify = classify;
        _unknown = unknown;
        _logger = logger;
        _ownPair = (clients.Own, model);
        _own = instrumented.Instrument(clients.Of(clients.Own), model);
    }

    // A policy without rules or timeout makes one attempt, so the call goes on as the client makes it
    private bool Passes => _policy.Rules.IsEmpty && _policy.UntilFirstUpdate == Timeout.InfiniteTimeSpan;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (Passes) return _own.GetResponseAsync(messages, options, cancellationToken);

        var run = ResolvedChatOptions.OfRun(options).AgentRun!;
        return GetResponseAsync(messages, options, run, run.Models ??= new(run.ResolvedProvider), cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (Passes) return _own.GetStreamingResponseAsync(messages, options, cancellationToken);

        var run = ResolvedChatOptions.OfRun(options).AgentRun!;
        return StreamAsync(messages, options, run, run.Models ??= new(run.ResolvedProvider), cancellationToken);
    }

    // D89, D32: a summary's call, from the pair given, under the run's policy. What its rules change holds for that
    // summary alone, and the run's own calls stay on their pair
    public Task<ChatResponse> SummarizeAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options,
        AgentRun run,
        ProviderId? provider,
        CancellationToken ct) =>
        Passes
            ? Pair(provider, options.ModelId).GetResponseAsync(messages, options, ct)
            : GetResponseAsync(messages, options, run, new(provider), ct);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : _own.GetService(serviceType, serviceKey);

    // D88: the run's holds on the clients Typeshift keeps end; the clients themselves are the app's or kept
    public void Dispose() => _clients.Dispose();

    private async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        AgentRun run,
        PolicyState state,
        CancellationToken ct)
    {
        var tries = Tries();

        while (true)
        {
            var (client, call, model) = Current(state, options);
            var attempt = Attempt.Start(_policy.UntilFirstUpdate, _time, ct);

            try
            {
                return await client.GetResponseAsync(messages, call, attempt.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                await HandleAsync(exception, attempt.TimedOut, run, state, model, tries, ct).ConfigureAwait(false);
            }
            finally
            {
                attempt.Dispose();
            }
        }
    }

    // An attempt is committed once its first update reached the caller: a failure after it is thrown on, since the
    // caller holds part of an answer, and continuing it is RetryAsync's (D50)
    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        AgentRun run,
        PolicyState state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var tries = Tries();

        while (true)
        {
            var (client, call, model) = Current(state, options);
            var attempt = Attempt.Start(_policy.UntilFirstUpdate, _time, ct);
            IAsyncEnumerator<ChatResponseUpdate>? updates = null;
            Exception? failure = null;
            var any = false;

            try
            {
                updates = client.GetStreamingResponseAsync(messages, call, attempt.Token)
                    .GetAsyncEnumerator(attempt.Token);

                any = await updates.MoveNextAsync().ConfigureAwait(false);
                attempt.Arrive();
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            // A timeout that ends as the first update arrives fails the attempt too, which the caller never saw
            if (failure is not null || attempt.TimedOut)
            {
                if (updates is not null) await updates.DisposeAsync().ConfigureAwait(false);
                attempt.Dispose();

                if (ct.IsCancellationRequested && failure is not null) ExceptionDispatchInfo.Throw(failure);
                ct.ThrowIfCancellationRequested();

                await HandleAsync(failure, attempt.TimedOut, run, state, model, tries, ct).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (!any) yield break;

                yield return updates!.Current;

                while (await updates.MoveNextAsync().ConfigureAwait(false))
                {
                    yield return updates.Current;
                }
            }
            finally
            {
                await updates!.DisposeAsync().ConfigureAwait(false);
                attempt.Dispose();
            }

            yield break;
        }
    }

    // The client, the options and the model of the pair the calls are on. The run's options go on unchanged until a
    // fallback names another model, which a copy of them carries
    private (IChatClient Client, ChatOptions? Options, string? Model) Current(PolicyState state, ChatOptions? options)
    {
        if (state.Model is not { } model) return (Pair(state.Provider, options?.ModelId), options, options?.ModelId);

        var call = options?.Clone() ?? new ChatOptions();
        call.ModelId = model;

        return (Pair(state.Provider, model), call, model);
    }

    private IChatClient Pair(ProviderId? provider, string? model) =>
        (provider, model) == _ownPair
            ? _own
            : _pairs.GetOrAdd((provider, model), Instrument);

    private IChatClient Instrument((ProviderId? Provider, string? Model) pair) =>
        _instrumented.Instrument(_clients.Of(pair.Provider), pair.Model);

    // The retries of one model call, for each rule, which a fallback starts again for the pair it moves to
    private int[] Tries() => _policy.Rules.IsEmpty ? [] : new int[_policy.Rules.Length];

    // Returns when a rule applies, after a retry's wait or with the run moved to the fallback's pair; throws the
    // failure as the client threw it when none does
    private async Task HandleAsync(
        Exception? exception,
        bool timedOut,
        AgentRun run,
        PolicyState state,
        string? model,
        int[] tries,
        CancellationToken ct)
    {
        var failure = timedOut ? TimedOut(run, state, model, exception) : exception!;
        var kind = timedOut ? ModelFailure.Timeout : ModelFailures.Classify(failure, _classify);
        var rules = _policy.Rules;
        var index = -1;

        for (var i = 0; i < rules.Length && kind != ModelFailure.None; i++)
        {
            if ((rules[i].Failures & kind) == 0) continue;
            if (rules[i].Kind == ModelRuleKind.Retry ? tries[i] >= rules[i].Times : state.Fallen(i)) continue;

            index = i;
            break;
        }

        if (index < 0)
        {
            // D89: a failure no rule could ever handle, since nothing told its kind, would read as a broken policy
            if (kind == ModelFailure.Unknown && !rules.IsEmpty && rules.All(rule => (rule.Failures & kind) == 0)
                && _unknown.IsFirst(failure.GetType()))
                LogEvents.UnknownModelFailure(_logger, run.Agent.Name, failure.GetType().FullName);

            ExceptionDispatchInfo.Throw(failure);
        }

        var rule = rules[index];
        var left = (state.Provider, model);

        if (rule.Kind == ModelRuleKind.Retry)
        {
            tries[index]++;
            ModelEvents.Retried(run.Activity, kind, failure, left, tries[index]);

            var delay = rule.DelayBefore(tries[index]);
            if (delay > TimeSpan.Zero) await Task.Delay(delay, _time, ct).ConfigureAwait(false);

            return;
        }

        state.Fall(index, rule.Provider ?? state.Provider, rule.Model ?? state.Model, rules.Length);
        Array.Clear(tries);

        ModelEvents.FellBack(run.Activity, kind, failure, left, (state.Provider, rule.Model ?? model));
    }

    // D89: the policy's own timeout, which the client saw only as a cancellation nobody asked for
    private TimeoutException TimedOut(AgentRun run, PolicyState state, string? model, Exception? inner) =>
        new(
            $"The model call of the agent '{run.Agent.Name}' to {Describe(state.Provider, model)} sent no answer "
            + $"within {_policy.UntilFirstUpdate}, the timeout of its model policy.",
            inner);

    private static string Describe(ProviderId? provider, string? model) =>
        (provider, model) switch
        {
            (null, null) => "the default client's own model",
            (null, _) => $"'{model}' on the default client",
            (_, null) => $"'{provider}'",
            _ => $"'{model}' of '{provider}'"
        };

    // How long an attempt waits for its first update: a token that the caller's cancellation and the timeout end,
    // until the first update arrives. Without a timeout, the caller's own token and nothing to dispose. A struct, so a
    // run without a timeout allocates nothing for it; its holder keeps it in one variable
    private struct Attempt
    {
        private readonly CancellationTokenSource? _timer;
        private readonly CancellationTokenSource? _linked;
        private bool _arrived;

        private Attempt(CancellationToken ct, CancellationTokenSource? timer)
        {
            _timer = timer;
            _linked = timer is null ? null : CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
            Token = _linked?.Token ?? ct;
        }

        public CancellationToken Token { get; }

        public readonly bool TimedOut => !_arrived && _timer?.IsCancellationRequested is true;

        public static Attempt Start(TimeSpan timeout, TimeProvider time, CancellationToken ct) =>
            new(ct, timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout, time));

        // The timeout counts no further once an update arrived in time; one that fired as it arrived still counts
        public void Arrive()
        {
            _timer?.Dispose();
            _arrived = _timer?.IsCancellationRequested is not true;
        }

        public readonly void Dispose()
        {
            _linked?.Dispose();
            _timer?.Dispose();
        }
    }
}

// D89: where a run's model calls are: the provider a fallback moved them to, the model it named, and the fallbacks
// used, each once in a run. A summary keeps a state of its own
internal sealed class PolicyState(ProviderId? provider)
{
    private bool[]? _fallen;

    public ProviderId? Provider { get; private set; } = provider;

    // Null for the model of the call's options
    public string? Model { get; private set; }

    public bool Fallen(int rule) => _fallen?[rule] is true;

    public void Fall(int rule, ProviderId? provider, string? model, int rules)
    {
        (_fallen ??= new bool[rules])[rule] = true;
        Provider = provider;
        Model = model;
    }
}

// D89, D88: the clients of the providers a run may use, its own and those its policy's fallbacks and its summary
// name, each taken when the run starts and held until its pipeline is disposed
internal sealed class RunClients(ProviderId? own) : IDisposable
{
    private readonly List<(ProviderId? Provider, IChatClient Client, IDisposable? Hold)> _clients = [];

    public ProviderId? Own => own;

    public IEnumerable<IChatClient> All => _clients.Select(held => held.Client);

    public bool Holds(ProviderId? provider) => _clients.Any(held => Equals(held.Provider, provider));

    public void Add(ProviderId? provider, IChatClient client, IDisposable? hold) =>
        _clients.Add((provider, client, hold));

    public IChatClient Of(ProviderId? provider) => _clients.First(held => Equals(held.Provider, provider)).Client;

    public void Dispose()
    {
        foreach (var held in _clients)
        {
            held.Hold?.Dispose();
        }
    }
}

// D89: the exception types a warning named already, once each for the app
internal sealed class UnknownFailures
{
    private readonly ConcurrentDictionary<Type, bool> _warned = [];

    public bool IsFirst(Type type) => _warned.TryAdd(type, true);
}

// D89, D36: a retry and a fallback as events of the run's span, with the failure's kind and type and the pairs
internal static class ModelEvents
{
    public static void Retried(
        Activity? activity,
        ModelFailure kind,
        Exception failure,
        (ProviderId? Provider, string? Model) pair,
        int retry)
    {
        if (activity is null) return;

        var tags = Describe(kind, failure, pair, pair);
        tags["typeshift.model.retries"] = retry;
        activity.AddEvent(new ActivityEvent("typeshift.model.retry", tags: tags));
    }

    public static void FellBack(
        Activity? activity,
        ModelFailure kind,
        Exception failure,
        (ProviderId? Provider, string? Model) left,
        (ProviderId? Provider, string? Model) taken) =>
        activity?.AddEvent(new ActivityEvent("typeshift.model.fallback", tags: Describe(kind, failure, left, taken)));

    private static ActivityTagsCollection Describe(
        ModelFailure kind,
        Exception failure,
        (ProviderId? Provider, string? Model) left,
        (ProviderId? Provider, string? Model) taken) =>
        new()
        {
            ["typeshift.model.failure"] = kind.ToString(),
            ["error.type"] = failure.GetType().FullName,
            ["typeshift.model.from.provider"] = left.Provider?.ToString(),
            ["typeshift.model.from.model"] = left.Model,
            ["typeshift.model.to.provider"] = taken.Provider?.ToString(),
            ["typeshift.model.to.model"] = taken.Model
        };
}
