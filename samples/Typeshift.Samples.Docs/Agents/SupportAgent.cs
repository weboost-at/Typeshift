using Typeshift.Agents;
using Typeshift.Samples.Docs.Knowledge;
using Typeshift.Samples.Docs.Tickets;

namespace Typeshift.Samples.Docs.Agents;

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

        UseToolset<TicketToolset>().Include(t => t.SearchTickets);

        UseToolset<KnowledgeToolset>();

        ConfigureSearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
// end-snippet
