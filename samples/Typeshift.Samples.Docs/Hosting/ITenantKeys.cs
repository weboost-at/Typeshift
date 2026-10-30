namespace Typeshift.Samples.Hosting;

// Your app's own store of the API keys its customers entered, such as a table encrypted at rest
internal interface ITenantKeys
{
    Task<string> GetApiKeyAsync(int customer, string provider, CancellationToken ct);
}
