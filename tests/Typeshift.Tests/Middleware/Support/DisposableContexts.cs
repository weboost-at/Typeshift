using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Middleware.Support;

// Created for each pass, since nobody registers it, with a tool that must still work while the pass runs it
public sealed class LookupContext : IContextProvider, IAsyncDisposable
{
    public const string Tool = "lookup_customer";

    private readonly MiddlewareScript _script;

    public LookupContext(MiddlewareScript script)
    {
        _script = script;
        script.Lifetimes.Add("lookup created");
    }

    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(new AgentContext { Tools = [AIFunctionFactory.Create(Lookup, Tool)] });

    public ValueTask DisposeAsync()
    {
        _script.Lifetimes.Add("lookup disposed");
        return ValueTask.CompletedTask;
    }

    private string Lookup()
    {
        _script.Lifetimes.Add("lookup called");
        return Tool;
    }
}

// Registered as scoped, so the scope disposes it and no pass does
public sealed class ProfileContext : IContextProvider, IDisposable
{
    private readonly MiddlewareScript _script;

    public ProfileContext(MiddlewareScript script)
    {
        _script = script;
        script.Lifetimes.Add("profile created");
    }

    public ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(new AgentContext());

    public void Dispose() => _script.Lifetimes.Add("profile disposed");
}

public partial class LookupContextAgent : Agent
{
    public LookupContextAgent()
    {
        WithChatOptions().WithProvider("scripted").WithModel("context-model");

        UseContext<LookupContext>();
    }
}

public partial class ProfileContextAgent : Agent
{
    public ProfileContextAgent()
    {
        WithChatOptions().WithProvider("scripted").WithModel("context-model");

        UseContext<ProfileContext>();
    }
}
