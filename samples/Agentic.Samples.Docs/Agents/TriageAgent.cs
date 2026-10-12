using Agentic.Agents;
using Agentic.Samples.Docs.Tickets;

namespace Agentic.Samples.Docs.Agents;

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
