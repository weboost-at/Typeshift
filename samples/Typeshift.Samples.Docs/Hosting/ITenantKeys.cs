namespace Typeshift.Samples.Hosting;

// The host's own store of the API keys its customers entered, such as a table encrypted at rest
internal interface ITenantKeys
{
    string GetApiKey(int customer, string provider);
}
