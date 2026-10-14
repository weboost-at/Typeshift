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

    public AgentContext Context { get; set; } = new();
}

public sealed class FirstRunMiddleware(MiddlewareScript script) : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) =>
        ScriptedRunMiddleware.InvokeAsync(script, 0, run, next);
}

public sealed class SecondRunMiddleware(MiddlewareScript script) : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) =>
        ScriptedRunMiddleware.InvokeAsync(script, 1, run, next);
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
    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(script.Context);
}

// Each middleware logs around what it does: calling next, or the script in its place
internal static class ScriptedRunMiddleware
{
    public static async Task InvokeAsync(MiddlewareScript script, int position, AgentRun run, Func<Task> next)
    {
        script.Log.Add($"run {position} before");

        await (script.Runs.TryGetValue(position, out var instead) ? instead(run, next) : next());

        script.Log.Add($"run {position} after: {run.Response?.Text}");
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
