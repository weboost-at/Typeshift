using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// The app's code a run called, and which of it ran where a synchronization context was current
public sealed class ContextLog
{
    public const string Tool = "tool";

    public const string RunMiddleware = "run middleware";

    public const string Load = "load";

    public const string Save = "save";

    public const string Append = "append";

    private readonly ConcurrentQueue<(string Code, bool OnContext, string? Flowed)> _calls = new();

    // A value the caller sets, which reaches the app's code through the execution context
    public AsyncLocal<string?> Flowing { get; } = new();

    public IReadOnlyList<string> Calls => [.. _calls.Select(call => call.Code)];

    public IReadOnlyList<string> OnContext => [.. _calls.Where(call => call.OnContext).Select(call => call.Code)];

    public IReadOnlyList<string?> FlowedTo(string code) =>
        [.. _calls.Where(call => call.Code == code).Select(call => call.Flowed)];

    public void Add(string code) => _calls.Enqueue((code, SynchronizationContext.Current is not null, Flowing.Value));
}

public sealed class ContextLoggingToolMiddleware(ContextLog log) : IToolMiddleware
{
    public ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        log.Add(ContextLog.Tool);
        return next();
    }
}

// Awaits as app code often does, without ConfigureAwait(false), so a context it found would get the rest of the run
public sealed class ContextLoggingRunMiddleware(ContextLog log) : IRunMiddleware
{
    public async Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        log.Add(ContextLog.RunMiddleware);
        await next();
    }
}

// The store in memory, which answers at once, so a run reaches the model without having waited
public sealed class ContextLoggingStore(ContextLog log) : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct)
    {
        log.Add(ContextLog.Load);
        return _store.LoadAsync(id, kind, ct);
    }

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        log.Add(ContextLog.Save);
        return _store.SaveAsync(id, kind, json, loadedVersion, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        log.Add(ContextLog.Load);
        return _store.LoadConversationAsync(conversation, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        log.Add(ContextLog.Load);
        return _store.LoadConversationSinceCompactionAsync(conversation, ct);
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        log.Add(ContextLog.Append);
        return _store.AppendConversationAsync(conversation, entry, ct);
    }

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct)
    {
        log.Add(ContextLog.Load);
        return _store.LoadDecisionsAsync(conversation, ct);
    }

    public Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct)
    {
        log.Add(ContextLog.Save);
        return _store.SaveDecisionsAsync(conversation, json, loadedVersion, ct);
    }
}
