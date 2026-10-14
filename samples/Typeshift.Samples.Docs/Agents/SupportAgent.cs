using Typeshift.Samples.Hosting;
using Typeshift.Samples.Knowledge;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Agents;

// begin-snippet: agent
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets).Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        // begin-snippet: use-context
        UseContext<CustomerProfileContext>();
        // end-snippet
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
// end-snippet
