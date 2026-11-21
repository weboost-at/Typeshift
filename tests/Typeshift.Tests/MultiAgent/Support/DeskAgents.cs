namespace Typeshift.Tests.MultiAgent.Support;

// A front desk and the agent it hands the conversation to, each on a provider of its own, so a test tells whose
// provider a run, its span and its entries name
public partial class FrontDeskAgent : Agent
{
    public const string AgentName = "front_desk_agent";

    public const string ModelProvider = "front-provider";

    public FrontDeskAgent()
    {
        WithDescription("Finds out what the customer needs.");

        WithChatOptions().WithProvider(ModelProvider).WithModel("front-model");

        UseHandoff<BackOfficeAgent>();
    }
}

public partial class BackOfficeAgent : Agent
{
    public const string AgentName = "back_office_agent";

    public const string ModelProvider = "back-provider";

    public const string Handoff = "handoff_to_back_office_agent";

    public BackOfficeAgent()
    {
        WithDescription("Settles what the customer needs.");

        WithChatOptions().WithProvider(ModelProvider).WithModel("back-model");

        UseHandoff<FrontDeskAgent>();
    }
}
