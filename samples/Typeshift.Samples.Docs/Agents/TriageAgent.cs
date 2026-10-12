using Typeshift.Agents;
using Typeshift.Samples.Docs.Tickets;

namespace Typeshift.Samples.Docs.Agents;

// begin-snippet: exclude-tools
internal partial class TriageAgent : AgentDefinition
{
    public TriageAgent()
    {
        WithChatOptions().WithModel("fast-model");

        UseToolset<TicketToolset>().Exclude(t => t.GetTicket);
    }

    protected override string GetInstructions() => "Find the tickets a customer's message is about.";
}
// end-snippet
