namespace Typeshift.Samples.Agents;

// begin-snippet: agent-own-tool
internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Settles charges and invoices.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        AddTool(GetToday)
            .ReadOnly();
    }

    /// <summary>
    /// Returns today's date, to tell whether an invoice is overdue.
    /// </summary>
    private static DateOnly GetToday(IServiceProvider services)
    {
        var time = services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(time.GetLocalNow().DateTime);
    }

    protected override string GetInstructions() => "Settle the customer's charges and invoices.";
}
// end-snippet
