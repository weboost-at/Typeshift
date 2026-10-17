using Typeshift.Samples.Tickets;

namespace Typeshift.Samples.Agents;

// begin-snippet: handoff
internal partial class TriageAgent : Agent
{
    public TriageAgent()
    {
        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        // begin-snippet: exclude-tools
        UseToolset<TicketToolset>()
            .Exclude(t => t.CloseTicket);
        // end-snippet

        // begin-snippet: run-options
        WithRunOptions()
            .WithMaxToolIterations(10)
            .WithConcurrentToolCalls(true)
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations);
        // end-snippet

        UseHandoff<SupportAgent>();
        UseHandoff<BillingAgent>();
    }

    protected override string GetInstructions() =>
        "Find out what the customer needs, then hand off to support or billing.";
}
// end-snippet
