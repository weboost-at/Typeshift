using Agentic.Agents;
using Agentic.Samples.Docs.Knowledge;
using Agentic.Samples.Docs.Tickets;

namespace Agentic.Samples.Docs.Agents;

// begin-snippet: agent
internal partial class SupportAgent : AgentDefinition
{
    public SupportAgent()
    {
        WithName("support_agent");
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.MaxOutputTokens);

        UseToolset<TicketToolset>()
            .SearchTickets()
            .GetTicket();

        UseToolset<KnowledgeToolset>();

        WithToolSettings()
            .SearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
// end-snippet
