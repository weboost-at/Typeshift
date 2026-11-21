namespace Typeshift.Samples.Hosting;

// The keys the sample customers entered, which are no keys: a client built with one calls nobody until a run does
internal sealed class SampleTenantKeys : ITenantKeys
{
    public Task<string> GetApiKeyAsync(int customer, string provider, CancellationToken ct) =>
        Task.FromResult($"sk-not-a-key-of-customer-{customer}");
}
