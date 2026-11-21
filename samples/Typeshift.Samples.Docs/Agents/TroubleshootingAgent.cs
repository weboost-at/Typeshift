using Microsoft.Extensions.AI;
using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Agents;

internal partial class TroubleshootingAgent : Agent
{
    public TroubleshootingAgent()
    {
        WithInstructions("Find the cause of the customer's problem in their tickets and on the web, then fix it.");

        // begin-snippet: read-only-tools
        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini")
            .WithReadOnlyTools(new HostedWebSearchTool());
        // end-snippet

        WithRunOptions()
            .WithMode(AgentMode.Plan);

        UseToolset<TicketToolset>();
    }
}
