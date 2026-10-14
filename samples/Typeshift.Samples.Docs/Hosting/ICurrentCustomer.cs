namespace Typeshift.Samples.Hosting;

// The host's own scoped service, filled per request; Typeshift resolves everything from the request's scope
internal interface ICurrentCustomer
{
    int Id { get; }

    string Name { get; }

    string Plan { get; }
}
