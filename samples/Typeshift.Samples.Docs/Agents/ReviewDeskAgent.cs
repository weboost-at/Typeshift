using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Agents;

// A desk whose team reviews every escalation, and whose customers may switch the ticket search off
internal partial class ReviewDeskAgent : Agent
{
    public ReviewDeskAgent()
    {
        WithInstructions("Help the customer with their tickets. Escalate a ticket only when it is urgent.");

        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini");

        // begin-snippet: use-requires-approval
        UseToolset<TicketEscalationToolset>()
            .RequiresApproval(t => t.EscalateTicket);
        // end-snippet

        // begin-snippet: switchable-tool
        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.GetTicket)
            .Configurable(t => t.SearchTickets);
        // end-snippet
    }
}
