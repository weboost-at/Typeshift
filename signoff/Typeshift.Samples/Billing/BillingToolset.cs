using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Billing;

internal partial class BillingToolset : Toolset
{
    public BillingToolset()
    {
        WithDisplayName("Billing");

        AddTool(GetInvoicesAsync);

        AddTool(RefundAsync)
            .WithName("refund_invoice")
            .RequiresApproval();
    }

    /// <summary>
    /// Lists the customer's invoices, newest first.
    /// </summary>
    public static Task<IReadOnlyList<Invoice>> GetInvoicesAsync(IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IBillingClient>().GetInvoicesAsync(ct);

    /// <summary>
    /// Refunds an invoice, in part or in full.
    /// </summary>
    /// <param name="invoice">The invoice's number.</param>
    /// <param name="amount">The amount to refund.</param>
    public static Task RefundAsync(string invoice, decimal amount, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IBillingClient>().RefundAsync(invoice, amount, ct);
}
