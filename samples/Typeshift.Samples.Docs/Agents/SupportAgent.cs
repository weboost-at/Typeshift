using Microsoft.Extensions.DependencyInjection;
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

        // begin-snippet: agent-model
        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);
        // end-snippet

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>()
            .Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        // begin-snippet: use-mcp-server
        UseMcpServer("status_page")
            .ReadOnly();
        // end-snippet

        // begin-snippet: use-context
        UseContext<CustomerProfileContext>();
        // end-snippet

        // begin-snippet: use-questions
        UseQuestions();
        // end-snippet
    }

    protected override string GetInstructions(IServiceProvider services)
    {
        var customer = services.GetRequiredService<ICurrentCustomer>();
        return $"Answer {customer.Name} from their tickets and cite help center articles.";
    }
}
// end-snippet
