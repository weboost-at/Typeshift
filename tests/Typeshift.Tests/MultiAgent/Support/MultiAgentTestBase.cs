using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Approval.Support;
using Typeshift.Tests.Testing.Fakes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.MultiAgent.Support;

// The agent tests' host, with the helper's model on a scripted client of its own, so a sub-agent working alongside
// its caller answers from its own script
public abstract class MultiAgentTestBase : AgentTestBase
{
    protected ScriptedChatClient HelperClient { get; } = new();

    protected ToolCalls ToolCalls { get; } = [];

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddTypeshift().WithChatClientProvider<TwoClientProvider>();
        services.AddSingleton(new HelperChatClient(HelperClient));
        services.AddSingleton(ToolCalls);
    }
}

public sealed record HelperChatClient(ScriptedChatClient Client);

public sealed class TwoClientProvider(ScriptedChatClient client, HelperChatClient helper, ProvidedModels models)
    : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct)
    {
        models.Add(model);
        return ValueTask.FromResult<IChatClient>(model == HelperAgent.Model ? helper.Client : client);
    }
}

// Answers one question, with the guarded tools
public partial class HelperAgent : Agent
{
    public const string Model = "helper-model";

    public const string About = "Answers one question about a ticket.";

    public HelperAgent()
    {
        WithDescription(About);

        WithChatOptions().WithModel(Model);

        UseToolset<GuardedToolset>();
    }
}

// The helper's guarded tools are the point: the tests see what becomes of a call that needs approval
#pragma warning disable TSH024
public partial class LeadAgent : Agent
{
    public LeadAgent()
    {
        WithChatOptions().WithModel("lead-model");

        WithRunOptions().Configurable(r => r.Mode);

        UseAgent<HelperAgent>();
    }
}

public partial class BackgroundLeadAgent : Agent
{
    public BackgroundLeadAgent()
    {
        WithChatOptions().WithModel("lead-model");

        UseAgent<HelperAgent>().InBackground();
    }
}
#pragma warning restore TSH024

public partial class FrontAgent : Agent
{
    public FrontAgent()
    {
        WithDescription("Finds out what the customer needs.");

        WithChatOptions().WithModel("front-model");

        UseHandoff<BackAgent>();
    }

    protected override string GetInstructions() => "Find out what the customer needs.";
}

public partial class BackAgent : Agent
{
    public const string Prompt = "Settle the customer's billing.";

    public BackAgent()
    {
        WithDescription("Settles billing.");

        WithChatOptions().WithModel("back-model");

        UseHandoff<FrontAgent>();
    }

    protected override string GetInstructions() => Prompt;
}
