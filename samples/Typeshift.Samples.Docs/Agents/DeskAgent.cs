using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Agents;

// Offers the ticket toolsets the guide shows on their own, so the toolsets scenario calls their tools as a model would
internal partial class DeskAgent : Agent
{
    public DeskAgent()
    {
        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        UseToolset<TicketLookupToolset>();
        UseToolset<TicketEscalationToolset>();
        UseToolset<TicketScreenshotToolset>();
    }
}
