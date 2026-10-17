namespace Typeshift.Samples.Agents;

internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Settles charges and invoices.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");
    }

    protected override string GetInstructions() => "Settle the customer's charges and invoices.";
}
