using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Decisions.Support;

// The approval tests' host over a store that can fail or lose a race, with a tool that blocks until released, and
// further processes of the app: containers of their own over the same store, script and tools
public abstract class DecisionTestBase : ApprovalTestBase
{
    private readonly List<ServiceProvider> _processes = [];

    protected UnreliableStore Store { get; } = new();

    protected ToolHold Hold { get; } = new();

    // The agent with the held tool, in the test's conversation
    protected AgentHost<HeldAgent> Held => Host<HeldAgent>().WithConversationId(Data.Agent.ConversationId);

    protected AgentHost<TAgent> OtherProcess<TAgent>() where TAgent : Agent
    {
        var services = new ServiceCollection();

        services.AddTypeshift()
            .WithStore(Store.Inner)
            .WithChatClientProvider<ScriptedChatClientProvider>()
            .WithApprovalRule<ScriptedRule>()
            .WithApprovalRule<SecondScriptedRule>();

        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton(ChatClient).AddSingleton(Models).AddSingleton(ToolCalls).AddSingleton(Rules);
        services.AddSingleton(Hold);

        var process = services.BuildServiceProvider();
        _processes.Add(process);

        return process.GetRequiredService<TypeshiftRuntime>()
            .Agent<TAgent>()
            .WithConversationId(Data.Agent.ConversationId);
    }

    // The call of the held tool that every test of a stopped run scripts
    protected ChatResponseUpdate SendRefund(string callId) =>
        Call(callId, "send_refund", ("number", Data.Agent.Ticket.Default));

    // The texts of the tool results the last model request carried, by call id
    protected Dictionary<string, string> LastResults() =>
        ChatClient.Requests[^1]
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .ToDictionary(result => result.CallId, result => result.Result?.ToString() ?? "");

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithStore(Store);
        services.AddSingleton(Hold);
    }

    protected override async ValueTask DisposeCoreAsync()
    {
        foreach (var process in _processes)
        {
            await process.DisposeAsync();
        }
    }
}
