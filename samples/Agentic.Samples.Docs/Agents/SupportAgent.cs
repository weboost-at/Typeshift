using Agentic.Agents;
using Agentic.Samples.Docs.Knowledge;
using Agentic.Samples.Docs.Tickets;
using Microsoft.Extensions.AI;

namespace Agentic.Samples.Docs.Agents;

// begin-snippet: agent
internal partial class SupportAgent : AgentDefinition
{
    public SupportAgent()
    {
        WithName("support_agent");
        WithDescription("Answers customers from their tickets and the help center.");

        var options = new ChatOptions
        {
            ModelId = "fast-model",
            Instructions = "Answer from the customer's tickets and cite help center articles.",
            Temperature = 0.3f
        };

        WithChatOptions(options, picker => picker
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.MaxOutputTokens));

        UseToolset<TicketToolset>(tools => tools
            .SearchTickets()
            .GetTicket());

        UseToolset<KnowledgeToolset>();

        WithToolSettings(new SearchTicketsSettings { Limit = 5 });
    }
}
// end-snippet
