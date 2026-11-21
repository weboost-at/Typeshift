namespace Typeshift.Samples.Hosting;

// begin-snippet: customer-provider
// A provider a customer brings: the record your app keeps in its own storage, with the members it needs
internal sealed record CustomerProvider(Guid Id, int Customer) : ChatProvider;
// end-snippet

// Your app's own store of the providers its customers entered, such as a table whose keys are encrypted at rest
internal interface ICustomerProviders
{
    Task<CustomerProvider> LoadAsync(int customer, CancellationToken ct);
}

// The provider the sample customer entered, whose key is no key: a client built with it calls nobody until a run does
internal sealed class SampleCustomerProviders : ICustomerProviders
{
    public Task<CustomerProvider> LoadAsync(int customer, CancellationToken ct) =>
        Task.FromResult(new CustomerProvider(Guid.Parse("01965f2e-7a3c-7d40-9b1e-6c2f4a8d0e51"), customer)
        {
            Kind = "openai",
            Name = $"Customer {customer}'s OpenAI account",
            ApiKey = $"sk-not-a-key-of-customer-{customer}",
            Model = "gpt-5-mini"
        });
}
