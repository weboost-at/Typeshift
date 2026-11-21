namespace Typeshift.Samples.Hosting;

// Your app's holder of the tenant a unit of work serves, scoped: request middleware fills it for a request, and a job
// fills it in its host's scope; the tools resolve it from the run's services
internal sealed class CurrentTenant
{
    public string? Id { get; set; }
}
