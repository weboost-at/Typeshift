using Typeshift.Demos.Providers;
using Typeshift.Demos.Support.Hosting;
using Typeshift.Demos.Support.Toolsets;

namespace Typeshift.Demos.Support.Agents;

// Finds out what the customer needs and hands the conversation to the agent for it
internal partial class TriageAgent : Agent
{
    public TriageAgent()
    {
        WithDescription("Finds out what the customer needs and hands the conversation to support or billing.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .Configurable(o => o.ModelId);

        UseContext<CustomerContext>();

        UseHandoff<SupportAgent>();
        UseHandoff<BillingAgent>();
    }

    protected override string GetInstructions() =>
        "Greet the customer, find out what they need in at most one question, then hand off to support for tickets "
        + "and help, or to billing for invoices and refunds.";
}

// Answers from the tickets and the help center; the help center is a sub-agent, the status page an MCP server
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets, the help center and the service status.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>();

        UseAgent<KnowledgeAgent>();

        UseMcpServer("status")
            .ReadOnly();

        UseContext<CustomerContext>();

        UseHandoff<BillingAgent>();

        ConfigureSearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        "Answer from the customer's tickets. Ask knowledge_agent for help center answers and service_status when "
        + "the customer asks whether something is down. Hand off to billing for invoices and refunds.";
}

// Settles invoices; a refund asks the host first
internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Settles the customer's invoices and refunds.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .Configurable(o => o.ModelId);

        UseToolset<BillingToolset>();

        UseContext<CustomerContext>();

        UseHandoff<SupportAgent>();

        WithRunOptions()
            .Configurable(r => r.Mode);
    }

    protected override string GetInstructions() =>
        "List the invoices before you refund. Refund only what the customer was charged twice or wrongly, and say "
        + "when it arrives. Hand off to support for anything but invoices.";
}

// Answers one question from the help center, as a tool of the support agent
internal partial class KnowledgeAgent : Agent
{
    public KnowledgeAgent()
    {
        WithDescription("Answers one question from the help center, quoting the article.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .Configurable(o => o.ModelId);

        UseToolset<KnowledgeToolset>();
    }

    protected override string GetInstructions() => "Search the help center and answer in two sentences.";
}

// Writes the structured summary of a conversation; it offers no tools, so a local model may answer with a schema
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithDescription("Summarizes a support conversation.");

        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
            .WithTemperature(0)
            .Configurable(o => o.ModelId);
    }

    protected override string GetInstructions() =>
        "Summarize the support conversation you are given as JSON of the schema requested.";
}
