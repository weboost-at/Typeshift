namespace Typeshift.Samples.Billing;

internal sealed record Invoice(string Number, decimal Amount, bool Paid);
