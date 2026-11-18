using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware.Support;

// What a test scripts for the middleware and the context provider, and what they saw
public sealed class MiddlewareScript
{
    public List<string> Log { get; } = [];

    // By the middleware's position: what it does in place of calling next
    public Dictionary<int, Func<AgentRun, Func<Task>, Task>> Runs { get; } = [];

    public Dictionary<int, Func<FunctionInvocationContext, Func<ValueTask<object?>>, ValueTask<object?>>> Tools
    {
        get;
    } = [];

    // By the middleware's position: what it makes of steering input in place of letting it pass
    public Dictionary<int, Func<ChatMessage, ChatMessage?>> Steering { get; } = [];

    public AgentContext Context { get; set; } = new();

    // The runs the context provider was asked for
    public List<AgentRun> ContextRuns { get; } = [];
}

public sealed class FirstRunMiddleware(MiddlewareScript script) : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) =>
        ScriptedRunMiddleware.InvokeAsync(script, 0, run, next, ct);

    public ValueTask<ChatMessage?> OnSteeringAsync(AgentRun run, ChatMessage input, CancellationToken ct) =>
        ScriptedRunMiddleware.OnSteeringAsync(script, 0, input);
}

public sealed class SecondRunMiddleware(MiddlewareScript script) : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) =>
        ScriptedRunMiddleware.InvokeAsync(script, 1, run, next, ct);

    public ValueTask<ChatMessage?> OnSteeringAsync(AgentRun run, ChatMessage input, CancellationToken ct) =>
        ScriptedRunMiddleware.OnSteeringAsync(script, 1, input);
}

public sealed class FirstToolMiddleware(MiddlewareScript script) : IToolMiddleware
{
    public ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct) =>
        ScriptedToolMiddleware.InvokeAsync(script, 0, call, next);
}

public sealed class SecondToolMiddleware(MiddlewareScript script) : IToolMiddleware
{
    public ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct) =>
        ScriptedToolMiddleware.InvokeAsync(script, 1, call, next);
}

public sealed class ScriptedContext(MiddlewareScript script) : IContextProvider
{
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct)
    {
        script.ContextRuns.Add(run);
        return ValueTask.FromResult(script.Context);
    }
}

// Each middleware logs around what it does: calling next, or the script in its place
internal static class ScriptedRunMiddleware
{
    public static async Task InvokeAsync(
        MiddlewareScript script,
        int position,
        AgentRun run,
        Func<Task> next,
        CancellationToken ct)
    {
        // A run that passes here without end stops with the test's time limit
        ct.ThrowIfCancellationRequested();
        script.Log.Add($"run {position} before");

        await (script.Runs.TryGetValue(position, out var instead) ? instead(run, next) : next());

        script.Log.Add($"run {position} after: {run.Response?.Text}");
    }

    // Each middleware logs the steering input it is shown, then lets it pass or does what the script says
    public static ValueTask<ChatMessage?> OnSteeringAsync(MiddlewareScript script, int position, ChatMessage input)
    {
        script.Log.Add($"steering {position}: {input.Text}");

        return new(script.Steering.TryGetValue(position, out var instead) ? instead(input) : input);
    }
}

internal static class ScriptedToolMiddleware
{
    public static async ValueTask<object?> InvokeAsync(
        MiddlewareScript script,
        int position,
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next)
    {
        script.Log.Add($"tool {position} before {call.Function.Name}");

        var result = await (script.Tools.TryGetValue(position, out var instead) ? instead(call, next) : next());

        script.Log.Add($"tool {position} after {call.Function.Name}");
        return result;
    }
}
