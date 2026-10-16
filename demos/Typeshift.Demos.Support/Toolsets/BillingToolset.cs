using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Support.Domain;

namespace Typeshift.Demos.Support.Toolsets;

internal partial class BillingToolset : Toolset
{
    public BillingToolset()
    {
        WithDisplayName("Billing");

        AddTool(ListInvoices)
            .ReadOnly();

        AddTool(IssueRefund)
            .RequiresApproval();
    }

    /// <summary>
    /// Lists the customer's invoices with what was refunded so far.
    /// </summary>
    public static IReadOnlyList<Invoice> ListInvoices(IServiceProvider services)
    {
        var customer = services.GetRequiredService<CurrentCustomer>().Id;
        return [.. services.GetRequiredService<CrmData>().Invoices.Where(invoice => invoice.Customer == customer)];
    }

    /// <summary>
    /// Refunds part or all of an invoice to the customer's payment method.
    /// </summary>
    /// <param name="invoice">The invoice number, such as INV-7.</param>
    /// <param name="amountCents">The amount in cents, at most what is left of the invoice.</param>
    /// <param name="reason">Why, as the customer will read it.</param>
    public static string IssueRefund(string invoice, int amountCents, string reason, IServiceProvider services)
    {
        var data = services.GetRequiredService<CrmData>();
        var customer = services.GetRequiredService<CurrentCustomer>().Id;

        var found = data.Invoices.FirstOrDefault(known => known.Number == invoice && known.Customer == customer)
            ?? throw new InvalidOperationException($"The customer has no invoice {invoice}; list the invoices first.");

        if (amountCents <= 0 || amountCents > found.AmountCents - found.RefundedCents)
            throw new ArgumentException($"The refund must be between 1 and {found.AmountCents - found.RefundedCents} cents.");

        data.Invoices[data.Invoices.IndexOf(found)] = found with { RefundedCents = found.RefundedCents + amountCents };
        data.Log.Add($"refund {amountCents} on {invoice}: {reason}");
        return $"Refunded {amountCents / 100.0:F2} on {invoice}; it reaches the customer within five days.";
    }
}
