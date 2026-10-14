namespace Typeshift.Samples.Billing;

internal interface IBillingClient
{
    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken ct);

    Task RefundAsync(string invoice, decimal amount, CancellationToken ct);
}
