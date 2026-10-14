using Typeshift.Samples.Billing;

namespace Typeshift.Samples.Agents;

internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Answers questions about invoices and refunds them.");

        WithChatOptions().WithModel("reasoning-model");

        UseToolset<BillingToolset>();
    }

    protected override string GetInstructions() =>
        "Answer from the customer's invoices. Refund only what the customer asks for.";
}
